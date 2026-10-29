using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using tPoseidon.Ui;

namespace tPoseidon.Launcher.Installer;

public sealed class SetupWindow : Window
{
    private readonly Button _action = new() { Height = 36, MinHeight = 0, Padding = new Thickness(26, 0), FontWeight = FontWeight.SemiBold, VerticalContentAlignment = VerticalAlignment.Center };
    private readonly Button _close = new() { Content = "Close", Height = 36, MinHeight = 0, Padding = new Thickness(20, 0), VerticalContentAlignment = VerticalAlignment.Center };
    private readonly Button? _after;
    private readonly ProgressBar _progress = new() { Minimum = 0, Maximum = 1, Height = 6, MinHeight = 0, IsVisible = false };
    private readonly Banner _banner = new();
    private readonly Func<IProgress<double>, string> _run;
    private readonly Action? _afterAction;

    public SetupWindow(string title, byte[]? logo, byte[]? icon, string accent, string description, string folder, string actionLabel, Func<IProgress<double>, string> run, (string Label, Action Action)? after)
    {
        ArgumentNullException.ThrowIfNull(run);
        _run = run;
        Title = title;
        Width = 480;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = Tokens.UiFont;
        FontSize = Tokens.FontSizeBody;
        Foreground = Tokens.TextPrimary;
        Background = Tokens.Surface0;
        if (icon is not null)
        {
            Icon = new WindowIcon(new MemoryStream(icon));
        }

        var color = Color.TryParse(accent, out var parsed) ? parsed : Color.Parse("#5B9DFF");
        _action.Content = actionLabel;
        foreach (var key in new[] { "ButtonBackground", "ButtonBackgroundPointerOver", "ButtonBackgroundPressed" })
        {
            _action.Resources[key] = new SolidColorBrush(color);
        }

        foreach (var key in new[] { "ButtonForeground", "ButtonForegroundPointerOver", "ButtonForegroundPressed" })
        {
            _action.Resources[key] = (0.299 * color.R) + (0.587 * color.G) + (0.114 * color.B) > 150 ? Tokens.OnAccent : Brushes.White;
        }

        _progress.Foreground = new SolidColorBrush(color);
        _action.Click += (_, _) => Start();
        _close.Click += (_, _) => Close();
        if (after is { } follow)
        {
            _afterAction = follow.Action;
            _after = new Button { Content = follow.Label, Height = 36, MinHeight = 0, Padding = new Thickness(20, 0), VerticalContentAlignment = VerticalAlignment.Center, IsVisible = false };
            _after.Click += (_, _) =>
            {
                _afterAction?.Invoke();
                Close();
            };
        }

        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        if (logo is not null)
        {
            head.Children.Add(new Image { Source = new Bitmap(new MemoryStream(logo)), Width = 44, Height = 44 });
        }

        head.Children.Add(new TextBlock { Text = title, FontSize = 18, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center });

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        if (_after is not null)
        {
            buttons.Children.Add(_after);
        }

        buttons.Children.Add(_action);
        buttons.Children.Add(_close);

        var body = new StackPanel { Margin = new Thickness(24, 20), Spacing = 12 };
        body.Children.Add(head);
        body.Children.Add(new TextBlock { Text = description, Foreground = Tokens.TextSecondary, TextWrapping = TextWrapping.Wrap });
        body.Children.Add(Tokens.Caption(folder));
        body.Children.Add(_progress);
        body.Children.Add(_banner);
        body.Children.Add(buttons);
        Content = body;
    }

    internal Button ActionButton => _action;

    internal Banner Result => _banner;

    public static SetupWindow Failure(string message)
    {
        var window = new SetupWindow("Setup", null, null, "#5B9DFF", "", "", "Close", _ => "", null);
        window._action.IsVisible = false;
        window._banner.Show(BannerKind.Error, message);
        return window;
    }

    private void Start()
    {
        _action.IsEnabled = false;
        _close.IsEnabled = false;
        _progress.IsVisible = true;
        _banner.Hide();
        var progress = new Progress<double>(value => _progress.Value = value);
        Task.Run(() => _run(progress)).ContinueWith(task => Dispatcher.UIThread.Post(() => Finish(task)), TaskScheduler.Default);
    }

    private void Finish(Task<string> task)
    {
        _close.IsEnabled = true;
        _progress.IsVisible = false;
        if (task.IsCompletedSuccessfully)
        {
            _action.IsVisible = false;
            _banner.Show(BannerKind.Success, task.Result);
            if (_after is not null)
            {
                _after.IsVisible = true;
            }

            return;
        }

        var error = task.Exception?.GetBaseException();
        if (error is InstallerException or IOException or UnauthorizedAccessException)
        {
            _action.IsEnabled = true;
            _banner.Show(BannerKind.Error, error.Message);
            return;
        }

        throw task.Exception!;
    }
}
