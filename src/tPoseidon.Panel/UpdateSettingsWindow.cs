using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using tPoseidon.Hosting;
using tPoseidon.Hosting.Updates;

namespace tPoseidon.Panel;

public sealed class UpdateSettingsWindow : Window
{
    private static readonly string[] ModeLabels =
    [
        "Tell me when an update is out; I install it myself",
        "Tell me, and let me install it with one click (recommended)",
        "Install it by itself at a set hour",
    ];

    private static readonly string[] ChannelLabels = ["Stable releases only", "Beta releases too"];

    private readonly PanelHost _host;
    private readonly ComboBox _mode = Combo(480, ModeLabels);
    private readonly ComboBox _hour = Combo(120, Enumerable.Range(0, 24).Select(hour => $"{hour:00}:00"));
    private readonly ComboBox _channel = Combo(260, ChannelLabels);
    private readonly TextBox _countdown;
    private readonly TextBox _character;
    private readonly TextBox _password;
    private readonly TextBlock _problem = new() { Foreground = Tokens.Danger, TextWrapping = TextWrapping.Wrap, IsVisible = false };
    private readonly Control _hourRow;

    public UpdateSettingsWindow(PanelHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        var current = host.Settings?.Updates ?? new UpdateSettings();
        Title = "Updates";
        Width = 540;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = Tokens.UiFont;
        FontSize = Tokens.FontSizeBody;
        Foreground = Tokens.TextPrimary;
        Background = Tokens.Surface0;
        Icon = PanelAssets.WindowIcon();

        _mode.SelectedIndex = (int)current.Mode;
        _hour.SelectedIndex = current.AutomaticHour;
        _channel.SelectedIndex = (int)current.Channel;
        _countdown = WizardPage.Box(current.CountdownMinutes.ToString(CultureInfo.InvariantCulture), null, false, 100);
        _character = WizardPage.Box(current.NoticeCharacter, "GM character", false, 220);
        _password = WizardPage.Box(current.NoticePassword, "Account password", true, 220);
        _hourRow = Row("Hour", _hour);
        _mode.SelectionChanged += (_, _) => _hourRow.IsVisible = _mode.SelectedIndex == (int)UpdateMode.Automatic;
        _hourRow.IsVisible = _mode.SelectedIndex == (int)UpdateMode.Automatic;

        var save = new Button { Content = "Save", Height = 32, MinHeight = 0, Padding = new Thickness(18, 0), VerticalContentAlignment = VerticalAlignment.Center, FontWeight = FontWeight.SemiBold };
        save.Classes.Add("primary");
        save.Click += async (_, _) => await SaveAsync().ConfigureAwait(true);
        var cancel = new Button { Content = "Cancel", Height = 32, MinHeight = 0, Padding = new Thickness(18, 0), VerticalContentAlignment = VerticalAlignment.Center };
        cancel.Classes.Add("flat");
        cancel.Click += (_, _) => Close();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(cancel);
        buttons.Children.Add(save);

        var body = new StackPanel { Margin = new Thickness(22, 18, 22, 16), Spacing = 10 };
        body.Children.Add(new TextBlock { Text = "Updating tPoseidon", FontSize = 15, FontWeight = FontWeight.SemiBold });
        body.Children.Add(Note("Every update is checked against the tPoseidon release signature before anything is changed. Your settings, database, server data, logs and backups are never replaced, and the database is backed up first."));
        body.Children.Add(Row("How", _mode));
        body.Children.Add(_hourRow);
        body.Children.Add(Row("Channel", _channel));
        body.Children.Add(Note("Before the servers stop, players get an in-game notice with a countdown. It needs a registered game master character (see the GM window); without one, no notice is sent and the servers stop at once."));
        body.Children.Add(Row("Minutes", _countdown));
        body.Children.Add(Row("Notice as", _character));
        body.Children.Add(Row("Password", _password));
        body.Children.Add(_problem);
        var footer = new Border { Padding = new Thickness(22, 12), Background = Tokens.Surface1, BorderBrush = Tokens.BorderSubtle, BorderThickness = new Thickness(0, 1, 0, 0), Child = buttons };
        var stack = new StackPanel();
        stack.Children.Add(body);
        stack.Children.Add(footer);
        Content = stack;
    }

    private static ComboBox Combo(double width, IEnumerable<string> items) =>
        new() { Height = 28, MinHeight = 0, Width = width, FontSize = Tokens.FontSizeBody, ItemsSource = items.ToList() };

    private static TextBlock Note(string text) =>
        new() { Text = text, Foreground = Tokens.TextSecondary, TextWrapping = TextWrapping.Wrap };

    private static StackPanel Row(string label, Control control)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        row.Children.Add(new TextBlock { Text = label, Width = 80, VerticalAlignment = VerticalAlignment.Center, Foreground = Tokens.TextSecondary });
        row.Children.Add(control);
        return row;
    }

    private async Task SaveAsync()
    {
        var value = new UpdateSettings
        {
            Mode = (UpdateMode)Math.Max(0, _mode.SelectedIndex),
            AutomaticHour = Math.Max(0, _hour.SelectedIndex),
            Channel = (ReleaseChannel)Math.Max(0, _channel.SelectedIndex),
            NoticeCharacter = (_character.Text ?? "").Trim(),
            NoticePassword = _password.Text ?? "",
            FeedUrl = _host.Settings?.Updates.FeedUrl ?? "",
        };
        if (!int.TryParse(_countdown.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes))
        {
            Reject($"'{_countdown.Text}' is not a number of minutes.", true);
            return;
        }

        value.CountdownMinutes = minutes;
        if (value.Problems() is { Count: > 0 } problems)
        {
            Reject(problems[0], true);
            return;
        }

        if ((value.NoticeCharacter.Length == 0) != (value.NoticePassword.Length == 0))
        {
            Reject("Give both the GM character and its password, or leave both empty.", false);
            return;
        }

        if (!await _host.SaveUpdateSettingsAsync(value).ConfigureAwait(true))
        {
            Reject("The update settings could not be saved.", false);
            return;
        }

        Close();
    }

    private void Reject(string message, bool countdown)
    {
        WizardPage.Mark(_countdown, countdown);
        _problem.Text = message;
        _problem.IsVisible = true;
    }
}
