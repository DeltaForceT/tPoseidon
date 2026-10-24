using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using tPoseidon.Hosting;
using tPoseidon.Hosting.GameMaster;

namespace tPoseidon.Panel;

public sealed class GmWindow : Window
{
    private static GmWindow? s_instance;

    private readonly GmModel _model;
    private readonly TextBox _character = GmKit.Box("GM character", 180);
    private readonly TextBox _password = new() { Watermark = "Account password", PasswordChar = '●', Width = 170, Height = 28, MinHeight = 0, Padding = new Thickness(8, 4), VerticalContentAlignment = VerticalAlignment.Center, FontSize = Tokens.FontSizeBody };
    private readonly Button _connect = GmKit.Plain("Connect", primary: true);
    private readonly Button _disconnect = GmKit.Plain("Disconnect");
    private readonly Border _dot = new() { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _link = new() { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, FontSize = Tokens.FontSizeCaption };
    private readonly ContentControl _page = new();
    private readonly StackPanel _results = new() { Spacing = 4 };
    private readonly TextBlock _running = Tokens.Caption();
    private readonly List<(ToggleButton Chip, Control Page)> _tabs = [];
    private int _shown = -1;

    public GmWindow(GmModel model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        Title = "tPoseidon game master";
        Width = 1000;
        Height = 760;
        MinWidth = 720;
        MinHeight = 520;
        FontFamily = Tokens.UiFont;
        FontSize = Tokens.FontSizeBody;
        Foreground = Tokens.TextPrimary;
        Background = Tokens.Surface0;
        UseLayoutRounding = true;
        Icon = PanelAssets.WindowIcon();

        var kit = new GmKit(model, this);
        AddTab("Players", GmPlayersPage.Build(kit));
        AddTab("Spawn", GmSpawnPage.Build(kit));
        AddTab("Server", GmServerPage.Build(kit));
        AddTab("Events", GmEventsPage.Build(kit));
        AddTab("Guilds", GmGuildsPage.Build(kit));
        AddTab("Penalties", GmPenaltiesPage.Build(kit));
        AddTab("GM accounts", GmAccountsPage.Build(kit, UseForLogin));
        AddTab("Help", GmHelpPage.Build(kit));

        var body = new Grid { RowDefinitions = new RowDefinitions("*,4,190") };
        _page.Margin = new Thickness(14, 10, 14, 14);

        body.Children.Add(new ScrollViewer { Content = _page });
        var splitter = new GridSplitter { Background = Tokens.BorderSubtle, ResizeDirection = GridResizeDirection.Rows };
        Grid.SetRow(splitter, 1);
        body.Children.Add(splitter);
        var results = BuildResults();
        Grid.SetRow(results, 2);
        body.Children.Add(results);

        var root = new DockPanel();
        var top = new StackPanel();
        top.Children.Add(BuildTop());
        top.Children.Add(BuildLogin());
        top.Children.Add(BuildTabs());
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);
        var bottom = BuildBottom();
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bottom);
        root.Children.Add(body);
        Content = root;

