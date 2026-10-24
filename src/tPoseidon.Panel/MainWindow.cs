using System.Diagnostics;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using tPoseidon.Hosting.Servers;

namespace tPoseidon.Panel;

public sealed class MainWindow : Window
{
    public const string ColumnLayout = "30,96,52,*,104,64,92";
    private static readonly TimeSpan ToastLength = TimeSpan.FromSeconds(7);

    private readonly Motion _motion = Motion.Shared;
    private readonly PanelHost _host;
    private readonly PanelPreferences _preferences;
    private readonly List<ServiceRow> _rows = [];
    private readonly StackPanel _rowStack = new();
    private readonly ResourceMeter _meter = new();
    private readonly TextBlock _summary = Tokens.Caption();
    private readonly TextBlock _readout = Tokens.Caption();
    private readonly TextBlock _statusLeft = Tokens.Caption();
    private readonly TextBlock[] _activity = Enumerable.Range(0, 4).Select(_ => new TextBlock { FontFamily = Tokens.MonoFont, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis }).ToArray();
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Banner _toast = new() { Margin = new Thickness(12), VerticalAlignment = VerticalAlignment.Bottom, MaxWidth = 560 };
    private readonly Button _client = Tokens.ToolbarButton(Glyph.Client, "Client", "Launch the game client");
    private readonly Button _playAccount = new() { Height = 26, MinHeight = 0, Padding = new Thickness(6, 0), Margin = new Thickness(-4, 0, 0, 0), VerticalContentAlignment = VerticalAlignment.Center, IsVisible = false };
    private readonly MenuItem _reduce = new() { Header = "Reduce animations", ToggleType = MenuItemToggleType.CheckBox };
    private IDisposable? _toastTimer;
    private bool _held;
    private bool _begun;
    private Image? _emblem;

    public MainWindow(PanelHost host, PanelPreferences preferences)
    {
        _host = host;
        _preferences = preferences;
        Title = "tPoseidon Control Panel";
        Width = SplashLayout.MainWidth;
        Height = SplashLayout.MainHeight;
        MinWidth = 640;
        MinHeight = 440;
        FontFamily = Tokens.UiFont;
        FontSize = Tokens.FontSizeBody;
        Foreground = Tokens.TextPrimary;
        Background = Tokens.Surface0;
        UseLayoutRounding = true;
        Icon = PanelAssets.WindowIcon();
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Opacity = 0;
        Transitions = [new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(320) }];

        var root = new Grid { RowDefinitions = new RowDefinitions($"{Tokens.HeaderHeight},Auto,*,Auto,{Tokens.StatusBarHeight}") };
        root.Children.Add(BuildHeader());
        var columns = BuildColumnHeader();
        Grid.SetRow(columns, 1);
        root.Children.Add(columns);
        var list = BuildList();
        Grid.SetRow(list, 2);
        root.Children.Add(list);
        var activity = BuildActivity();
        Grid.SetRow(activity, 3);
        root.Children.Add(activity);
        var status = BuildStatusBar();
        Grid.SetRow(status, 4);
        root.Children.Add(status);
        Content = root;

        _host.ServicesRebuilt += RebuildRows;
        _host.ServicesRebuilt += ShowPlayAccount;
        _host.PlayAccountChanged += ShowPlayAccount;
        _host.ServiceChanged += OnServiceChanged;
        _host.Notice += ShowToast;
        LogHub.Added += _ => RefreshActivity();

