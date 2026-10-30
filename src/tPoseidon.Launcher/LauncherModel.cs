using System.Globalization;
using tPoseidon.Core.Common.Launcher;
using tPoseidon.Hosting;
using tPoseidon.LauncherKit;
using tPoseidon.Ui;

namespace tPoseidon.Launcher;

public sealed record LanguageChoice(string Service, string Label);

public enum ChannelStatus
{
    Unknown,
    Checking,
    Online,
    Maintenance,
    Offline,
}

public enum LauncherUpdateState
{
    None,
    Downloading,
    Ready,
    Failed,
}

public enum SessionState
{
    SignedOut,
    Working,
    UpdateFailed,
    Ready,
}

public sealed class LauncherModel
{
    public static readonly IReadOnlyList<LanguageChoice> Languages =
    [
        new(ClientFolder.AutomaticService, "Automatic (from the game data)"),
        new(ClientFolder.KoreanService, "Korean"),
        new(ClientFolder.EnglishService, "English"),
    ];

    private readonly LauncherSettings _settings;
    private readonly string _settingsPath;
    private readonly ITicketService _tickets;
    private static readonly int ShopPort = new PortSettings().Shop;

    private readonly IClientHost _client;
    private readonly ClientUpdater _updater;
    private readonly Branding? _branding;
    private readonly IClientSetupStore? _setupStore;
    private readonly ManifestTrust _trust;
    private Task _lastRefresh = Task.CompletedTask;
    private readonly LauncherSelfUpdater? _selfUpdater;
    private LauncherStage? _pendingLauncher;
    private string? _password;

    public LauncherModel(LauncherSettings settings, string settingsPath, ITicketService tickets, IClientHost client, ClientUpdater updater, Branding? branding = null, IClientSetupStore? setupStore = null, LauncherSelfUpdater? selfUpdater = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _settingsPath = settingsPath ?? throw new ArgumentNullException(nameof(settingsPath));
        _tickets = tickets ?? throw new ArgumentNullException(nameof(tickets));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _updater = updater ?? throw new ArgumentNullException(nameof(updater));
        _branding = branding;
        _setupStore = setupStore;
        _trust = new ManifestTrust(branding?.Spec.PublicKey);
        _selfUpdater = selfUpdater;
        if (selfUpdater?.TakeUnseenFailure() is { } failure)
        {
            Notice = new LauncherNotice(BannerKind.Warning, $"The launcher was not updated to version {failure.Version}", failure.Reason + " You can keep playing with this version.");
        }

        if (_branding is not null)
        {
            _settings.Language = _branding.Spec.Language;
        }

        if (!Languages.Any(choice => choice.Service == _settings.Language))
        {
            _settings.Language = ClientFolder.AutomaticService;
        }

        ClientCheck = CheckClient(_settings.ClientFolder);
        if (_branding is null && SelectedServer is null && _settings.Servers.Count > 0)
        {
            _settings.SelectedServer = _settings.Servers[0].Address;
        }
    }

    public event Action? Changed;

    public string ClientFolderPath => _settings.ClientFolder;

    public ClientCheck ClientCheck { get; private set; }

    public bool Locked => _branding is not null;

    public Branding? Branding => _branding;

    public bool OptionsAvailable => _setupStore is not null;

    public SessionState Session { get; private set; }

    public IReadOnlyList<ServerFile> Servers => _branding is null ? _settings.Servers : [_branding.Server];

    public ServerFile? SelectedServer => _branding is null
        ? _settings.Servers.FirstOrDefault(server => SameAddress(server.Address, _settings.SelectedServer))
        : _branding.Server;

    public string Account => _settings.Account;

    public string Language => _settings.Language;

    public bool Busy { get; private set; }

    public ChannelStatus Status { get; private set; }

    public string StatusDetail { get; private set; } = "";

    public LauncherManifest? Manifest { get; private set; }

    public ChannelTrust Trust { get; private set; }

    public LauncherUpdateState LauncherUpdate { get; private set; }

    public string? LauncherUpdateVersion => _pendingLauncher?.Version;

    public bool CanGoBackToEarlierLauncher => _selfUpdater?.CanGoBack == true;

    public byte[]? BannerImage { get; private set; }

    public IReadOnlyDictionary<string, byte[]> NewsPictures { get; private set; } = new Dictionary<string, byte[]>();

    public UpdatePlan Plan { get; private set; } = UpdatePlan.Empty;

    public double ProgressFraction { get; private set; }