        ShowTab(0);
        Refresh();
        _model.Changed += () => Dispatcher.UIThread.Post(Refresh);
        Activated += (_, _) => Motion.Shared.SetFocused(true);
        Deactivated += (_, _) => Motion.Shared.SetFocused(false);
        Closed += (_, _) =>
        {
            s_instance = null;
            _model.Dispose();
        };
    }

    public GmModel Model => _model;

    public IReadOnlyList<string> Tabs => _tabs.Select(tab => tab.Chip.Content as string ?? "").ToList();

    public static void Toggle(InstallPaths paths)
    {
        if (s_instance is null)
        {
            s_instance = new GmWindow(new GmModel(GmServices.For(paths)));
            s_instance.Show();
        }
        else
        {
            s_instance.Close();
        }
    }

    public void ShowTab(int index)
    {
        if (index == _shown || index < 0 || index >= _tabs.Count)
        {
            return;
        }

        _shown = index;
        for (var i = 0; i < _tabs.Count; i++)
        {
            _tabs[i].Chip.IsChecked = i == index;
        }

        _page.Content = _tabs[index].Page;
    }

    private void AddTab(string title, Control page)
    {
        var chip = new ToggleButton { Content = title, Height = 26, MinHeight = 0, Padding = new Thickness(12, 0), VerticalContentAlignment = VerticalAlignment.Center, Foreground = Tokens.TextPrimary };
        chip.Classes.Add("chip");
        var index = _tabs.Count;
        chip.Click += (_, _) =>
        {
            chip.IsChecked = true;
            ShowTab(index);
        };
        _tabs.Add((chip, page));
    }

    private void UseForLogin(string character)
    {
        _character.Text = character;
        _password.Focus();
    }

    private static Border BuildTop()
    {
        var sea = new Avalonia.Controls.Panel { Height = 30 };
        sea.Children.Add(new SeaHost(new HeaderSeaHandler(), Motion.Shared));
        var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        title.Children.Add(Tokens.Glyphs(Glyph.Admin, 12, Tokens.Accent));
        title.Children.Add(new TextBlock { Text = "Game master", FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        sea.Children.Add(title);
        return new Border { Background = Tokens.Surface1, BorderBrush = Tokens.BorderSubtle, BorderThickness = new Thickness(0, 0, 0, 1), Child = sea };
    }

    private Border BuildLogin()
    {
        _connect.Click += async (_, _) => await ConnectAsync().ConfigureAwait(true);
        _disconnect.Classes.Remove("secondary");
        _disconnect.Classes.Add("flat");
        _disconnect.Click += (_, _) => _model.Disconnect();
        _password.KeyDown += async (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                await ConnectAsync().ConfigureAwait(true);
            }
        };
        var state = new DockPanel { Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        _dot.Margin = new Thickness(0, 0, 8, 0);
        DockPanel.SetDock(_dot, Dock.Left);
        state.Children.Add(_dot);
        state.Children.Add(_link);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,Auto,*") };
        _password.Margin = new Thickness(6, 0, 0, 0);
        _connect.Margin = new Thickness(6, 0, 0, 0);
        _disconnect.Margin = new Thickness(4, 0, 0, 0);
        Grid.SetColumn(_password, 1);
        Grid.SetColumn(_connect, 2);
        Grid.SetColumn(_disconnect, 3);
        Grid.SetColumn(state, 4);
        grid.Children.Add(_character);
        grid.Children.Add(_password);
        grid.Children.Add(_connect);
        grid.Children.Add(_disconnect);
        grid.Children.Add(state);
        return new Border { Background = Tokens.Surface1, BorderBrush = Tokens.BorderSubtle, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(12, 8), Child = grid };
    }

    private Border BuildTabs()
    {
        var strip = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var (chip, _) in _tabs)
        {
            chip.Margin = new Thickness(0, 0, 4, 0);
            strip.Children.Add(chip);
        }

        return new Border { BorderBrush = Tokens.BorderSubtle, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(12, 6), Child = strip };
    }

    private Border BuildResults()
    {
        var heading = new DockPanel { Margin = new Thickness(12, 6, 12, 4) };
        var clear = Tokens.IconButton(Glyph.Clear, "Clear the results");
        clear.Click += (_, _) => _model.ClearHistory();
        DockPanel.SetDock(clear, Dock.Right);
        heading.Children.Add(clear);
        heading.Children.Add(new TextBlock { Text = "Results", FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        var dock = new DockPanel();
        DockPanel.SetDock(heading, Dock.Top);
        dock.Children.Add(heading);
        dock.Children.Add(new ScrollViewer { Content = _results, Margin = new Thickness(12, 0, 12, 8) });
        return new Border { Background = Tokens.Surface1, Child = dock };
    }

    private Border BuildBottom()
    {
        var endpoint = Tokens.Caption(_model.Endpoint is { } where ? $"System server {where}" : "Not set up");
        DockPanel.SetDock(_running, Dock.Right);
        var dock = new DockPanel { LastChildFill = true };
        dock.Children.Add(_running);
        dock.Children.Add(endpoint);
        return new Border { Background = Tokens.Surface1, BorderBrush = Tokens.BorderSubtle, BorderThickness = new Thickness(0, 1, 0, 0), Height = Tokens.StatusBarHeight, Padding = new Thickness(12, 0), Child = dock };
    }

    private async Task ConnectAsync() => await _model.ConnectAsync(_character.Text ?? "", _password.Text ?? "").ConfigureAwait(true);

    private void Refresh()
    {
        var link = _model.Link;
        _dot.Background = link.Status switch
        {
            GmLinkStatus.Connected => Tokens.Success,
            GmLinkStatus.NotConnected => Tokens.TextMuted,
            _ => Tokens.Danger,
        };
        _link.Text = link.Message;
        _link.Foreground = link.Connected ? Tokens.TextPrimary : link.Status == GmLinkStatus.NotConnected ? Tokens.TextSecondary : Tokens.Danger;
        _connect.IsEnabled = !_model.Busy && link.Status != GmLinkStatus.NotSetUp;
        _disconnect.IsVisible = link.Connected;
        _character.IsEnabled = !link.Connected;
        _password.IsEnabled = !link.Connected;
        _running.Text = _model.Busy ? $"Running: {_model.Running}..." : "";
        _running.Foreground = Tokens.Accent;
        ShowHistory();
    }

    private void ShowHistory()
    {
        var history = _model.History;
        if (_results.Children.Count == history.Count && (history.Count == 0 || ReferenceEquals(_results.Children[0].Tag, history[0])))
        {
            return;
        }

        _results.Children.Clear();
        for (var i = 0; i < history.Count; i++)
        {
            _results.Children.Add(Entry(history[i], i == 0));
        }
    }

    private static Border Entry(GmEntry entry, bool latest)
    {
        var (glyph, brush, tint) = entry.Kind switch
        {
            GmEntryKind.Done => (Glyph.Check, Tokens.Success, Tokens.SuccessTint),
            GmEntryKind.Refused => (Glyph.Warn, Tokens.Warning, Tokens.WarningTint),
            _ => (Glyph.Error, Tokens.Danger, Tokens.DangerTint),
        };
        var head = new DockPanel();
        var icon = Tokens.Glyphs(glyph, 12, brush);
        icon.Margin = new Thickness(0, 0, 8, 0);
        DockPanel.SetDock(icon, Dock.Left);
        head.Children.Add(icon);
        var time = Tokens.Caption($"{entry.At:HH:mm:ss}");
        DockPanel.SetDock(time, Dock.Right);
        head.Children.Add(time);
        head.Children.Add(new TextBlock { Text = entry.Title, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(head);
        stack.Children.Add(new SelectableTextBlock
        {
            Text = string.Join(Environment.NewLine, entry.Lines),
            FontFamily = Tokens.MonoFont,
            FontSize = 11.5,
            Foreground = entry.Kind == GmEntryKind.Done ? Tokens.TextPrimary : brush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(20, 0, 0, 0),
        });
        return new Border
        {
            Tag = entry,
            Background = latest ? tint : Tokens.Surface0,
            BorderBrush = latest ? brush : Tokens.BorderSubtle,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 6),
            Child = stack,
        };
    }
}