        Activated += (_, _) => _motion.SetFocused(true);
        Deactivated += (_, _) => _motion.SetFocused(false);
        _clock.Tick += (_, _) => OnClock();
        _clock.Start();
        Opened += (_, _) => OnOpened();
        RebuildRows();
        OnClock();
    }

    public event Action? FirstShown;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WindowStateProperty)
        {
            _motion.SetMinimized(WindowState == WindowState.Minimized);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.L && e.KeyModifiers == KeyModifiers.Control)
        {
            LogWindow.Toggle();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _clock.Stop();
        _host.End();
    }

    public void OpenSetup()
    {
        var window = new SetupWindow(_host.Paths);
        window.Completed += start =>
        {
            _host.Reload();
            if (start)
            {
                _ = StartAllAsync();
            }
        };
        window.Show(this);
    }

    private async Task StartAllAsync()
    {
        if (!_host.IsSetUp)
        {
            OpenSetup();
            return;
        }

        if (_host.Settings is { } settings && ServerStartup.NeedsBackupBeforeStart(settings) && !_host.AnyServerRunning())
        {
            var answer = await AskWindow.ShowAsync(
                this,
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
                await _host.BackupAsync().ConfigureAwait(true);
            }
        }

        await _host.StartAllAsync().ConfigureAwait(true);
    }

    private Border BuildHeader()
    {
        var header = new Avalonia.Controls.Panel();
        header.Children.Add(new SeaHost(new HeaderSeaHandler(), _motion));

        var startAll = Tokens.IconButton(Glyph.Play, "Start all servers, one by one", Tokens.Accent);
        startAll.Click += async (_, _) => await StartAllAsync().ConfigureAwait(true);
        var stopAll = Tokens.IconButton(Glyph.Stop, "Stop all servers. Postgres keeps running", Tokens.Danger);
        stopAll.Click += async (_, _) => await _host.StopAllAsync().ConfigureAwait(true);
        var restartAll = Tokens.IconButton(Glyph.Restart, "Stop and start all servers");
        restartAll.Click += async (_, _) => await _host.RestartAllAsync().ConfigureAwait(true);
        var logs = Tokens.ToolbarButton(Glyph.Log, "Logs", "Show or hide the log window (Ctrl+L)");
        logs.Click += (_, _) => LogWindow.Toggle();
        var security = Tokens.ToolbarButton(Glyph.Shield, "Security", "Blocked addresses, incidents and bans from the connection guard");
        security.Click += (_, _) => SecurityWindow.Toggle(_host.Paths);
        var gameMaster = Tokens.ToolbarButton(Glyph.Admin, "GM", "Game master tools: players, spawns, events, guilds and GM accounts");
        gameMaster.Click += (_, _) => GmWindow.Toggle(_host.Paths);
        _client.Click += (_, _) => _host.LaunchClient();
        _playAccount.Classes.Add("flat");
        ToolTip.SetTip(_playAccount, "The account Play logs in with. Click to pick another one.");
        _playAccount.Click += async (_, _) => await PickPlayAccountAsync().ConfigureAwait(true);
        ShowPlayAccount();

        var menu = Tokens.IconButton(Glyph.More, "Settings, setup and theme");
        _reduce.IsChecked = _motion.Reduced;
        _reduce.Click += (_, _) => SetReduced(_reduce.IsChecked);
        var setup = new MenuItem { Header = "Run first-time setup..." };
        setup.Click += (_, _) => OpenSetup();
        var backup = new MenuItem { Header = "Back up the database" };
        backup.Click += async (_, _) => await _host.BackupAsync().ConfigureAwait(true);
        var folder = new MenuItem { Header = "Open the data folder" };
        folder.Click += async (_, _) => await OpenDataFolderAsync().ConfigureAwait(true);
        var splash = new MenuItem { Header = "Replay startup splash" };
        splash.Click += (_, _) =>
        {
            var replay = new SplashWindow(_ => { });
            replay.Bind(this, false);
            replay.PlaceOverScreenCenter();
            replay.Show();
            replay.Aim();
        };
        var contextMenu = new ContextMenu
        {
            Items =
            {
                setup,
                backup,
                folder,
                new Separator(),
                _reduce,
                splash,
            },
        };
        contextMenu.Opening += (_, _) =>
        {
            backup.IsEnabled = _host.IsSetUp;
            splash.IsEnabled = !_motion.Reduced;
        };
        menu.ContextMenu = contextMenu;
        menu.Click += (_, _) => contextMenu.Open(menu);

        var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Spacing = 4 };
        right.Children.Add(startAll);
        right.Children.Add(stopAll);
        right.Children.Add(restartAll);
        right.Children.Add(new Border { Width = 1, Height = 16, Margin = new Thickness(4, 0), Background = Tokens.BorderSubtle });
        right.Children.Add(_client);
        right.Children.Add(_playAccount);
        right.Children.Add(logs);
        right.Children.Add(security);
        right.Children.Add(gameMaster);
        right.Children.Add(menu);
        DockPanel.SetDock(right, Dock.Right);

        var emblem = _emblem = new Image
        {
            Source = new Bitmap(AssetLoader.Open(PanelAssets.Uri("emblem-48.png"))),
            Width = 30,
            Height = 30,
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _summary.FontSize = Tokens.FontSizeCaption;
        var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 0 };
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        titleRow.Children.Add(new TextBlock { Text = "tPoseidon", FontWeight = FontWeight.SemiBold, FontSize = 14 });
        titleRow.Children.Add(new Border
        {
            Background = Tokens.AccentSoft,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 1),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = "Original", FontSize = 10, FontWeight = FontWeight.SemiBold, Foreground = Tokens.Accent },
        });
        titles.Children.Add(titleRow);
        titles.Children.Add(_summary);
        var left = new StackPanel { Orientation = Orientation.Horizontal };
        left.Children.Add(emblem);
        left.Children.Add(titles);

        var dock = new DockPanel { LastChildFill = true, Margin = new Thickness(10, 0) };
        dock.Children.Add(right);
        dock.Children.Add(left);
        header.Children.Add(dock);
        return new Border { BorderBrush = Tokens.BorderSubtle, BorderThickness = new Thickness(0, 0, 0, 1), Child = header };
    }

    private static Border BuildColumnHeader()
    {
        var grid = new Grid { Margin = new Thickness(12, 0, 8, 0), Height = 24, ColumnDefinitions = new ColumnDefinitions(ColumnLayout) };
        string[] labels = ["", "Service", "Port", "Status", "CPU", "Memory", ""];
        for (var i = 0; i < labels.Length; i++)
        {
            var text = Tokens.Caption(labels[i]);
            text.FontWeight = FontWeight.SemiBold;
            text.Foreground = Tokens.TextMuted;
            Grid.SetColumn(text, i);
            grid.Children.Add(text);
        }

        return new Border { Background = Tokens.Surface1, BorderBrush = Tokens.BorderSubtle, BorderThickness = new Thickness(0, 0, 0, 1), Child = grid };
    }

    private Avalonia.Controls.Panel BuildList()
    {
        var area = new Avalonia.Controls.Panel();
        area.Children.Add(new SeaHost(new ListSeaHandler(), _motion));
        area.Children.Add(new ScrollViewer { Content = _rowStack, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto });
        _toast.PointerPressed += (_, _) => _toast.Hide();
        area.Children.Add(_toast);
        return area;
    }

    private Border BuildActivity()
    {
        var title = Tokens.Caption("Recent activity");
        title.FontWeight = FontWeight.SemiBold;
        title.Foreground = Tokens.TextMuted;
        var open = new Button { Content = "Open logs", FontSize = Tokens.FontSizeCaption, Padding = new Thickness(6, 0), Height = 20, MinHeight = 0, Foreground = Tokens.Accent };
        open.Classes.Add("flat");
        open.Click += (_, _) => LogWindow.Open(null);
        DockPanel.SetDock(open, Dock.Right);
        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 2) };
        head.Children.Add(open);
        head.Children.Add(title);
        var stack = new StackPanel { Spacing = 1 };
        stack.Children.Add(head);
        foreach (var line in _activity)
        {
            stack.Children.Add(line);
        }

        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#E61B1F26")),
            BorderBrush = Tokens.BorderSubtle,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(12, 6, 8, 6),
            Child = stack,
        };
    }

    private Border BuildStatusBar()
    {
        DockPanel.SetDock(_readout, Dock.Right);
        _statusLeft.TextTrimming = TextTrimming.CharacterEllipsis;
        var dock = new DockPanel { LastChildFill = true };
        dock.Children.Add(_readout);
        dock.Children.Add(_statusLeft);
        return new Border
        {
            Background = Tokens.Surface1,
            BorderBrush = Tokens.BorderSubtle,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(12, 0),
            Child = dock,
        };
    }

    private void ShowPlayAccount()
    {
        var name = _host.PlayAccountName;
        _playAccount.IsVisible = _host.IsSetUp;
        _playAccount.Content = new TextBlock
        {
            Text = name.Length == 0 ? "no account" : $"as {name}",
            FontSize = Tokens.FontSizeCaption,
            Foreground = name.Length == 0 ? Tokens.Warning : Tokens.TextSecondary,
            MaxWidth = 140,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    private async Task PickPlayAccountAsync()
    {
        var names = await _host.ListAccountsAsync().ConfigureAwait(true);
        if (names.Count == 0)
        {
            return;
        }

        var menu = new ContextMenu();
        foreach (var name in names)
        {
            var item = new MenuItem
            {
                Header = name,
                Icon = string.Equals(name, _host.PlayAccountName, StringComparison.Ordinal) ? Tokens.Glyphs(Glyph.Check, 12, Tokens.Accent) : null,
            };
            item.Click += async (_, _) => await _host.ChoosePlayAccountAsync(name).ConfigureAwait(true);
            menu.Items.Add(item);
        }

        menu.Open(_playAccount);
    }

    private void RebuildRows()
    {
        _rows.Clear();
        _rowStack.Children.Clear();
        foreach (var service in _host.Services)
        {
            var row = new ServiceRow(service, _host, _motion, name => LogWindow.Open(name));
            _rows.Add(row);
            _rowStack.Children.Add(row);
        }

        LogWindow.Sources = ["All", .. _host.Services.Select(service => service.Name), LogHub.PanelSource];
        _client.IsEnabled = _host.IsSetUp;
        OnRowsChanged();
    }

    private void OnServiceChanged(ServiceModel service, ServiceTransition transition)
    {
        if (_rows.FirstOrDefault(row => row.Service == service) is { } row)
        {
            row.Refresh(transition);
        }

        OnRowsChanged();
    }

    private void OnRowsChanged()
    {
        var services = _host.Services;
        var running = services.Count(service => service.Status == ServerStatus.Running);
        var crashed = services.FirstOrDefault(service => service.Status == ServerStatus.Crashed);
        var state = !_host.IsSetUp ? "Not set up yet"
            : crashed is not null ? $"{crashed.Name} {(crashed.IsDatabase ? "is not reachable" : "crashed")}"
            : running == services.Count ? $"All {services.Count} services up"
            : running == 0 ? "All services stopped"
            : $"{running} of {services.Count} services up";
        _summary.Text = $"v{ServerStartup.CurrentVersion}  ·  {state}";
        _summary.Foreground = crashed is not null ? Tokens.Danger : running == services.Count ? Tokens.Success : Tokens.TextSecondary;
        _statusLeft.Text = $"{running} of {services.Count} running  ·  data folder {ShortPath(_host.Paths.Root)}";
    }

    private void OnClock()
    {
        var sample = _meter.Sample();
        var motion = _motion.Reduced ? "reduced" : _motion.Ambient ? $"{sample.Fps:0} fps" : "paused";
        _readout.Text = $"CPU {sample.CpuMachinePercent:0.0} %  ·  RAM {sample.WorkingSetMb:0} MB  ·  {motion}";
        foreach (var row in _rows)
        {
            row.Refresh(ServiceTransition.Refreshed);
        }
    }

    private void RefreshActivity()
    {
        var lines = LogHub.Lines;
        for (var i = 0; i < _activity.Length; i++)
        {
            var index = lines.Count - _activity.Length + i;
            var box = _activity[i];
            if (index < 0)
            {
                box.Text = "";
                continue;
            }

            var line = lines[index];
            box.Text = $"{line.Time:HH:mm:ss}  {line.Server,-9}{LogHub.HidePaths(line.Message)}";
            box.Foreground = line.Level switch { LogLevel.Error => Tokens.Danger, LogLevel.Warn => Tokens.Warning, _ => Tokens.TextSecondary };
        }
    }

    private void ShowToast(PanelNotice notice)
    {
        _toast.Show(notice.Kind, notice.Title, notice.Detail);
        _toastTimer?.Dispose();
        _toastTimer = DispatcherTimer.RunOnce(_toast.Hide, ToastLength);
    }

    private void SetReduced(bool reduced)
    {
        _motion.SetReduced(reduced);
        _preferences.ReduceAnimations = reduced;
        try
        {
            _preferences.Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogHub.Write(LogHub.PanelSource, LogLevel.Warn, $"Could not save the panel preferences to {PanelPreferences.FilePath}: {ex.Message}");
        }
    }

    private async Task OpenDataFolderAsync()
    {
        Directory.CreateDirectory(_host.Paths.Root);
        if (!await Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(_host.Paths.Root)).ConfigureAwait(true))
        {
            ShowToast(new PanelNotice(BannerKind.Warning, "The data folder could not be opened.", _host.Paths.Root));
        }
    }

    private static string ShortPath(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 2)
        {
            return path;
        }

        var root = Path.GetPathRoot(path) ?? "";
        return $"{root.TrimEnd(Path.DirectorySeparatorChar)}{Path.DirectorySeparatorChar}...{Path.DirectorySeparatorChar}{parts[^1]}";
    }

    public void Hold()
    {
        _held = true;
        ShowInTaskbar = false;
        if (_emblem is not null)
        {
            _emblem.Opacity = 0;
        }

        if (Content is Control root)
        {
            root.IsHitTestVisible = false;
        }
    }

    public void Release()
    {
        if (!_held)
        {
            return;
        }

        _held = false;
        _ = Cloak(false);
        ShowInTaskbar = true;
        if (Content is Control root)
        {
            root.IsHitTestVisible = true;
        }

        if (IsVisible)
        {
            Begin();
        }
    }

    public void SetEmblemVisible(bool visible)
    {
        if (_emblem is not null)
        {
            _emblem.Opacity = visible ? 1 : 0;
        }
    }

    public (double X, double Y, double Size)? EmblemScreenBox()
    {
        if (_emblem is null || TopLevel.GetTopLevel(_emblem) is not { } top || _emblem.Bounds.Width <= 0)
        {
            return null;
        }

        var topLeft = _emblem.TranslatePoint(new Point(0, 0), top);
        var bottomRight = _emblem.TranslatePoint(new Point(_emblem.Bounds.Width, _emblem.Bounds.Height), top);
        if (topLeft is null || bottomRight is null)
        {
            return null;
        }

        var a = top.PointToScreen(topLeft.Value);
        var b = top.PointToScreen(bottomRight.Value);
        return ((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0, b.X - a.X);
    }

    private bool Cloak(bool on) =>
        OperatingSystem.IsWindows() && TryGetPlatformHandle() is { } handle && NativeWindows.Cloak(handle.Handle, on);

    private void OnOpened()
    {
        if (_held)
        {
            _ = Cloak(true);
            return;
        }

        Begin();
    }

    private void Begin()
    {
        Opacity = 1;
        if (_begun)
        {
            return;
        }

        _begun = true;
        _host.Begin();
        FirstShown?.Invoke();
    }
}

