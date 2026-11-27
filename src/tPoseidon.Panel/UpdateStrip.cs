using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using tPoseidon.Hosting;

namespace tPoseidon.Panel;

public sealed class UpdateStrip : Border
{
    private const int NotesPreviewLength = 220;

    private readonly PanelUpdater _updater;
    private readonly PanelHost _host;
    private readonly Window _owner;
    private readonly TextBlock _title = new() { FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _detail = new() { Foreground = Tokens.TextSecondary, TextWrapping = TextWrapping.Wrap, FontSize = Tokens.FontSizeCaption };
    private readonly Button _now = new() { Content = "Update now", Height = 28, MinHeight = 0, Padding = new Thickness(14, 0), VerticalContentAlignment = VerticalAlignment.Center };
    private readonly Button _later = new() { Content = "Later", Height = 28, MinHeight = 0, Padding = new Thickness(14, 0), VerticalContentAlignment = VerticalAlignment.Center };
    private readonly Button _stop = new() { Content = "Cancel update", Height = 28, MinHeight = 0, Padding = new Thickness(14, 0), VerticalContentAlignment = VerticalAlignment.Center };

    public UpdateStrip(PanelUpdater updater, PanelHost host, Window owner)
    {
        _updater = updater ?? throw new ArgumentNullException(nameof(updater));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Margin = new Thickness(12);
        VerticalAlignment = VerticalAlignment.Top;
        MaxWidth = 620;
        CornerRadius = new CornerRadius(6);
        Padding = new Thickness(12, 10);
        BorderThickness = new Thickness(1);
        Background = Tokens.InfoTint;
        BorderBrush = Tokens.Accent;
        IsVisible = false;

        _now.Classes.Add("primary");
        _later.Classes.Add("flat");
        _stop.Classes.Add("secondary");
        _now.Click += async (_, _) => await ConfirmAndInstallAsync().ConfigureAwait(true);
        _later.Click += (_, _) => _updater.Dismiss();
        _stop.Click += (_, _) => _updater.CancelRunning();

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 8, 0, 0) };
        buttons.Children.Add(_now);
        buttons.Children.Add(_later);
        buttons.Children.Add(_stop);
        var text = new StackPanel { Spacing = 3 };
        text.Children.Add(_title);
        text.Children.Add(_detail);
        text.Children.Add(buttons);
        Child = text;
        _updater.Changed += Refresh;
        Refresh();
    }

    public void Refresh()
    {
        var running = _updater.Running;
        IsVisible = running || _updater.Visible;
        if (!IsVisible)
        {
            return;
        }

        if (running)
        {
            _title.Text = _updater.Status ?? "Updating...";
            _detail.Text = "";
        }
        else if (_updater.Offer is { } offer)
        {
            _title.Text = $"tPoseidon {offer.Version} is available";
            _detail.Text = Preview(offer.Release.Notes) + (_updater.Mode == UpdateMode.NotifyOnly
                ? " Install it from the command line with 'tposeidon update install', or choose one-click updates in Updates > Update settings."
                : "");
        }

        _detail.IsVisible = _detail.Text?.Length > 0;
        _now.IsVisible = !running && _updater.Mode != UpdateMode.NotifyOnly;
        _later.IsVisible = !running;
        _stop.IsVisible = running;
    }

    private static string Preview(string notes)
    {
        var flat = string.Join(" ", notes.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return flat.Length <= NotesPreviewLength ? flat : flat[..NotesPreviewLength] + "...";
    }

    private async Task ConfirmAndInstallAsync()
    {
        if (_updater.Offer is not { } offer || _host.Settings is not { } settings)
        {
            return;
        }

        var running = _host.AnyServerRunning();
        var steps = running
            ? (settings.Updates.CanSendNotice
                ? $"Players get an in-game notice, and after {settings.Updates.CountdownMinutes} minute(s) the servers stop."
                : "No game master character is set for notices, so the servers stop at once. You can set one in Updates > Update settings.")
              + " The database is backed up, the program files are replaced, and the servers start again."
            : "The program files are replaced. The servers are stopped, so they are not started.";
        var answer = await AskWindow.ShowAsync(
            _owner,
            $"Update to tPoseidon {offer.Version}?",
            steps + " The panel closes and opens again by itself. If the new version does not start, the earlier version and the backup come back.",
            "Update now",
            "Not now").ConfigureAwait(true);
        if (answer == true)
        {
            await _updater.InstallAsync().ConfigureAwait(true);
        }
    }
}