    public string ProgressText { get; private set; } = "";

    public string DisplayName => _branding?.Spec.ServerName ?? Manifest?.Name ?? SelectedServer?.Name ?? "tPoseidon";

    public string? AccentColor => _branding?.Spec.AccentColor ?? Manifest?.Color ?? SelectedServer?.Color;

    public string? Note => SelectedServer?.Note;

    public LauncherNotice? Notice { get; private set; }

    public bool NeedsSetup => !ClientCheck.Ok || Servers.Count == 0;

    public void ReportProblem(string problem)
    {
        Notice = new LauncherNotice(BannerKind.Warning, problem, null);
        Changed?.Invoke();
    }

    public void SetClientFolder(string folder)
    {
        var trimmed = (folder ?? "").Trim().Trim('"');
        _settings.ClientFolder = Normalized(trimmed);
        ClientCheck = CheckClient(_settings.ClientFolder);
        ForgetSession();
        Notice = null;
        Save();
        Changed?.Invoke();
    }

    public bool AddServer(string name, string address, string? webUrl, string? note)
    {
        if (RefuseWhenLocked())
        {
            return false;
        }

        ServerFile server;
        try
        {
            var trimmedAddress = (address ?? "").Trim();
            server = new ServerFile(string.IsNullOrWhiteSpace(name) ? trimmedAddress : name, trimmedAddress, webUrl, note).Validated();
        }
        catch (HostingException ex)
        {
            Notice = LauncherNotice.From(ex);
            Changed?.Invoke();
            return false;
        }

        Store(server);
        return true;
    }

    public bool ImportServerFile(string path)
    {
        if (RefuseWhenLocked())
        {
            return false;
        }

        ServerFile server;
        try
        {
            server = ServerFile.Read(path);
        }
        catch (HostingException ex)
        {
            Notice = LauncherNotice.From(ex);
            Changed?.Invoke();
            return false;
        }

        Store(server);
        return true;
    }

    public void SelectServer(string address)
    {
        if (_branding is not null || !_settings.Servers.Any(server => SameAddress(server.Address, address)))
        {
            return;
        }

        _settings.SelectedServer = address;
        ForgetSession();
        Notice = null;
        Save();
        Changed?.Invoke();
    }

    public void RemoveSelectedServer()
    {
        if (_branding is not null || SelectedServer is not { } selected)
        {
            return;
        }

        _settings.Servers.Remove(selected);
        _settings.SelectedServer = _settings.Servers.Count > 0 ? _settings.Servers[0].Address : "";
        ForgetSession();
        Notice = null;
        Save();
        Changed?.Invoke();
    }

    public void SetAccount(string account)
    {
        _settings.Account = (account ?? "").Trim();
        Save();
    }

    public void SetLanguage(string service)
    {
        if (_branding is null && Languages.Any(choice => choice.Service == service))
        {
            _settings.Language = service;
            Save();
        }
    }

    public Task RefreshChannelAsync(CancellationToken ct)
    {
        var next = RefreshAfterAsync(_lastRefresh, ct);
        _lastRefresh = next;
        return next;
    }

    private async Task RefreshAfterAsync(Task previous, CancellationToken ct)
    {
        await Task.WhenAny(previous).ConfigureAwait(true);
        await RefreshChannelCoreAsync(ct).ConfigureAwait(true);
    }