public sealed record MeterSample(double CpuMachinePercent, double CpuCorePercent, double WorkingSetMb, double PrivateMb, double Fps);

public sealed class ResourceMeter
{
    private readonly Process _process = Process.GetCurrentProcess();
    private TimeSpan _lastCpu;
    private long _lastStamp;
    private int _lastFrames;

    public MeterSample Sample()
    {
        _process.Refresh();
        var cpu = _process.TotalProcessorTime;
        var stamp = Stopwatch.GetTimestamp();
        var frames = HeaderSeaHandler.Frames;
        var elapsed = _lastStamp == 0 ? 0 : (stamp - _lastStamp) / (double)Stopwatch.Frequency;
        var core = elapsed > 0 ? (cpu - _lastCpu).TotalSeconds / elapsed * 100 : 0;
        var fps = elapsed > 0 ? (frames - _lastFrames) / elapsed : 0;
        _lastCpu = cpu;
        _lastStamp = stamp;
        _lastFrames = frames;
        return new MeterSample(core / Environment.ProcessorCount, core, _process.WorkingSet64 / 1048576.0, _process.PrivateMemorySize64 / 1048576.0, fps);
    }
}

public sealed class ServiceRow : Border
{
    private const double CpuBarWidth = 80;
    private const double CpuBarFull = 25;

