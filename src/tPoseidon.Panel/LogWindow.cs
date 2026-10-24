using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace tPoseidon.Panel;

public sealed record LogRow(LogLine Line, string Text);

public sealed class LogWindow : Window
{
    private const int MaxRows = 600;
    private const string AllSources = "All";
    private static LogWindow? s_instance;

    private readonly ObservableCollection<LogRow> _rows = [];
    private readonly ListBox _list;
    private readonly Dictionary<string, ToggleButton> _serverChips = [];
    private readonly ToggleButton _info = Chip("Info", Tokens.TextSecondary, true);
    private readonly ToggleButton _warn = Chip("Warn", Tokens.Warning, true);
    private readonly ToggleButton _error = Chip("Error", Tokens.Danger, true);
    private readonly ToggleButton _auto = Chip("Auto-scroll", null, true);
    private readonly ToggleButton _paths = Chip("Hide personal paths", null, false);
    private readonly TextBox _search = new() { Watermark = "Search", Width = 170, Height = 26, MinHeight = 0, Padding = new Thickness(8, 3), VerticalContentAlignment = VerticalAlignment.Center, FontSize = Tokens.FontSizeBody };
    private readonly TextBlock _count = Tokens.Caption();
    private readonly TextBlock _flash = Tokens.Caption();
    private string _server = AllSources;