    private async Task RefreshChannelCoreAsync(CancellationToken ct)
    {
        if (SelectedServer is not { } server)
        {
            ClearChannel(ChannelStatus.Unknown, "");
            Changed?.Invoke();
            return;
        }

        ClearChannel(ChannelStatus.Checking, "");
        Changed?.Invoke();
        try
        {
            var fetched = await FetchManifestAsync(server, ct).ConfigureAwait(true);
            if (!ReferenceEquals(SelectedServer, server))
            {
                return;
            }

            if (fetched is not { } channel)
            {
                ClearChannel(ChannelStatus.Online, "");
            }
            else
            {
                var (manifest, trust) = channel;
                var banner = manifest.Banner is null ? null : await _updater.Source.GetImageAsync(server.WebAddress(ShopPort, UpdateChannelPaths.Banner), ct).ConfigureAwait(true);
                var pictures = await LoadNewsPicturesAsync(server, manifest, ct).ConfigureAwait(true);
                var plan = ClientCheck.Ok ? await Task.Run(() => ClientUpdater.Plan(_settings.ClientFolder, manifest), ct).ConfigureAwait(true) : UpdatePlan.Empty;
                if (!ReferenceEquals(SelectedServer, server))
                {
                    return;
                }

                ClearChannel(string.IsNullOrWhiteSpace(manifest.Maintenance) ? ChannelStatus.Online : ChannelStatus.Maintenance, manifest.Maintenance ?? "");
                Manifest = manifest;
                Trust = trust;
                BannerImage = banner;
                NewsPictures = pictures;
                Plan = plan;
                if (manifest.Launcher is { } release && _trust.Required && trust == ChannelTrust.Verified)
                {
                    await PrepareLauncherUpdateAsync(server, release, ct).ConfigureAwait(true);
                }
            }
        }
        catch (PlayFailure failure)
        {
            ClearChannel(failure.Reason is PlayFailureReason.UpdateFailed or PlayFailureReason.UpdatesNotTrusted ? ChannelStatus.Online : ChannelStatus.Offline, failure.Summary);
            Notice = LauncherNotice.From(failure);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ClearChannel(ChannelStatus.Online, "");
            Notice = LauncherNotice.Error("Your client files could not be checked", ex.Message);
        }
        catch (OperationCanceledException)
        {
            ClearChannel(ChannelStatus.Unknown, "");
        }

        Changed?.Invoke();
    }

    public bool UndoLastUpdate()
    {
        if (!ClientCheck.Ok)
        {
            Notice = ClientNotice();
            Changed?.Invoke();
            return false;
        }

        try
        {
            var undone = ClientUpdater.Undo(_settings.ClientFolder);
            Notice = undone
                ? new LauncherNotice(BannerKind.Success, "The last update was undone", "Your client files are back as they were before it.")
                : new LauncherNotice(BannerKind.Info, "There is no update to undo", null);
            return undone;
        }
        catch (Exception ex) when (ex is HostingException or IOException or UnauthorizedAccessException)
        {
            Notice = LauncherNotice.Error("The update could not be undone", ex.Message);
            return false;
        }
        finally
        {
            Changed?.Invoke();
        }
    }

    public async Task SignInAsync(string account, string password, CancellationToken ct)
    {
        if (Busy || Session is SessionState.Ready or SessionState.UpdateFailed)
        {
            return;
        }

        if (LauncherUpdate == LauncherUpdateState.Ready)
        {
            Notice = new LauncherNotice(BannerKind.Info, "Restart the launcher first", "A new launcher version is ready. Press Restart to install it, then log in.");
            Changed?.Invoke();
            return;
        }

        SetAccount(account);
        if (Problem(password) is { } problem)
        {
            Notice = problem;
            Changed?.Invoke();
            return;
        }

        var server = SelectedServer!;
        if (ClientIsRunning())
        {
            Notice = LauncherNotice.From(new PlayFailure(PlayFailureReason.ClientRunning, "The game client is already running."));
            Changed?.Invoke();
            return;
        }

        if (Status == ChannelStatus.Maintenance)
        {
            Notice = new LauncherNotice(BannerKind.Warning, "The server is in maintenance", StatusDetail);
            Changed?.Invoke();
            return;
        }

        Begin("Logging in...");
        try
        {
            await _tickets.RequestAsync(server.TicketEndpoint(ShopPort), _settings.Account, password, ct).ConfigureAwait(true);
            _password = password;
            await UpdateAsync(server, ct).ConfigureAwait(true);
        }
        catch (HostingException ex)
        {
            Notice = LauncherNotice.From(ex);
            ForgetSession();
        }
        catch (OperationCanceledException)
        {
            Notice = new LauncherNotice(BannerKind.Warning, "Cancelled", null);
            ForgetSession();
        }
        finally
        {
            End();
        }

        Changed?.Invoke();
    }

    public async Task RetryUpdateAsync(CancellationToken ct)
    {
        if (Busy || Session != SessionState.UpdateFailed || SelectedServer is not { } server)
        {
            return;
        }

        Begin("Checking for updates...");
        try
        {
            await UpdateAsync(server, ct).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            Notice = new LauncherNotice(BannerKind.Warning, "Cancelled", null);
            Session = SessionState.UpdateFailed;
        }
        finally
        {
            End();
        }

        Changed?.Invoke();
    }

