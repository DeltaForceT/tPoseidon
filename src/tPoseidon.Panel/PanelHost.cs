using Avalonia.Threading;
using Npgsql;
using tPoseidon.Hosting;
using tPoseidon.Hosting.Database;
using tPoseidon.Hosting.Processes;
using tPoseidon.Hosting.Servers;

namespace tPoseidon.Panel;

public enum ServiceTransition
{
    Refreshed,
    Started,
    Stopped,
    Crashed,
}

public sealed class ServiceModel
{
    public const string DatabaseName = "Postgres";

    public ServiceModel(ServerDefinition? server, int port)
    {
        Server = server;
        Port = port;
    }

    public ServerDefinition? Server { get; }

    public string Name => Server?.ShortName ?? DatabaseName;

    public string Title => Server?.Title ?? "PostgreSQL";

    public bool IsDatabase => Server is null;

    public int Port { get; internal set; }

    public ServerStatus Status { get; internal set; } = ServerStatus.Stopped;

    public DateTime? StartedUtc { get; internal set; }

    public double Cpu { get; internal set; }

    public double RamMb { get; internal set; }

    public string? Detail { get; internal set; }

    public bool Busy { get; internal set; }

    public bool Measured { get; internal set; }

    internal bool StopRequested { get; set; }
}

public sealed record PanelNotice(BannerKind Kind, string Title, string? Detail);

public sealed class PanelHost
{
    private const int DatabaseProbeEveryTicks = 5;
    private const int DefaultPostgresPort = 5432;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    private readonly DispatcherTimer _poll = new() { Interval = PollInterval };
    private readonly ProcessMeter _meter = new();
    private List<ServiceModel> _services = [];
    private bool _sampling;
    private bool _ended;
    private bool _firstSample = true;
    private int _tick;
    private string? _lastDatabaseState;

    public PanelHost(InstallPaths paths)
    {
        Paths = paths;
        _poll.Tick += (_, _) => Poll();
    }

    public event Action? ServicesRebuilt;

    public event Action<ServiceModel, ServiceTransition>? ServiceChanged;

    public event Action<PanelNotice>? Notice;

    public event Action? PlayAccountChanged;

    public InstallPaths Paths { get; }

    public HostSettings? Settings { get; private set; }

    public bool IsSetUp => Settings is not null;

    public bool AnyBusy => _services.Any(service => service.Busy);

    public IReadOnlyList<ServiceModel> Services => _services;

    public IEnumerable<ServiceModel> Servers => _services.Where(service => !service.IsDatabase);

    public void Reload()
    {
        try
        {
            Settings = HostSettings.Load(Paths);
        }
        catch (HostingException ex)
        {
            Settings = null;
            LogHub.Write(LogHub.PanelSource, LogLevel.Error, ex.Summary);
            Notify(BannerKind.Error, ex.Summary, ex.Hint);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Settings = null;
            LogHub.Write(LogHub.PanelSource, LogLevel.Error, $"{Paths.SettingsFile} could not be read: {ex.Message}");
            Notify(BannerKind.Error, "The settings could not be read.", $"{Paths.SettingsFile}: {ex.Message}");
        }

        var settings = Settings ?? new HostSettings();
        var services = new List<ServiceModel> { new(null, DatabasePort(settings)) };
        services.AddRange(ServerCatalog.Enabled(settings).Select(server => new ServiceModel(server, server.Port(settings.Ports))));
        _services = services;
        _firstSample = true;
        _tick = 0;
        _lastDatabaseState = null;
        LogHub.Follow(Settings is null
            ? []
            : ServerCatalog.Enabled(settings).Select(server => new LogSource(server.ShortName, Paths.LogFile(server.Name))).ToList());
        ServicesRebuilt?.Invoke();
        Poll();
    }

    public void Begin()
    {
        _ended = false;
        _poll.Start();
    }

    public void End()
    {
        _ended = true;
        _poll.Stop();
        LogHub.StopFollowing();
    }

    public async Task StartAllAsync()
    {
        if (Settings is not { } settings || AnyBusy)
        {
            return;
        }

        var servers = Servers.ToList();
        foreach (var service in servers)
        {
            service.Busy = true;
            service.StopRequested = false;
        }

        var observer = new StartupObserver(this);
        try
        {
            await Task.Run(() => ServerStartup.StartAllAsync(Paths, settings, observer, CancellationToken.None)).ConfigureAwait(true);
            LogHub.Write(LogHub.PanelSource, LogLevel.Info, $"All servers are running. Players connect to {settings.Network.PublicAddress}.");
        }
        catch (HostingException ex)
        {
            if (observer.Current is { } failed)
            {
                failed.Status = ServerStatus.Crashed;
                failed.Detail = ex.Summary;
                ServiceChanged?.Invoke(failed, ServiceTransition.Crashed);
            }

            foreach (var service in servers.Where(service => service != observer.Current))
            {
                service.StopRequested = true;
            }

            Fail(observer.Current?.Name ?? LogHub.PanelSource, ex);
        }
        finally
        {
            foreach (var service in servers)
            {
                service.Busy = false;
            }

            Poll();
        }
    }