    private readonly SeaHost _shell;
    private readonly TextBlock _state = Tokens.Caption();
    private readonly TextBlock _port = Tokens.Caption();
    private readonly TextBlock _cpuText = Tokens.Caption();
    private readonly TextBlock _ramText = Tokens.Caption();
    private readonly Border _cpuFill = new() { Height = 4, CornerRadius = new CornerRadius(2), HorizontalAlignment = HorizontalAlignment.Left, Background = Tokens.Accent };
    private readonly Button _toggle;
    private readonly StackPanel _hoverActions;
    private readonly PanelHost _host;
    private ServerStatus _shownStatus = ServerStatus.Stopped;

    public ServiceRow(ServiceModel service, PanelHost host, Motion motion, Action<string> openLog)
    {
        Service = service;
        _host = host;
        Height = Tokens.RowHeight;
        Background = Brushes.Transparent;
        BorderBrush = Tokens.BorderSubtle;
        BorderThickness = new Thickness(0, 0, 0, 1);

        _shell = new SeaHost(new ShellBadgeHandler(), motion) { Width = 30, Height = 30, Margin = new Thickness(-6, 0, -2, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        var title = new TextBlock { Text = service.Name, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        ToolTip.SetTip(title, service.Title);
        _state.TextTrimming = TextTrimming.CharacterEllipsis;

        _toggle = Tokens.IconButton(Glyph.Play, "Start", Tokens.Accent);
        _toggle.Click += async (_, _) => await ToggleAsync().ConfigureAwait(true);
        var restart = Tokens.IconButton(Glyph.Restart, "Restart");
        restart.Click += async (_, _) => await _host.RestartAsync(Service).ConfigureAwait(true);
        var log = Tokens.IconButton(Glyph.Log, "Open the log window for this server");
        log.Click += (_, _) => openLog(service.Name);
        _hoverActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Opacity = 0, Transitions = [new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(140) }] };
        _hoverActions.Children.Add(restart);
        _hoverActions.Children.Add(log);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
        actions.Children.Add(_hoverActions);
        actions.Children.Add(_toggle);
        if (service.IsDatabase)
        {
            restart.IsVisible = false;
            _toggle.IsVisible = false;
            ToolTip.SetTip(log, "Open the log window for the database checks");
        }

        var cpu = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        var cpuTrack = new Border { Height = 4, CornerRadius = new CornerRadius(2), Background = Tokens.Surface2, Child = _cpuFill, Width = CpuBarWidth, HorizontalAlignment = HorizontalAlignment.Left };
        cpu.Children.Add(_cpuText);
        cpu.Children.Add(cpuTrack);

        var grid = new Grid { Margin = new Thickness(12, 0, 8, 0), ColumnDefinitions = new ColumnDefinitions(MainWindow.ColumnLayout) };
        Place(grid, _shell, 0);
        Place(grid, title, 1);
        Place(grid, _port, 2);
        Place(grid, _state, 3);
        Place(grid, cpu, 4);
        Place(grid, _ramText, 5);
        Place(grid, actions, 6);
        Child = grid;

        PointerEntered += (_, _) => ShowHover(true);
        PointerExited += (_, _) => ShowHover(false);
        Refresh(ServiceTransition.Refreshed);
    }

    public ServiceModel Service { get; }

    public void ShowHover(bool on)
    {
        Background = on ? Tokens.Surface1 : Brushes.Transparent;
        _hoverActions.Opacity = on ? 1 : 0;
    }

    public void Refresh(ServiceTransition transition)
    {
        var service = Service;
        var running = service.Status == ServerStatus.Running;
        var ripple = transition == ServiceTransition.Started;
        var shake = transition == ServiceTransition.Crashed;
        if (ripple || shake || service.Status != _shownStatus)
        {
            _shownStatus = service.Status;
            _shell.Send(new BadgeState(service.Status, ripple, shake));
        }

        _port.Text = service.Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        (_state.Text, _state.Foreground) = StatusText(service);
        ToolTip.SetTip(_state, service.Detail);
        var measured = running && service.Measured;
        _cpuText.Text = measured ? $"{service.Cpu:0.0} %" : "—";
        _cpuText.Foreground = measured ? Tokens.TextPrimary : Tokens.TextMuted;
        _cpuFill.Width = measured ? Math.Max(2, CpuBarWidth * Math.Min(1, service.Cpu / CpuBarFull)) : 0;
        _ramText.Text = measured ? $"{service.RamMb:0} MB" : "—";
        _ramText.Foreground = measured ? Tokens.TextPrimary : Tokens.TextMuted;
        var stoppable = service.Status is ServerStatus.Running or ServerStatus.Starting;
        var glyph = (GlyphView)_toggle.Content!;
        glyph.Symbol = stoppable ? Glyph.Stop : Glyph.Play;
        glyph.Foreground = stoppable ? Tokens.Danger : Tokens.Accent;
        ToolTip.SetTip(_toggle, stoppable ? "Stop" : "Start");
        _toggle.IsEnabled = _host.IsSetUp && !service.Busy;
    }

    private async Task ToggleAsync()
    {
        if (Service.Status is ServerStatus.Running or ServerStatus.Starting)
        {
            await _host.StopAsync(Service).ConfigureAwait(true);
        }
        else
        {
            await _host.StartAsync(Service).ConfigureAwait(true);
        }
    }

    private (string Text, IBrush Brush) StatusText(ServiceModel service)
    {
        if (!_host.IsSetUp)
        {
            return ("Not set up", Tokens.TextMuted);
        }

        if (service.Busy && service.StopRequested && service.Status == ServerStatus.Running)
        {
            return ("Stopping...", Tokens.TextSecondary);
        }

        return service.Status switch
        {
            ServerStatus.Running when service.IsDatabase => (service.Detail ?? "Reachable", Tokens.Success),
            ServerStatus.Running => (service.StartedUtc is { } started ? $"Up {FormatUptime(DateTime.UtcNow - started)}" : "Up", Tokens.Success),
            ServerStatus.Starting => ("Starting...", Tokens.Accent),
            ServerStatus.Crashed when service.IsDatabase => ("Not reachable", Tokens.Danger),
            ServerStatus.Crashed => ("Crashed", Tokens.Danger),
            _ when service.IsDatabase => (service.Detail is null ? "Checking..." : "Not reachable", Tokens.TextMuted),
            _ => ("Stopped", Tokens.TextMuted),
        };
    }

    private static void Place(Grid grid, Control control, int column)
    {
        Grid.SetColumn(control, column);
        grid.Children.Add(control);
    }

    private static string FormatUptime(TimeSpan span) =>
        span.TotalHours >= 1 ? $"{(int)span.TotalHours}h {span.Minutes:00}m" : span.TotalMinutes >= 1 ? $"{span.Minutes}m {span.Seconds:00}s" : $"{Math.Max(0, span.Seconds)}s";
}