    public async Task PlayAsync(CancellationToken ct)
    {
        if (Busy || Session != SessionState.Ready || _password is null || SelectedServer is not { } server)
        {
            return;
        }

        if (ClientIsRunning())
        {
            Notice = LauncherNotice.From(new PlayFailure(PlayFailureReason.ClientRunning, "The game client is already running."));
            Changed?.Invoke();
            return;
        }

        Begin("Logging in...");
        var started = false;
        try
        {
            await _tickets.RequestAsync(server.TicketEndpoint(ShopPort), _settings.Account, _password, ct).ConfigureAwait(true);
            var target = _client.Start(Launch(server));
            Notice = new LauncherNotice(BannerKind.Success, "Started", $"The game client is starting and connecting to {target}.");
            started = true;
        }
        catch (PlayFailure failure) when (failure.Reason is PlayFailureReason.WrongPassword or PlayFailureReason.TooManyAttempts)
        {
            Notice = LauncherNotice.From(failure);
            ForgetSession();
        }
        catch (HostingException ex)
        {
            Notice = LauncherNotice.From(ex);
            Session = SessionState.Ready;
        }
        catch (OperationCanceledException)
        {
            Notice = new LauncherNotice(BannerKind.Warning, "Cancelled", null);
            Session = SessionState.Ready;
        }
        finally
        {
            if (started)
            {
                ForgetSession();
            }

            End();
        }

        Changed?.Invoke();
    }

    public void SignOut()
    {
        ForgetSession();
        Notice = null;
        Changed?.Invoke();
    }

    public ClientSetup LoadClientSetup()
    {
        var store = _setupStore ?? throw new InvalidOperationException("The client settings are not available on this computer.");
        try
        {
            return store.Load(SetupProfile());
        }
        catch (HostingException ex)
        {
            Notice = LauncherNotice.From(ex);
            Changed?.Invoke();
            return ClientSetup.Defaults;
        }
    }

    public bool SaveClientSetup(ClientSetup setup)
    {
        var store = _setupStore ?? throw new InvalidOperationException("The client settings are not available on this computer.");
        if (ClientIsRunning())
        {
            Notice = new LauncherNotice(BannerKind.Warning, "Close the game client first", "The client only reads its settings when it starts.");
            Changed?.Invoke();
            return false;
        }

        try
        {
            store.Save(SetupProfile(), setup);
            Notice = new LauncherNotice(BannerKind.Success, "Options saved", "They apply the next time the game starts.");
            return true;
        }
        catch (HostingException ex)
        {
            Notice = LauncherNotice.From(ex);
            return false;
        }
        finally
        {
            Changed?.Invoke();
        }
    }

    public string SetupProfileName() =>
        ClientSetupProfile.NameFor(ClientCheck.Ok ? ClientFolder.ServiceFor(_settings.ClientFolder, _settings.Language) : _settings.Language);

    public ClientSetupProfile SetupProfile()
    {
        if (!ClientCheck.Ok)
        {
            throw new HostingException("Pick your game client folder first.", "The client settings belong to the client, so the launcher needs to know where it is.", null);
        }

        byte[] executable;
        try
        {
            executable = File.ReadAllBytes(Path.Combine(_settings.ClientFolder, ClientCheck.Executable));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new HostingException($"The client could not be read: {ex.Message}", "Close the game client, then try again.", ex);
        }

        var company = ClientSetupProfile.CompanyIn(executable)
            ?? throw new HostingException("This client does not say where it keeps its settings.", "Use the KR 2014 client.", null);
        return new ClientSetupProfile(company, SetupProfileName());
    }

    public LauncherNotice? Problem(string password)
    {
        if (!ClientCheck.Ok)
        {
            return ClientNotice();
        }

        if (SelectedServer is null)
        {
            return LauncherNotice.Error("Pick a server", "Add a server by its address, or import the server file the owner gave you.");
        }

        if (_settings.Account.Length == 0)
        {
            return LauncherNotice.Error("Enter your account name");
        }

        return string.IsNullOrEmpty(password) ? LauncherNotice.Error("Enter your password") : null;
    }

    public LauncherNotice ClientNotice() =>
        _settings.ClientFolder.Length == 0
            ? LauncherNotice.Error("Pick your game client folder", "Choose the folder that holds your KR 2014 client (the one with Trickster.bin and a data folder).")
            : LauncherNotice.Error("This client folder is not usable", string.Join("; ", ClientCheck.Problems) + ".");

    private static string Megabytes(long bytes) =>
        (bytes / 1048576.0).ToString("0.#", CultureInfo.InvariantCulture) + " MB";

