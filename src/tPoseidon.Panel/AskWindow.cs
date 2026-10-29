using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace tPoseidon.Panel;

public sealed class AskWindow : Window
{
    private bool? _answer;

    private AskWindow(string question, string detail, string yes, string? no)
    {
        Title = "tPoseidon";
        Width = 440;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        FontFamily = Tokens.UiFont;
        FontSize = Tokens.FontSizeBody;
        Foreground = Tokens.TextPrimary;
        Background = Tokens.Surface0;
        Icon = PanelAssets.WindowIcon();

        var yesButton = new Button { Content = yes, Height = 32, MinHeight = 0, Padding = new Thickness(18, 0), VerticalContentAlignment = VerticalAlignment.Center, FontWeight = FontWeight.SemiBold };
        yesButton.Classes.Add("primary");
        yesButton.Click += (_, _) => Answer(true);
        var cancel = new Button { Content = "Cancel", Height = 32, MinHeight = 0, Padding = new Thickness(18, 0), VerticalContentAlignment = VerticalAlignment.Center };
        cancel.Classes.Add("flat");
        cancel.Click += (_, _) => Answer(null);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(cancel);
        if (no is not null)
        {
            var noButton = new Button { Content = no, Height = 32, MinHeight = 0, Padding = new Thickness(18, 0), VerticalContentAlignment = VerticalAlignment.Center };
            noButton.Classes.Add("secondary");
            noButton.Click += (_, _) => Answer(false);
            buttons.Children.Add(noButton);
        }

        buttons.Children.Add(yesButton);

        var body = new StackPanel { Margin = new Thickness(22, 18, 22, 16), Spacing = 8 };
        body.Children.Add(new TextBlock { Text = question, FontSize = 15, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        body.Children.Add(new TextBlock { Text = detail, Foreground = Tokens.TextSecondary, TextWrapping = TextWrapping.Wrap });
        var footer = new Border { Padding = new Thickness(22, 12), Background = Tokens.Surface1, BorderBrush = Tokens.BorderSubtle, BorderThickness = new Thickness(0, 1, 0, 0), Child = buttons };
        var stack = new StackPanel();
        stack.Children.Add(body);
        stack.Children.Add(footer);
        Content = stack;
    }

    public static async Task<bool?> ShowAsync(Window? owner, string question, string detail, string yes, string no)
    {
        var window = new AskWindow(question, detail, yes, no);
        await window.PresentAsync(owner).ConfigureAwait(true);
        return window._answer;
    }

    public static async Task<bool> ConfirmAsync(Window? owner, string question, string detail, string yes)
    {
        var window = new AskWindow(question, detail, yes, null);
        await window.PresentAsync(owner).ConfigureAwait(true);
        return window._answer == true;
    }

    private Task PresentAsync(Window? owner)
    {
        if (owner is not null)
        {
            return ShowDialog(owner);
        }

        var closed = new TaskCompletionSource();
        Closed += (_, _) => closed.SetResult();
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = true;
        Show();
        return closed.Task;
    }

    private void Answer(bool? answer)
    {
        _answer = answer;
        Close();
    }
}
