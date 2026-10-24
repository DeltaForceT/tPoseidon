using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using tPoseidon.Hosting;
using tPoseidon.Hosting.Security;

namespace tPoseidon.Panel;

public sealed class SecurityWindow : Window
{
    private static readonly (string Label, TimeSpan? Length)[] BanLengths =
    [
        ("for 1 hour", TimeSpan.FromHours(1)),
        ("for 1 day", TimeSpan.FromDays(1)),
        ("for 1 week", TimeSpan.FromDays(7)),
        ("until unbanned", null),
    ];

    private static readonly (string Key, string Label)[] Counters =
    [
        ("refusedBanned", "banned"),
        ("refusedAddressRate", "rate"),
        ("refusedAddressLimit", "per address"),
        ("refusedServerFull", "full"),
        ("handshakeTimeouts", "silent"),
        ("loginTimeouts", "no login"),
        ("partialFrames", "partial frames"),
        ("oversizedFrames", "oversized"),
        ("malformedFrames", "malformed"),
        ("packetFloods", "floods"),
        ("loginFailures", "failed logins"),
        ("lockouts", "lockouts"),
        ("bans", "auto-bans"),
        ("requestsRefused", "web requests refused"),
    ];

    private static SecurityWindow? s_instance;

    private readonly InstallPaths _paths;
    private readonly StackPanel _servers = new() { Spacing = 2 };
    private readonly StackPanel _blocked = new() { Spacing = 2 };
    private readonly StackPanel _incidents = new() { Spacing = 1 };
    private readonly TextBlock _allowed = new() { FontSize = Tokens.FontSizeCaption, Foreground = Tokens.TextSecondary, TextWrapping = TextWrapping.Wrap };
    private readonly TextBox _address = new() { Watermark = "Address or range, like 203.0.113.9 or 203.0.113.0/24", Width = 300, Height = 26, MinHeight = 0, Padding = new Thickness(8, 3), VerticalContentAlignment = VerticalAlignment.Center, FontSize = Tokens.FontSizeBody };
    private readonly ComboBox _length = new() { Height = 26, MinHeight = 0, Width = 130, FontSize = Tokens.FontSizeBody, ItemsSource = BanLengths.Select(length => length.Label).ToList(), SelectedIndex = 0 };
    private readonly TextBlock _flash = Tokens.Caption();
    private readonly TextBlock _folder = Tokens.Caption();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private bool _refreshing;