    private async Task UpdateAsync(ServerFile server, CancellationToken ct)
    {
        ProgressText = "Checking for updates...";
        Changed?.Invoke();
        try
        {
            var fetched = await FetchManifestAsync(server, ct).ConfigureAwait(true);
            if (fetched is { } channel)
            {
                var (manifest, trust) = channel;
                Trust = trust;
                if (!string.IsNullOrWhiteSpace(manifest.Maintenance))
                {
                    Status = ChannelStatus.Maintenance;
                    StatusDetail = manifest.Maintenance;
                    Notice = new LauncherNotice(BannerKind.Warning, "The server is in maintenance", manifest.Maintenance);
                    ForgetSession();
                    return;
                }

                Manifest = manifest;
                await InstallAsync(server, manifest, ct).ConfigureAwait(true);
            }

            Session = SessionState.Ready;
            Notice = null;
        }
        catch (HostingException ex)
        {
            Notice = LauncherNotice.From(ex);
            Session = SessionState.UpdateFailed;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Notice = LauncherNotice.Error("Your client files could not be checked", ex.Message);
            Session = SessionState.UpdateFailed;
        }
    }

    public bool RestartForLauncherUpdate()
    {
        if (LauncherUpdate != LauncherUpdateState.Ready || _selfUpdater is null || _pendingLauncher is null)
        {
            return false;
        }

        try
        {
            _selfUpdater.Restart(_pendingLauncher);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LauncherUpdate = LauncherUpdateState.Failed;
            Notice = LauncherNotice.Error("The launcher could not restart to update", ex.Message);
            Changed?.Invoke();
            return false;
        }
    }

    public bool GoBackToEarlierLauncher()
    {
        if (_selfUpdater is null || !_selfUpdater.CanGoBack)
        {
            return false;
        }

        try
        {
            _selfUpdater.GoBack();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Notice = LauncherNotice.Error("The launcher could not go back to the earlier version", ex.Message);
            Changed?.Invoke();
            return false;
        }
    }

