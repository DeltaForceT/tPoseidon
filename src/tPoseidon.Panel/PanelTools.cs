using Avalonia.Controls;
using Avalonia.Platform.Storage;
using tPoseidon.Hosting;
using tPoseidon.Hosting.Servers;
using tPoseidon.LauncherKit;

namespace tPoseidon.Panel;

public sealed class PanelTools
{
    public const string ServerFileKey = "server-file";
    public const string GuildCoinsKey = "guild-coins";
    public const string CharacterSlotsKey = "character-slots";
    public const string LauncherKey = "launcher";
    public const string LauncherChannelKey = "launcher-channel";

    private readonly Dictionary<string, Window> _open = [];
    private readonly Window _main;
    private readonly Action _openSetup;
    private readonly (string Service, MenuItem Item)[] _languages;

    public PanelTools(PanelHost host, Window main, Action openSetup)
    {
        Host = host ?? throw new ArgumentNullException(nameof(host));
        _main = main ?? throw new ArgumentNullException(nameof(main));
        _openSetup = openSetup ?? throw new ArgumentNullException(nameof(openSetup));
        _languages = BuildLanguages();
    }

    public PanelHost Host { get; }

    public IReadOnlyList<(string Service, MenuItem Item)> Languages => _languages;

    public IReadOnlyCollection<Window> Windows => _open.Values;

    public Window? Find(string key) => _open.GetValueOrDefault(key);

    public async Task StartAllAsync()
    {
        if (!Host.IsSetUp)
        {
            _openSetup();
            return;
        }

        if (Host.Settings is { } settings && ServerStartup.NeedsBackupBeforeStart(settings) && !Host.AnyServerRunning())
        {
            var answer = await AskWindow.ShowAsync(
                _main.IsVisible ? _main : null,
                "Back up the database first?",
                $"This database was last used by tPoseidon {settings.LastStartedVersion}; this is {ServerStartup.CurrentVersion}. A new version can change how data is stored, and the change can't be undone without a backup.",
                "Back up, then start",
                "Start without a backup").ConfigureAwait(true);
            if (answer is null)
            {
                return;
            }

            if (answer == true)
            {
                await Host.BackupAsync().ConfigureAwait(true);
            }
        }

        await Host.StartAllAsync().ConfigureAwait(true);
    }

    public static void OpenLogs() => LogWindow.Open(null);

    public void OpenGameMaster() => GmWindow.Open(Host.Paths);

    public void OpenSecurity() => SecurityWindow.Open(Host.Paths);

    public Window? EditGuildCoins() =>
        Host.Settings is null ? null : Present(GuildCoinsKey, () => new GuildCoinsWindow(Host));

    public Window? EditCharacterSlots() =>
        Host.Settings is null ? null : Present(CharacterSlotsKey, () => new CharacterSlotsWindow(Host));

    public Window? ShareServerFile() =>
        Host.Settings is { } settings ? Present(ServerFileKey, () => new ServerFileWindow(settings)) : null;

    public Window? MakeLauncher()
    {
        if (Host.Settings is not { } settings)
        {
            return null;
        }

        var kit = Path.Combine(AppContext.BaseDirectory, LauncherKitFiles.FolderName);
        return Present(LauncherKey, () => new LauncherWizardWindow(settings, kit, new HttpConnectionCheck(new HttpClient()), ServerStartup.CurrentVersion, server: new LauncherWizardServer(Host.Paths, Host.AnyServerRunning, Host.RestartAllAsync)));
    }

    public Window? EditLauncherChannel() =>
        Host.Settings is { } settings ? Present(LauncherChannelKey, () => new LauncherChannelWindow(Host.Paths, settings)) : null;

    public async Task OpenDataFolderAsync()
    {
        Directory.CreateDirectory(Host.Paths.Root);
        if (!await _main.Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(Host.Paths.Root)).ConfigureAwait(true))
        {
            Host.Notify(BannerKind.Warning, "The data folder could not be opened.", Host.Paths.Root);
        }
    }

    public void OpenLanguageMenu(Control source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var menu = new ContextMenu();
        foreach (var choice in BuildLanguages())
        {
            menu.Items.Add(choice.Item);
            Mark(choice);
        }

        menu.Open(source);
    }

    public void MarkLanguages()
    {
        foreach (var choice in _languages)
        {
            Mark(choice);
        }
    }

    private void Mark((string Service, MenuItem Item) choice) =>
        choice.Item.Icon = string.Equals(choice.Service, Host.ClientLanguage, StringComparison.OrdinalIgnoreCase) ? Tokens.Glyphs(Glyph.Check, 12, Tokens.Accent) : null;

    private (string Service, MenuItem Item)[] BuildLanguages()
    {
        var choices = new[]
        {
            (ClientFolder.AutomaticService, new MenuItem { Header = "Automatic (from the game data)" }),
            (ClientFolder.KoreanService, new MenuItem { Header = "Korean" }),
            (ClientFolder.EnglishService, new MenuItem { Header = "English" }),
        };
        foreach (var (service, item) in choices)
        {
            item.Click += async (_, _) => await Host.ChooseClientLanguageAsync(service).ConfigureAwait(true);
        }

        return choices;
    }

    private Window Present(string key, Func<Window> create)
    {
        if (_open.TryGetValue(key, out var existing))
        {
            Raise(existing);
            return existing;
        }

        var window = create();
        _open[key] = window;
        window.Closed += (_, _) => _open.Remove(key);
        window.Show();
        return window;
    }

    private static void Raise(Window window)
    {
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
    }
}