    public SecurityWindow(InstallPaths paths)
    {
        _paths = paths;
        Title = "tPoseidon security";
        Width = 760;
        Height = 560;
        MinWidth = 560;
        MinHeight = 360;
        FontFamily = Tokens.UiFont;
        FontSize = Tokens.FontSizeBody;
        Foreground = Tokens.TextPrimary;
        Background = Tokens.Surface0;
        UseLayoutRounding = true;
        Icon = PanelAssets.WindowIcon();

        var body = new StackPanel { Margin = new Thickness(14, 10, 14, 14), Spacing = 6 };
        body.Children.Add(Heading("Servers"));
        body.Children.Add(_servers);
        body.Children.Add(Heading("Blocked addresses"));
        body.Children.Add(BuildBanRow());
        body.Children.Add(_blocked);
        body.Children.Add(Heading("Always allowed"));
        body.Children.Add(_allowed);
        body.Children.Add(Heading("Recent incidents"));
        body.Children.Add(_incidents);

        var root = new DockPanel();
        var top = BuildTop();
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);
        var bottom = BuildBottom();
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bottom);
        root.Children.Add(new ScrollViewer { Content = body });
        Content = root;

        _timer.Tick += async (_, _) => await RefreshAsync().ConfigureAwait(true);
        Activated += (_, _) => Motion.Shared.SetFocused(true);
        Deactivated += (_, _) => Motion.Shared.SetFocused(false);
        Opened += async (_, _) =>
        {
            _timer.Start();
            await RefreshAsync().ConfigureAwait(true);
        };
        Closed += (_, _) =>
        {
            _timer.Stop();
            s_instance = null;
        };
    }

    public static void Toggle(InstallPaths paths)
    {
        if (s_instance is null)
        {
            s_instance = new SecurityWindow(paths);
            s_instance.Show();
        }
        else
        {
            s_instance.Close();
        }
    }

    private static TextBlock Heading(string text) =>
        new() { Text = text, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 8, 0, 2) };

    private static Border BuildTop()
    {
        var sea = new Avalonia.Controls.Panel { Height = 30 };
        sea.Children.Add(new SeaHost(new HeaderSeaHandler(), Motion.Shared));
        var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        title.Children.Add(Tokens.Glyphs(Glyph.Shield, 12, Tokens.Accent));
        title.Children.Add(new TextBlock { Text = "Connection guard", FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        sea.Children.Add(title);
        return new Border { Background = Tokens.Surface1, BorderBrush = Tokens.BorderSubtle, BorderThickness = new Thickness(0, 0, 0, 1), Child = sea };
    }

    private Border BuildBottom()
    {
        DockPanel.SetDock(_flash, Dock.Right);
        var dock = new DockPanel { LastChildFill = true };
        dock.Children.Add(_flash);
        dock.Children.Add(_folder);
        return new Border { Background = Tokens.Surface1, BorderBrush = Tokens.BorderSubtle, BorderThickness = new Thickness(0, 1, 0, 0), Height = Tokens.StatusBarHeight, Padding = new Thickness(12, 0), Child = dock };
    }

    private StackPanel BuildBanRow()
    {
        var ban = Tokens.ToolbarButton(Glyph.Shield, "Ban", "Block this address on every server");
        ban.Classes.Add("secondary");
        ban.Click += async (_, _) => await BanAsync().ConfigureAwait(true);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 0, 0, 4) };
        row.Children.Add(_address);
        row.Children.Add(_length);
        row.Children.Add(ban);
        return row;
    }

    private async Task RefreshAsync()
    {
        if (_refreshing)
        {
            return;
        }

        _refreshing = true;
        try
        {
            var overview = await Task.Run(() => SecurityState.Read(_paths, DateTimeOffset.UtcNow)).ConfigureAwait(true);
            Show(overview);
        }
        catch (HostingException ex)
        {
            Flash(ex.Summary, Tokens.Danger);
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void Show(SecurityOverview overview)
    {
        _folder.Text = overview.Folder;
        _servers.Children.Clear();
        if (overview.Servers.Count == 0)
        {
            _servers.Children.Add(Tokens.Caption("No server has reported yet. Start the servers to see their guard."));
        }

        foreach (var server in overview.Servers)
        {
            _servers.Children.Add(ServerRow(server));
        }

        _blocked.Children.Clear();
        if (overview.Blocked.Count == 0)
        {
            _blocked.Children.Add(Tokens.Caption("Nothing is blocked."));
        }

        foreach (var blocked in overview.Blocked.OrderBy(b => b.Until ?? DateTimeOffset.MaxValue))
        {
            _blocked.Children.Add(BlockedRow(blocked));
        }

        _allowed.Text = "loopback"
            + string.Concat(overview.Trusted.Select(text => ", " + text + " (settings)"))
            + string.Concat(overview.Allowed.Select(entry => ", " + entry.Address));

        _incidents.Children.Clear();
        if (overview.Incidents.Count == 0)
        {
            _incidents.Children.Add(Tokens.Caption("No incidents."));
        }

        foreach (var incident in overview.Incidents)
        {
            _incidents.Children.Add(new TextBlock
            {
                Text = $"{incident.At.ToLocalTime():HH:mm:ss}  {incident.Server}  {incident.Address}  {incident.Kind}: {incident.Detail}",
                FontFamily = Tokens.MonoFont,
                FontSize = 11.5,
                Foreground = incident.Kind == "ban" ? Tokens.Warning : Tokens.TextSecondary,
                TextWrapping = TextWrapping.Wrap,
            });
        }
    }

    private static Grid ServerRow(GuardServer server)
    {
        var snapshot = server.Snapshot;
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("110,200,*") };
        var name = new TextBlock { Text = snapshot.Server, FontWeight = FontWeight.SemiBold };
        var state = new TextBlock
        {
            Text = server.Running
                ? string.Create(CultureInfo.InvariantCulture, $"{snapshot.Connections} connected, {snapshot.Pending} logging in")
                : "not running",
            Foreground = server.Running ? Tokens.TextPrimary : Tokens.TextMuted,
        };
        var counts = Counters
            .Where(counter => snapshot.Counters.GetValueOrDefault(counter.Key) > 0)
            .Select(counter => string.Create(CultureInfo.InvariantCulture, $"{counter.Label} {snapshot.Counters[counter.Key]}"))
            .ToList();
        var detail = Tokens.Caption(counts.Count == 0 ? "nothing refused" : string.Join(" · ", counts));
        detail.TextWrapping = TextWrapping.Wrap;
        Grid.SetColumn(state, 1);
        Grid.SetColumn(detail, 2);
        grid.Children.Add(name);
        grid.Children.Add(state);
        grid.Children.Add(detail);
        return grid;
    }

    private Border BlockedRow(BlockedAddress blocked)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("170,*,Auto") };
        var address = new TextBlock { Text = blocked.Address, FontFamily = Tokens.MonoFont, VerticalAlignment = VerticalAlignment.Center };
        var until = blocked.Until is { } end ? $"until {end.ToLocalTime():yyyy-MM-dd HH:mm}" : "until unbanned";
        var source = blocked.Manual ? "manual" : "automatic, " + string.Join(", ", blocked.Servers);
        var reason = string.IsNullOrWhiteSpace(blocked.Reason) ? "" : " · " + blocked.Reason;
        var detail = Tokens.Caption($"{until} · {source}{reason}");
        detail.TextTrimming = TextTrimming.CharacterEllipsis;
        var unban = new Button { Content = "Unban", Height = 24, MinHeight = 0, Padding = new Thickness(10, 0), VerticalContentAlignment = VerticalAlignment.Center, Foreground = Tokens.TextPrimary };
        unban.Classes.Add("secondary");
        unban.Click += async (_, _) => await UnbanAsync(blocked.Address).ConfigureAwait(true);
        Grid.SetColumn(detail, 1);
        Grid.SetColumn(unban, 2);
        grid.Children.Add(address);
        grid.Children.Add(detail);
        grid.Children.Add(unban);
        return new Border { Background = Tokens.Surface1, CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 4), Child = grid };
    }

    private async Task BanAsync()
    {
        var text = _address.Text?.Trim() ?? "";
        var length = BanLengths[Math.Max(0, _length.SelectedIndex)].Length;
        try
        {
            var ban = await Task.Run(() => SecurityState.Ban(_paths, text, length, "control panel", DateTimeOffset.UtcNow)).ConfigureAwait(true);
            _address.Text = "";
            Flash($"Banned {ban.Address}", Tokens.Success);
        }
        catch (HostingException ex)
        {
            Flash(ex.Summary, Tokens.Danger);
        }

        await RefreshAsync().ConfigureAwait(true);
    }

    private async Task UnbanAsync(string address)
    {
        try
        {
            await Task.Run(() => SecurityState.Unban(_paths, address, DateTimeOffset.UtcNow)).ConfigureAwait(true);
            Flash($"Unbanned {address}", Tokens.Success);
        }
        catch (HostingException ex)
        {
            Flash(ex.Summary, Tokens.Danger);
        }

        await RefreshAsync().ConfigureAwait(true);
    }

    private void Flash(string text, IBrush brush)
    {
        _flash.Text = text;
        _flash.Foreground = brush;
        DispatcherTimer.RunOnce(() => _flash.Text = "", TimeSpan.FromSeconds(4));
    }
}
