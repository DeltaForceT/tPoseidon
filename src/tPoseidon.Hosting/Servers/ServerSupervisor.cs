using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace tPoseidon.Hosting.Servers;

public sealed record ServerState(ServerDefinition Server, int? ProcessId, DateTime? StartedUtc, bool Listening)
{
    public bool Running => ProcessId is not null;
}

public enum ServerStartStage
{
    AlreadyRunning,
    Starting,
    Up,
}

public sealed record ServerStartReport(ServerDefinition Server, ServerStartStage Stage, int Port, int? ProcessId);

public sealed class ServerSupervisor
{
    private static readonly TimeSpan StartTimeTolerance = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(30);
    private const int LogTailLines = 20;

    private readonly InstallPaths _paths;
    private readonly HostSettings _settings;

    public ServerSupervisor(InstallPaths paths, HostSettings settings)
    {
        _paths = paths;
        _settings = settings;
    }

    public ServerState State(ServerDefinition server)
    {
        ArgumentNullException.ThrowIfNull(server);
        using var process = RunningProcess(server, out var startedUtc);
        var processId = process?.Id;
        return new ServerState(server, processId, startedUtc, processId is not null && PortProbe.IsListening(server.Port(_settings.Ports), _settings.Network.BindAddress));
    }

    public bool AnyRunning() => ServerCatalog.All.Any(server => State(server).Running);

    public async Task StartAsync(ServerDefinition server, IProgress<ServerStartReport>? progress, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(server);
        var port = server.Port(_settings.Ports);
        using (var existing = RunningProcess(server, out _))
        {
            if (existing is not null)
            {
                progress?.Report(new ServerStartReport(server, ServerStartStage.AlreadyRunning, port, existing.Id));
                return;
            }
        }

        if (PortProbe.IsListening(port, _settings.Network.BindAddress))
        {
            throw new HostingException($"Port {port} for the {server.Title.ToLowerInvariant()} is already in use by another program.");
        }

        var executable = _paths.AppExecutable(server.Assembly);
        if (!File.Exists(executable))
        {
            throw new HostingException($"{executable} is missing. Unpack the full release package again.");
        }

        Directory.CreateDirectory(_paths.Run);
        Directory.CreateDirectory(_paths.Logs);
        Directory.CreateDirectory(_paths.CrashDumps);

        progress?.Report(new ServerStartReport(server, ServerStartStage.Starting, port, null));
        using var process = DetachedLauncher.Start(
            executable,
            ServerEnvironment.Arguments(server, _settings),
            ServerEnvironment.For(server, _settings, _paths),
            _paths.App);
        WritePidFile(server, process);

        var deadline = DateTime.UtcNow + server.ReadyTimeout;
        while (!PortProbe.IsListening(port, _settings.Network.BindAddress))
        {
            if (process.HasExited)
            {
                File.Delete(_paths.PidFile(server.Name));
                throw new HostingException(
                    $"The {server.Title.ToLowerInvariant()} stopped while starting (exit {process.ExitCode}).",
                    LogTail(server),
                    null);
            }

            if (DateTime.UtcNow > deadline)
            {
                throw new HostingException(
                    $"The {server.Title.ToLowerInvariant()} did not open port {port} within {server.ReadyTimeout.TotalSeconds:0} seconds.",
                    LogTail(server),
                    null);
            }

            await Task.Delay(PollInterval, ct).ConfigureAwait(false);
        }

        progress?.Report(new ServerStartReport(server, ServerStartStage.Up, port, process.Id));
    }

    public bool Stop(ServerDefinition server)
    {
        ArgumentNullException.ThrowIfNull(server);
        using var process = RunningProcess(server, out _);
        var pidFile = _paths.PidFile(server.Name);
        if (File.Exists(pidFile))
        {
            File.Delete(pidFile);
        }

        if (process is null)
        {
            return false;
        }

        process.Kill(entireProcessTree: true);
        process.WaitForExit(StopTimeout);
        return true;
    }

    public string LogTail(ServerDefinition server)
    {
        ArgumentNullException.ThrowIfNull(server);
        var log = _paths.LogFile(server.Name);
        if (!File.Exists(log))
        {
            return $"No log was written to {log}.";
        }

        using var stream = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        var lines = new Queue<string>();
        while (reader.ReadLine() is { } line)
        {
            lines.Enqueue(line);
            if (lines.Count > LogTailLines)
            {
                lines.Dequeue();
            }
        }

        return $"Last lines of {log}:{Environment.NewLine}{string.Join(Environment.NewLine, lines)}";
    }

    private Process? RunningProcess(ServerDefinition server, out DateTime? startedUtc)
    {
        startedUtc = null;
        var pidFile = _paths.PidFile(server.Name);
        if (!File.Exists(pidFile))
        {
            return null;
        }

        var lines = File.ReadAllLines(pidFile);
        if (lines.Length < 2
            || !int.TryParse(lines[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid)
            || !long.TryParse(lines[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var startTicks))
        {
            File.Delete(pidFile);
            return null;
        }

        Process process;
        try
        {
            process = Process.GetProcessById(pid);
        }
        catch (ArgumentException)
        {
            File.Delete(pidFile);
            return null;
        }

        DateTime started;
        try
        {
            started = process.StartTime.ToUniversalTime();
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            process.Dispose();
            File.Delete(pidFile);
            return null;
        }

        if (process.HasExited || Math.Abs(started.Ticks - startTicks) > StartTimeTolerance.Ticks)
        {
            process.Dispose();
            File.Delete(pidFile);
            return null;
        }

        startedUtc = started;
        return process;
    }

    private void WritePidFile(ServerDefinition server, Process process)
    {
        var started = process.StartTime.ToUniversalTime().Ticks;
        File.WriteAllLines(_paths.PidFile(server.Name), new[]
        {
            process.Id.ToString(CultureInfo.InvariantCulture),
            started.ToString(CultureInfo.InvariantCulture),
        });
    }
}