    public LogWindow()
    {
        Title = "tPoseidon logs";
        Width = 860;
        Height = 520;
        MinWidth = 640;
        MinHeight = 320;
        FontFamily = Tokens.UiFont;
        FontSize = Tokens.FontSizeBody;
        Foreground = Tokens.TextPrimary;
        Background = Tokens.Surface0;
        UseLayoutRounding = true;
        Icon = PanelAssets.WindowIcon();

        _list = new ListBox
        {
            ItemsSource = _rows,
            Background = Tokens.Surface0,
            SelectionMode = SelectionMode.Multiple,
            ItemTemplate = new FuncDataTemplate<LogRow>((row, _) => RowView(row)),
            FontFamily = Tokens.MonoFont,
            Padding = new Thickness(0),
        };

        var root = new DockPanel();
        var top = BuildTop();
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);
        var bottom = BuildBottom();
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bottom);
        root.Children.Add(_list);
        Content = root;

        Activated += (_, _) => Motion.Shared.SetFocused(true);
        Deactivated += (_, _) => Motion.Shared.SetFocused(false);
        Opened += (_, _) =>
        {
            Refilter();
            LogHub.Added += OnAdded;
        };
        Closed += (_, _) =>
        {
            LogHub.Added -= OnAdded;
            s_instance = null;
        };
    }

    public static IReadOnlyList<string> Sources { get; set; } = [AllSources];

    public static void Open(string? server)
    {
        if (s_instance is null)
        {
            s_instance = new LogWindow();
            s_instance.Show();
        }
        else
        {
            if (s_instance.WindowState == WindowState.Minimized)
                s_instance.WindowState = WindowState.Normal;
            s_instance.Activate();
        }
        s_instance.SelectServer(server ?? AllSources);
    }

    public static void Toggle()
    {
        if (s_instance is null)
            Open(null);
        else
            s_instance.Close();
    }

    public void SelectServer(string server)
    {
        _server = _serverChips.ContainsKey(server) ? server : AllSources;
        foreach (var (name, chip) in _serverChips)
            chip.IsChecked = name == _server;
        Refilter();
    }

    private static ToggleButton Chip(string text, IBrush? dot, bool on)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        if (dot is not null)
            content.Children.Add(new Border { Width = 7, Height = 7, CornerRadius = new CornerRadius(4), Background = dot, VerticalAlignment = VerticalAlignment.Center });
        content.Children.Add(new TextBlock { Text = text, FontSize = Tokens.FontSizeCaption });
        var chip = new ToggleButton { Content = content, Height = 26, MinHeight = 0, Padding = new Thickness(10, 0), IsChecked = on, VerticalContentAlignment = VerticalAlignment.Center, Foreground = Tokens.TextPrimary };
        chip.Classes.Add("chip");
        return chip;
    }

    private Border BuildTop()
    {
        var sea = new Avalonia.Controls.Panel { Height = 30 };
        sea.Children.Add(new SeaHost(new HeaderSeaHandler(), Motion.Shared));
        var title = new TextBlock { Text = "Server logs", FontWeight = FontWeight.SemiBold, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        sea.Children.Add(title);

        var servers = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var name in Sources)
        {
            var chip = Chip(name, null, name == _server);
            chip.Margin = new Thickness(0, 0, 4, 0);
            chip.Click += (_, _) => SelectServer(name);
            _serverChips[name] = chip;
            servers.Children.Add(chip);
        }

        var levels = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        foreach (var chip in new[] { _info, _warn, _error })
        {
            chip.Click += (_, _) => Refilter();
            levels.Children.Add(chip);
        }
        _search.TextChanged += (_, _) => Refilter();

        var row1 = new DockPanel { Margin = new Thickness(10, 8, 10, 4) };
        _search.HorizontalAlignment = HorizontalAlignment.Right;
        DockPanel.SetDock(_search, Dock.Right);
        row1.Children.Add(_search);
        row1.Children.Add(servers);

        _auto.Click += (_, _) => { if (_auto.IsChecked == true) ScrollToEnd(); };
        _paths.Click += (_, _) => Refilter();
        var copy = Tokens.ToolbarButton(Glyph.Copy, "Copy", "Copy the selected lines, or every visible line");
        copy.Classes.Add("secondary");
        copy.Click += async (_, _) => await CopyAsync();
        var clear = Tokens.ToolbarButton(Glyph.Clear, "Clear", "Clear the view");
        clear.Classes.Add("secondary");
        clear.Click += (_, _) => _rows.Clear();
        ToolTip.SetTip(_paths, "Replace your user folder in paths with ~ so the log can be shared");
        var row2 = new DockPanel { Margin = new Thickness(10, 0, 10, 8) };
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        tools.Children.Add(_auto);
        tools.Children.Add(_paths);
        tools.Children.Add(copy);
        tools.Children.Add(clear);
        DockPanel.SetDock(tools, Dock.Right);
        row2.Children.Add(tools);
        row2.Children.Add(levels);

        var stack = new StackPanel();
        stack.Children.Add(sea);
        stack.Children.Add(row1);
        stack.Children.Add(row2);
        return new Border { Background = Tokens.Surface1, BorderBrush = Tokens.BorderSubtle, BorderThickness = new Thickness(0, 0, 0, 1), Child = stack };
    }

    private Border BuildBottom()
    {
        _flash.Foreground = Tokens.Success;
        DockPanel.SetDock(_flash, Dock.Right);
        var dock = new DockPanel { LastChildFill = true };
        dock.Children.Add(_flash);
        dock.Children.Add(_count);
        return new Border { Background = Tokens.Surface1, BorderBrush = Tokens.BorderSubtle, BorderThickness = new Thickness(0, 1, 0, 0), Height = Tokens.StatusBarHeight, Padding = new Thickness(12, 0), Child = dock };
    }

    private static Border RowView(LogRow? row)
    {
        if (row is null)
            return new Border();
        var line = row.Line;
        var (levelText, levelBrush, tint) = line.Level switch
        {
            LogLevel.Error => ("ERROR", Tokens.Danger, Tokens.DangerTint),
            LogLevel.Warn => ("WARN", Tokens.Warning, Tokens.WarningTint),
            _ => ("INFO", Tokens.TextMuted, Brushes.Transparent),
        };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("72,70,52,*"), Margin = new Thickness(12, 2, 12, 2) };
        void Add(Control c, int col)
        {
            Grid.SetColumn(c, col);
            grid.Children.Add(c);
        }
        Add(new TextBlock { Text = line.Time.ToString("HH:mm:ss"), Foreground = Tokens.TextMuted, FontSize = 11.5 }, 0);
        Add(new TextBlock { Text = line.Server, Foreground = Tokens.Accent, FontSize = 11.5 }, 1);
        Add(new TextBlock { Text = levelText, Foreground = levelBrush, FontSize = 11.5, FontWeight = FontWeight.SemiBold }, 2);
        Add(new TextBlock { Text = row.Text, FontSize = 11.5, TextWrapping = TextWrapping.Wrap, Foreground = line.Level == LogLevel.Info ? Tokens.TextPrimary : levelBrush }, 3);
        return new Border { Background = tint, Child = grid };
    }

    private bool Matches(LogLine line) =>
        (_server == AllSources || line.Server == _server)
        && line.Level switch { LogLevel.Info => _info.IsChecked == true, LogLevel.Warn => _warn.IsChecked == true, _ => _error.IsChecked == true }
        && (string.IsNullOrWhiteSpace(_search.Text) || line.Message.Contains(_search.Text.Trim(), StringComparison.OrdinalIgnoreCase) || line.Server.Contains(_search.Text.Trim(), StringComparison.OrdinalIgnoreCase));

    private LogRow Make(LogLine line) => new(line, _paths.IsChecked == true ? LogHub.HidePaths(line.Message) : line.Message);

    private void Refilter()
    {
        var matching = LogHub.Lines.Where(Matches).TakeLast(MaxRows).Select(Make).ToList();
        _rows.Clear();
        foreach (var row in matching)
            _rows.Add(row);
        UpdateCount();
        ScrollToEnd();
    }

    private void OnAdded(LogLine line)
    {
        if (!Matches(line))
            return;
        _rows.Add(Make(line));
        if (_rows.Count > MaxRows)
            _rows.RemoveAt(0);
        UpdateCount();
        if (_auto.IsChecked == true)
            ScrollToEnd();
    }

    private void UpdateCount() => _count.Text = $"{_rows.Count} lines shown  ·  {LogHub.Lines.Count} kept in memory";

    private void ScrollToEnd()
    {
        if (_auto.IsChecked != true || _rows.Count == 0)
            return;
        var last = _rows[^1];
        Dispatcher.UIThread.Post(() => _list.ScrollIntoView(last), DispatcherPriority.Background);
    }

    private async Task CopyAsync()
    {
        var selected = _list.SelectedItems?.Cast<LogRow>().ToList();
        var source = selected is { Count: > 0 } ? selected : _rows.ToList();
        var text = string.Join(Environment.NewLine, source.Select(r => $"{r.Line.Time:HH:mm:ss} {r.Line.Server} {r.Line.Level.ToString().ToUpperInvariant()} {r.Text}"));
        if (Clipboard is { } clipboard)
            await clipboard.SetTextAsync(text);
        _flash.Text = $"Copied {source.Count} lines" + (_paths.IsChecked == true ? " (paths hidden)" : "");
        DispatcherTimer.RunOnce(() => _flash.Text = "", TimeSpan.FromSeconds(2.5));
    }
}
