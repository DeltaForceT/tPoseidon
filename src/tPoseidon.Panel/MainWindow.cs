using System.Diagnostics;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using tPoseidon.Hosting;
using tPoseidon.LauncherKit;
using tPoseidon.Hosting.Servers;

namespace tPoseidon.Panel;

public sealed class MainWindow : Window
{
    private const int OffscreenFloor = -30000;
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
    private const double BubbleIconSize = 12;
    private const double TitleRoom = 180;
    private readonly List<TextBlock> _toolbarLabels = [];
    private StackPanel _toolbar = new();
    private Grid _headerRow = new();
    private const double BubbleIconRim = 1.5;
    private const string BubbleToggleTip = "Quick-access bubble: when on, minimising the panel turns it into a bubble with the server tools";
    private readonly ToggleButton _bubbleToggle = BubbleToggle();
    private readonly List<Control> _bubbleCovered = [];
    private IDisposable? _toastTimer;
    private bool _held;
    private bool _setupOpen;
    private PixelPoint? _restOrigin;
    private Size _restSize;
    private bool _begun;
    private Image? _emblem;

    public MainWindow(PanelHost host, PanelPreferences preferences)
    {
        _host = host;
        _preferences = preferences;
        Tools = new PanelTools(host, this, OpenSetup);
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
        ApplyBubbleMode();

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
        PositionChanged += (_, _) => RememberRestBox();
        RebuildRows();
        OnClock();
    }

    public event Action? FirstShown;

    public event Action? BubbleModeChanged;

    public PanelTools Tools { get; }

    public PanelFlight? Flight { get; set; }

    public PanelPreferences Preferences => _preferences;

    public bool SetupOpen => _setupOpen;

    public (PixelPoint Origin, Size Size)? RestBox => _restOrigin is { } origin ? (origin, _restSize) : null;

    public void Reveal()
    {
        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }

