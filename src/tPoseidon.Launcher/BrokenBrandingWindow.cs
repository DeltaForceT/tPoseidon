using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using tPoseidon.Hosting;
using tPoseidon.Ui;

namespace tPoseidon.Launcher;

public sealed class BrokenBrandingWindow : Window
{
    public BrokenBrandingWindow(HostingException problem)
    {
        ArgumentNullException.ThrowIfNull(problem);
        Title = "Launcher";
        Width = 460;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = Tokens.UiFont;
        Foreground = Tokens.TextPrimary;
        Background = Tokens.Surface0;
        var close = new Button { Content = "Close", HorizontalAlignment = HorizontalAlignment.Right, Padding = new Thickness(20, 6) };
        close.Click += (_, _) => Close();
        var banner = new Banner();
        banner.Show(BannerKind.Error, "This launcher cannot start", problem.Hint is null ? problem.Summary : $"{problem.Summary} {problem.Hint}");
        var body = new StackPanel { Margin = new Thickness(22), Spacing = 14 };
        body.Children.Add(banner);
        body.Children.Add(close);
        Content = body;
    }
}