    public async Task StopAllAsync()
    {
        if (Settings is not { } settings || AnyBusy)
        {
            return;
        }

        var servers = Servers.ToList();
        foreach (var service in servers)
        {
            service.Busy = true;
            service.StopRequested = true;
        }

        try
        {
            var stopped = await Task.Run(() => ServerStartup.StopAll(Paths, settings)).ConfigureAwait(true);
            foreach (var definition in stopped)
            {
                if (servers.FirstOrDefault(service => service.Server == definition) is { } service)
                {
                    MarkStopped(service);
                }
            }

            LogHub.Write(LogHub.PanelSource, LogLevel.Info, "All servers are stopped. PostgreSQL keeps running; it is managed by your system.");
        }
        finally
        {
            foreach (var service in servers)
            {
                service.Busy = false;
            }

            Poll();
        }
    }

    public async Task RestartAllAsync()
    {
        await StopAllAsync().ConfigureAwait(true);
        await StartAllAsync().ConfigureAwait(true);
    }

    public async Task StartAsync(ServiceModel service)
    {
        ArgumentNullException.ThrowIfNull(service);
        if (Settings is not { } settings || service.Server is not { } server || service.Busy)
        {
            return;
        }

        service.Busy = true;
        service.StopRequested = false;
        var progress = new StartupObserver(this);
        try
        {
            await Task.Run(() => new ServerSupervisor(Paths, settings).StartAsync(server, progress, CancellationToken.None)).ConfigureAwait(true);
        }
        catch (HostingException ex)
        {
            service.Status = ServerStatus.Crashed;
            service.Detail = ex.Summary;
            ServiceChanged?.Invoke(service, ServiceTransition.Crashed);
            Fail(service.Name, ex);
        }
        finally
        {
            service.Busy = false;
            Poll();
        }
    }

    public async Task StopAsync(ServiceModel service)
    {
        ArgumentNullException.ThrowIfNull(service);
        if (Settings is not { } settings || service.Server is not { } server || service.Busy)
        {
            return;
        }

        service.Busy = true;
        service.StopRequested = true;
        try
        {
            var stopped = await Task.Run(() => new ServerSupervisor(Paths, settings).Stop(server)).ConfigureAwait(true);
            MarkStopped(service);
            if (stopped)
            {
                LogHub.Write(service.Name, LogLevel.Info, $"{server.Title} stopped");
            }
        }
        finally
        {
            service.Busy = false;
            Poll();
        }
    }

    public async Task RestartAsync(ServiceModel service)
    {
        ArgumentNullException.ThrowIfNull(service);
        await StopAsync(service).ConfigureAwait(true);
        await StartAsync(service).ConfigureAwait(true);
    }

    public bool AnyServerRunning() => Settings is { } settings && new ServerSupervisor(Paths, settings).AnyRunning();

    public async Task BackupAsync()
    {
        if (Settings is not { } settings)
        {
            return;
        }

        if (Servers.Any(service => service.Status is ServerStatus.Running or ServerStatus.Starting))
        {
            Notify(BannerKind.Warning, "Stop the servers before a backup.", "A backup taken while the servers run may not be consistent.");
            return;
        }

        try
        {
            var pgDump = DatabaseBackup.LocatePgDump();
            LogHub.Write(LogHub.PanelSource, LogLevel.Info, $"Backing up {settings.Database.Describe()} with {pgDump}...");
            var target = await Task.Run(() => DatabaseBackup.Run(Paths, settings, pgDump)).ConfigureAwait(true);
            LogHub.Write(LogHub.PanelSource, LogLevel.Info, $"Backup written to {target}. {DatabaseBackup.RestoreHint(target)}");
            Notify(BannerKind.Success, "Backup written.", target);
        }
        catch (HostingException ex)
        {
            Fail(LogHub.PanelSource, ex);
        }
    }

    public void LaunchClient()
    {
        if (Settings is not { } settings)
        {
            return;
        }

        try
        {
            var target = GameClientLauncher.Launch(settings);
            LogHub.Write(LogHub.PanelSource, LogLevel.Info, $"Started the game client, connecting to {target}.");
        }
        catch (HostingException ex)
        {
            Fail(LogHub.PanelSource, ex);
        }
    }

    public string PlayAccountName => Settings?.Launch.Account ?? "";