    public void Exit() => Close();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WindowStateProperty)
        {
            var minimized = WindowState == WindowState.Minimized;
            _motion.SetMinimized(minimized);
            if (minimized && _preferences.BubbleEnabled)
            {
                Dispatcher.UIThread.Post(() => Flight?.Enter());
            }
            else if (!minimized && change.OldValue is WindowState.Minimized)
            {
                Flight?.Leave();
            }
        }
        else if (change.Property == BoundsProperty)
        {
            RememberRestBox();
        }
    }

    private void RememberRestBox()
    {
        if (WindowState != WindowState.Normal || !IsVisible || Bounds.Width <= 0)
        {
            return;
        }

        var origin = this.PointToScreen(new Point(0, 0));
        if (origin.X < OffscreenFloor || origin.Y < OffscreenFloor)
        {
            return;
        }

        _restOrigin = origin;
        _restSize = Bounds.Size;
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
        Reveal();
        var window = new SetupWindow(_host.Paths);
        window.Completed += start =>
        {
            _host.Reload();
            if (start)
            {
                _ = Tools.StartAllAsync();
            }
        };
        _setupOpen = true;
        window.Closed += (_, _) => _setupOpen = false;
        window.Show(this);
    }

    private Border BuildHeader()
    {
        var header = new Avalonia.Controls.Panel();
        header.Children.Add(new SeaHost(new HeaderSeaHandler(), _motion));

        var startAll = Tokens.IconButton(Glyph.Play, "Start all servers, one by one", Tokens.Accent);
        startAll.Click += async (_, _) => await Tools.StartAllAsync().ConfigureAwait(true);
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
        var share = new MenuItem { Header = "Share a server file for friends..." };
        share.Click += (_, _) => Tools.ShareServerFile();
        var launcher = new MenuItem { Header = "Make a launcher for your players..." };
        launcher.Click += (_, _) => Tools.MakeLauncher();
        var channel = new MenuItem { Header = "Edit launcher news and updates..." };
        channel.Click += (_, _) => Tools.EditLauncherChannel();
        var publish = new MenuItem { Header = "Publish launcher update" };
        publish.Click += async (_, _) => await _host.PublishLauncherUpdateAsync().ConfigureAwait(true);
        var launcherDrawer = new MenuItem { Header = "Launcher", Items = { launcher, channel, publish } };
        var language = new MenuItem { Header = "Client language" };
        foreach (var choice in Tools.Languages)
        {
            language.Items.Add(choice.Item);
        }

        var guildCoins = new MenuItem { Header = "Guild coins per win..." };
        guildCoins.Click += (_, _) => Tools.EditGuildCoins();
        var characterSlots = new MenuItem { Header = "Character slots per account..." };
        characterSlots.Click += (_, _) => Tools.EditCharacterSlots();
        var folder = new MenuItem { Header = "Open the data folder" };
        folder.Click += async (_, _) => await Tools.OpenDataFolderAsync().ConfigureAwait(true);
        var splash = new MenuItem { Header = "Replay startup splash" };
        splash.Click += (_, _) =>
        {
            var replay = new SplashWindow(_ => { });
            replay.Bind(this, false);
            replay.PlaceOverScreenCenter();
            replay.Show();
            replay.Aim();
        };
        var exit = new MenuItem { Header = "Exit tPoseidon" };
        exit.Click += (_, _) => Exit();
        var firstDivider = new Separator();
        var lastDivider = new Separator();
        _bubbleToggle.IsCheckedChanged += (_, _) => SetBubble(_bubbleToggle.IsChecked == true);
        _bubbleToggle.IsChecked = _preferences.BubbleEnabled;
        var contextMenu = new ContextMenu
        {
            Items =
            {
                setup,
                backup,
                share,
                launcherDrawer,
                language,
                guildCoins,
                characterSlots,
                folder,
                firstDivider,
                _reduce,
                splash,
                lastDivider,
                exit,
            },
        };
        _bubbleCovered.AddRange([logs, security, backup, share, folder, firstDivider, lastDivider, exit]);
        contextMenu.Opening += (_, _) =>
        {
            backup.IsEnabled = _host.IsSetUp;
            share.IsEnabled = _host.IsSetUp;
            launcher.IsEnabled = _host.IsSetUp;
            channel.IsEnabled = _host.IsSetUp;
            publish.IsEnabled = _host.IsSetUp;
            guildCoins.IsEnabled = _host.IsSetUp;
            characterSlots.IsEnabled = _host.IsSetUp;
            language.IsEnabled = _host.IsSetUp;
            Tools.MarkLanguages();
            splash.IsEnabled = !_motion.Reduced;
        };
        menu.ContextMenu = contextMenu;
        menu.Click += (_, _) => contextMenu.Open(menu);

        var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Spacing = 4 };
        right.Children.Add(startAll);
        right.Children.Add(stopAll);
        right.Children.Add(restartAll);
        var divider = new Border { Width = 1, Height = 16, Margin = new Thickness(4, 0), Background = Tokens.BorderSubtle };
        right.Children.Add(divider);
        right.Children.Add(_client);
        right.Children.Add(_playAccount);
        right.Children.Add(logs);
        right.Children.Add(security);
        right.Children.Add(gameMaster);
        right.Children.Add(_bubbleToggle);
        right.Children.Add(menu);
        _toolbar = right;
        foreach (var labelled in new ContentControl[] { _client, logs, security, gameMaster, _bubbleToggle })
        {
            if (labelled.Content is StackPanel content)
            {
                _toolbarLabels.AddRange(content.Children.OfType<TextBlock>());
            }
        }

        var emblem = _emblem = new Image
        {
            Source = new Bitmap(AssetLoader.Open(PanelAssets.Uri("emblem-48.png"))),
            Width = 30,
            Height = 30,
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _summary.FontSize = Tokens.FontSizeCaption;
        _summary.TextTrimming = TextTrimming.CharacterEllipsis;
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
        var left = new DockPanel { LastChildFill = true, ClipToBounds = true };
        DockPanel.SetDock(emblem, Dock.Left);
        left.Children.Add(emblem);
        left.Children.Add(titles);

        var row = _headerRow = new Grid { Margin = new Thickness(10, 0), ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(right, 1);
        row.Children.Add(left);
        row.Children.Add(right);
        row.SizeChanged += (_, _) => FitToolbar();
        header.Children.Add(row);
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
        FitToolbar();
    }

    private static ToggleButton BubbleToggle()
    {
        var icon = new Avalonia.Controls.Shapes.Ellipse
        {
            Width = BubbleIconSize,
            Height = BubbleIconSize,
            StrokeThickness = BubbleIconRim,
            Stroke = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse("#C4B5FD"), 0), new GradientStop(Color.Parse("#7DD3FC"), 0.5), new GradientStop(Color.Parse("#5EEAD4"), 1) },
            },
            Fill = new RadialGradientBrush
            {
                GradientStops = { new GradientStop(Color.Parse("#00FFFFFF"), 0.55), new GradientStop(Color.Parse("#407DD3FC"), 1) },
            },
            VerticalAlignment = VerticalAlignment.Center,
        };
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        content.Children.Add(icon);
        content.Children.Add(new TextBlock { Text = "Bubble", VerticalAlignment = VerticalAlignment.Center });
        var toggle = new ToggleButton { Content = content, Height = 26, MinHeight = 0, Padding = new Thickness(8, 0), VerticalContentAlignment = VerticalAlignment.Center };
        toggle.Classes.Add("chip");
        ToolTip.SetTip(toggle, BubbleToggleTip);
        return toggle;
    }

    private void ApplyBubbleMode()
    {
        foreach (var control in _bubbleCovered)
        {
            control.IsVisible = !_preferences.BubbleEnabled;
        }

        FitToolbar();
    }

    public bool ToolbarLabelsShown => _toolbarLabels.TrueForAll(label => label.IsVisible);

    private void FitToolbar()
    {
        var room = _headerRow.Bounds.Width - TitleRoom;
        if (room <= 0)
        {
            return;
        }

        SetToolbarLabels(true);
        _toolbar.Measure(Size.Infinity);
        SetToolbarLabels(_toolbar.DesiredSize.Width <= room);
    }

    private void SetToolbarLabels(bool shown)
    {
        foreach (var label in _toolbarLabels)
        {
            label.IsVisible = shown;
        }
    }

    public void SetBubble(bool enabled)
    {
        if (_preferences.BubbleEnabled == enabled && _bubbleToggle.IsChecked == enabled)
        {
            return;
        }

        _preferences.BubbleEnabled = enabled;
        _preferences.TrySave();
        _bubbleToggle.IsChecked = enabled;
        ApplyBubbleMode();
        BubbleModeChanged?.Invoke();
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
        _preferences.TrySave();
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

    internal bool Cloak(bool on) =>
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
