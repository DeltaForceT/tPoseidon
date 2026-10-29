using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace tPoseidon.Ui;

public static class NewsCard
{
    public const double PictureMaxHeight = 180;

    public static Border Create(string title, string date, string text, bool pinned, IImage? picture)
    {
        var heading = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        heading.Children.Add(new TextBlock { Text = title, FontWeight = FontWeight.SemiBold, FontSize = 13 });
        heading.Children.Add(Tokens.Caption(date));
        if (pinned)
        {
            heading.Children.Add(new Border
            {
                Background = Tokens.AccentSoft,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 1),
                Child = new TextBlock { Text = "Pinned", FontSize = Tokens.FontSizeCaption, Foreground = Tokens.Accent },
            });
        }

        var body = new StackPanel { Spacing = 4 };
        body.Children.Add(heading);
        body.Children.Add(new TextBlock { Text = text, Foreground = Tokens.TextSecondary, TextWrapping = TextWrapping.Wrap });
        if (picture is not null)
        {
            body.Children.Add(new Image { Source = picture, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, MaxHeight = PictureMaxHeight, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 0) });
        }

        return new Border
        {
            Background = Tokens.Surface1,
            BorderBrush = Tokens.BorderSubtle,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 10),
            Child = body,
        };
    }
}