    public async Task<IReadOnlyList<string>> ListAccountsAsync()
    {
        if (Settings is not { } settings)
        {
            return [];
        }

        try
        {
            return await Task.Run(() => new DatabaseAccountDirectory(settings.Database).Names()).ConfigureAwait(true);
        }
        catch (HostingException ex)
        {
            Fail(LogHub.PanelSource, ex);
            return [];
        }
    }

    public async Task ChoosePlayAccountAsync(string username)
    {
        if (Settings is not { } settings)
        {
            return;
        }

        try
        {
            var chosen = await Task.Run(() =>
            {
                var name = PlayAccount.Choose(settings, new DatabaseAccountDirectory(settings.Database), username);
                settings.Save(Paths);
                return name;
            }).ConfigureAwait(true);
            LogHub.Write(LogHub.PanelSource, LogLevel.Info, $"Play now logs in with '{chosen}'.");
            PlayAccountChanged?.Invoke();
        }
        catch (HostingException ex)
        {
            Fail(LogHub.PanelSource, ex);
        }
    }

    public void Notify(BannerKind kind, string title, string? detail) => Notice?.Invoke(new PanelNotice(kind, title, detail));

    private void Fail(string source, HostingException ex)
    {
        LogHub.Write(source, LogLevel.Error, ex.Summary);
        if (ex.Hint is { } hint)
        {
            foreach (var line in hint.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                LogHub.Write(source, LogLevel.Info, line);
            }
        }

        Notify(BannerKind.Error, ex.Summary, ex.Hint?.Split('\n')[0].Trim());
    }

    private void MarkStopped(ServiceModel service)
    {
        if (service.Status == ServerStatus.Stopped)
        {
            return;
        }

        service.Status = ServerStatus.Stopped;
        service.StartedUtc = null;
        service.Detail = null;
        ServiceChanged?.Invoke(service, ServiceTransition.Stopped);
    }

    private static int DatabasePort(HostSettings settings) =>
        string.IsNullOrEmpty(settings.Database.Connection) ? DefaultPostgresPort : new NpgsqlConnectionStringBuilder(settings.Database.Connection).Port;

    private void Poll()
    {
        if (_sampling)
        {
            return;
        }

        _sampling = true;
        var services = _services;
        var settings = Settings;
        var probeDatabase = settings is not null && _tick++ % DatabaseProbeEveryTicks == 0;
        _ = Task.Run(() => Sample(services, settings, probeDatabase)).ContinueWith(
            task => Dispatcher.UIThread.Post(() =>
            {
                _sampling = false;
                if (task.Exception is { } error)
                {
                    LogHub.Write(LogHub.PanelSource, LogLevel.Error, $"Reading the server state failed: {error.GetBaseException().Message}");
                    return;
                }

                if (!_ended && services == _services)
                {
                    Apply(task.Result);
                }
            }),
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default);
    }

    private sealed record ServerSample(ServiceModel Service, ServerState? State, ProcessUsage? Usage);

    private sealed record Sampled(IReadOnlyList<ServerSample> Servers, DatabaseSample? Database);

    private sealed record DatabaseSample(Version? Version, string? Problem);

    private Sampled Sample(IReadOnlyList<ServiceModel> services, HostSettings? settings, bool probeDatabase)
    {
        if (settings is null)
        {
            return new Sampled([], null);
        }

        var supervisor = new ServerSupervisor(Paths, settings);
        var samples = new List<ServerSample>();
        foreach (var service in services)
        {
            if (service.Server is not { } server)
            {
                continue;
            }

            var state = supervisor.State(server);
            var usage = state.ProcessId is { } pid ? _meter.Sample(pid) : null;
            samples.Add(new ServerSample(service, state, usage));
        }

        DatabaseSample? database = null;
        if (probeDatabase)
        {
            try
            {
                database = new DatabaseSample(DatabaseAdmin.ProbeAsync(settings.Database.Connection, CancellationToken.None).GetAwaiter().GetResult(), null);
            }
            catch (HostingException ex)
            {
                database = new DatabaseSample(null, ex.Summary);
            }
        }

        return new Sampled(samples, database);
    }

    private void Apply(Sampled sampled)
    {
        var silent = _firstSample;
        _firstSample = false;
        foreach (var sample in sampled.Servers)
        {
            ApplyServer(sample, silent);
        }

        if (sampled.Database is { } database && _services.FirstOrDefault(service => service.IsDatabase) is { } row)
        {
            ApplyDatabase(row, database, silent);
        }

        foreach (var service in _services)
        {
            ServiceChanged?.Invoke(service, ServiceTransition.Refreshed);
        }
    }