    private async Task PrepareLauncherUpdateAsync(ServerFile server, LauncherRelease release, CancellationToken ct)
    {
        if (_selfUpdater is null || !_selfUpdater.Wants(release))
        {
            LauncherUpdate = LauncherUpdateState.None;
            _pendingLauncher = null;
            return;
        }

        Busy = true;
        LauncherUpdate = LauncherUpdateState.Downloading;
        ProgressFraction = 0;
        ProgressText = "Downloading the new launcher...";
        Changed?.Invoke();
        try
        {
            var progress = new Progress<UpdateProgress>(update =>
            {
                ProgressFraction = update.TotalBytes == 0 ? 1 : (double)update.DoneBytes / update.TotalBytes;
                ProgressText = $"Downloading the new launcher {update.FileNumber}/{update.FileCount}: {update.Current}";
                Changed?.Invoke();
            });
            _pendingLauncher = await _selfUpdater.StageAsync(release, path => server.WebAddress(ShopPort, UpdateChannelPaths.LauncherFile(path)), progress, ct).ConfigureAwait(true);
            LauncherUpdate = _pendingLauncher is null ? LauncherUpdateState.None : LauncherUpdateState.Ready;
            if (_pendingLauncher is not null)
            {
                Notice = new LauncherNotice(BannerKind.Info, "A new launcher version is ready", "Press Restart to update. The launcher closes, installs it and opens again.");
            }
        }
        catch (HostingException ex)
        {
            LauncherUpdate = LauncherUpdateState.Failed;
            Notice = new LauncherNotice(BannerKind.Warning, "The launcher could not be updated", $"{ex.Summary} You can keep playing with this version.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LauncherUpdate = LauncherUpdateState.Failed;
            Notice = new LauncherNotice(BannerKind.Warning, "The launcher could not be updated", $"{ex.Message} You can keep playing with this version.");
        }
        finally
        {
            Busy = false;
            ProgressFraction = 0;
            ProgressText = "";
        }
    }

    private async Task<(LauncherManifest Manifest, ChannelTrust Trust)?> FetchManifestAsync(ServerFile server, CancellationToken ct)
    {
        var fetched = await _updater.Source.GetManifestAsync(server.WebAddress(ShopPort, UpdateChannelPaths.Manifest), server.WebAddress(ShopPort, UpdateChannelPaths.Signature), ct).ConfigureAwait(true);
        return fetched is null ? null : (fetched.Manifest, _trust.Check(fetched));
    }

    private async Task InstallAsync(ServerFile server, LauncherManifest manifest, CancellationToken ct)
    {
        var folder = _settings.ClientFolder;
        var plan = await Task.Run(() => ClientUpdater.Plan(folder, manifest), ct).ConfigureAwait(true);
        Plan = plan;
        if (!plan.IsEmpty)
        {
            var progress = new Progress<UpdateProgress>(update =>
            {
                ProgressFraction = update.TotalBytes == 0 ? 1 : (double)update.DoneBytes / update.TotalBytes;
                ProgressText = $"Updating {update.FileNumber}/{update.FileCount}: {update.Current} ({Megabytes(update.DoneBytes)} of {Megabytes(update.TotalBytes)})";
                Changed?.Invoke();
            });
            await _updater.ApplyAsync(folder, plan, path => server.WebAddress(ShopPort, UpdateChannelPaths.File(path)), progress, ct).ConfigureAwait(true);
        }

        ProgressText = "Verifying your files...";
        Changed?.Invoke();
        var remaining = await Task.Run(() => ClientUpdater.Plan(folder, manifest), ct).ConfigureAwait(true);
        if (!remaining.IsEmpty)
        {
            throw new PlayFailure(PlayFailureReason.UpdateFailed, $"{remaining.Changes.Count} files did not match the server's list after the update.", "Press Retry. If it keeps failing, tell the server owner.", null);
        }

        Plan = UpdatePlan.Empty;
        ProgressFraction = 1;
    }

    private void Begin(string text)
    {
        Busy = true;
        Session = SessionState.Working;
        Notice = null;
        ProgressFraction = 0;
        ProgressText = text;
        Changed?.Invoke();
    }

    private void End()
    {
        Busy = false;
        ProgressText = "";
        if (Session == SessionState.Working)
        {
            Session = SessionState.SignedOut;
        }
    }

    private void ForgetSession()
    {
        _password = null;
        Session = SessionState.SignedOut;
    }

    private bool ClientIsRunning() =>
        ClientCheck.Ok && _client.IsRunning(Path.Combine(_settings.ClientFolder, ClientCheck.Executable));

    private bool RefuseWhenLocked()
    {
        if (_branding is null)
        {
            return false;
        }

        Notice = LauncherNotice.Error($"This launcher is for {_branding.Spec.ServerName} only");
        Changed?.Invoke();
        return true;
    }

    private async Task<IReadOnlyDictionary<string, byte[]>> LoadNewsPicturesAsync(ServerFile server, LauncherManifest manifest, CancellationToken ct)
    {
        var pictures = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var name in manifest.News.Select(item => item.Picture).OfType<string>().Distinct(StringComparer.Ordinal))
        {
            if (await _updater.Source.GetImageAsync(server.WebAddress(ShopPort, UpdateChannelPaths.NewsPicture(name)), ct).ConfigureAwait(true) is { } bytes)
            {
                pictures[name] = bytes;
            }
        }

        return pictures;
    }

    private void ClearChannel(ChannelStatus status, string detail)
    {
        Status = status;
        StatusDetail = detail;
        Manifest = null;
        Trust = ChannelTrust.Unknown;
        BannerImage = null;
        NewsPictures = new Dictionary<string, byte[]>();
        Plan = UpdatePlan.Empty;
        ProgressFraction = 0;
        ProgressText = "";
    }

    private static ClientCheck CheckClient(string folder) =>
        folder.Length == 0 ? new ClientCheck(false, "", []) : ClientFolder.Check(folder);

    private static bool SameAddress(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private HostSettings Launch(ServerFile server)
    {
        var settings = new HostSettings { ClientFolder = _settings.ClientFolder };
        settings.Network.PublicAddress = server.Address;
        settings.Launch.Account = _settings.Account;
        settings.Launch.ServiceName = _settings.Language;
        return settings;
    }

    private void Store(ServerFile server)
    {
        var existing = _settings.Servers.FindIndex(known => SameAddress(known.Address, server.Address));
        if (existing >= 0)
        {
            _settings.Servers[existing] = server;
        }
        else
        {
            _settings.Servers.Add(server);
        }

        _settings.SelectedServer = server.Address;
        Notice = new LauncherNotice(BannerKind.Success, $"Added {server.Name}", server.Note);
        Save();
        Changed?.Invoke();
    }

    private static string Normalized(string folder)
    {
        if (folder.Length == 0)
        {
            return "";
        }

        try
        {
            return Path.GetFullPath(folder);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return folder;
        }
    }

    private void Save()
    {
        try
        {
            _settings.Save(_settingsPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Notice = LauncherNotice.Error("Your launcher settings could not be saved", ex.Message);
        }
    }
}