    private void ApplyServer(ServerSample sample, bool silent)
    {
        var service = sample.Service;
        var state = sample.State!;
        service.Measured = sample.Usage is not null;
        service.Cpu = sample.Usage?.CpuPercent ?? 0;
        service.RamMb = sample.Usage?.MemoryMegabytes ?? 0;
        service.StartedUtc = state.StartedUtc;
        if (service.Busy)
        {
            return;
        }

        var previous = service.Status;
        var next = state.Running
            ? state.Listening ? ServerStatus.Running : ServerStatus.Starting
            : previous is ServerStatus.Running or ServerStatus.Starting && !service.StopRequested ? ServerStatus.Crashed
            : previous == ServerStatus.Crashed ? ServerStatus.Crashed
            : ServerStatus.Stopped;
        if (next == previous)
        {
            return;
        }

        service.Status = next;
        if (next == ServerStatus.Running)
        {
            service.Detail = null;
            if (!silent)
            {
                ServiceChanged?.Invoke(service, ServiceTransition.Started);
            }
        }
        else if (next == ServerStatus.Crashed)
        {
            service.Detail = "Stopped unexpectedly";
            LogHub.Write(service.Name, LogLevel.Error, $"{service.Title} stopped unexpectedly. Its last lines are in {Paths.LogFile(service.Server!.Name)}.");
            Notify(BannerKind.Error, $"{service.Title} stopped unexpectedly.", "Open its log to see the last lines it wrote.");
            ServiceChanged?.Invoke(service, ServiceTransition.Crashed);
        }
        else if (next == ServerStatus.Stopped)
        {
            ServiceChanged?.Invoke(service, ServiceTransition.Stopped);
        }
    }

    private void ApplyDatabase(ServiceModel row, DatabaseSample sample, bool silent)
    {
        var describe = Settings?.Database.Describe() ?? "";
        if (sample.Version is { } version)
        {
            var text = $"PostgreSQL {version.Major}.{version.Minor}";
            row.Detail = text;
            if (row.Status != ServerStatus.Running)
            {
                row.Status = ServerStatus.Running;
                ServiceChanged?.Invoke(row, silent ? ServiceTransition.Refreshed : ServiceTransition.Started);
            }

            if (_lastDatabaseState != text)
            {
                _lastDatabaseState = text;
                LogHub.Write(row.Name, LogLevel.Info, $"{text} is reachable ({describe})");
            }

            return;
        }

        var wasUp = row.Status == ServerStatus.Running;
        row.Status = wasUp || row.Status == ServerStatus.Crashed ? ServerStatus.Crashed : ServerStatus.Stopped;
        row.Detail = "Not reachable";
        if (wasUp)
        {
            ServiceChanged?.Invoke(row, ServiceTransition.Crashed);
        }

        if (_lastDatabaseState != sample.Problem)
        {
            _lastDatabaseState = sample.Problem;
            LogHub.Write(row.Name, LogLevel.Error, $"PostgreSQL is not reachable: {sample.Problem}");
        }
    }

    private sealed class StartupObserver : IStartupObserver
    {
        private readonly PanelHost _host;

        public StartupObserver(PanelHost host)
        {
            _host = host;
        }

        public ServiceModel? Current { get; private set; }

        public void CheckingDatabase(string database) => LogHub.Post(ServiceModel.DatabaseName, LogLevel.Info, $"Checking the database ({database})...");

        public void UpdatingTables() => LogHub.Post(ServiceModel.DatabaseName, LogLevel.Info, "Updating the database tables...");

        public void StartingServers() => LogHub.Post(LogHub.PanelSource, LogLevel.Info, "Starting the servers, one by one.");

        public void RollingBack() => LogHub.Post(LogHub.PanelSource, LogLevel.Warn, "Stopping the servers that did start...");

        public void Report(ServerStartReport value)
        {
            var services = _host._services;
            if (services.FirstOrDefault(service => service.Server == value.Server) is not { } service)
            {
                return;
            }

            Current = value.Stage == ServerStartStage.Starting ? service : null;
            Dispatcher.UIThread.Post(() => Show(service, value));
        }

        private void Show(ServiceModel service, ServerStartReport value)
        {
            switch (value.Stage)
            {
                case ServerStartStage.Starting:
                    service.Status = ServerStatus.Starting;
                    service.Detail = null;
                    LogHub.Write(service.Name, LogLevel.Info, $"Starting the {value.Server.Title.ToLowerInvariant()} on port {value.Port}");
                    _host.ServiceChanged?.Invoke(service, ServiceTransition.Refreshed);
                    break;
                case ServerStartStage.Up:
                    service.Status = ServerStatus.Running;
                    LogHub.Write(service.Name, LogLevel.Info, $"{value.Server.Title} is up (pid {value.ProcessId})");
                    _host.ServiceChanged?.Invoke(service, ServiceTransition.Started);
                    break;
                case ServerStartStage.AlreadyRunning:
                    LogHub.Write(service.Name, LogLevel.Info, $"{value.Server.Title} is already running (pid {value.ProcessId})");
                    break;
            }
        }
    }
}
