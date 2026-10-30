using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using tPoseidon.Core.Data;

namespace tPoseidon.Panel;

public sealed class GuildCoinsWindow : Window
{
    private readonly PanelHost _host;
    private readonly TextBox _coins;
    private readonly TextBlock _problem = new() { Foreground = Tokens.Danger, TextWrapping = TextWrapping.Wrap, IsVisible = false };

    public GuildCoinsWindow(PanelHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        Title = "Guild coins";
        Width = 440;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = Tokens.UiFont;
        FontSize = Tokens.FontSizeBody;
        Foreground = Tokens.TextPrimary;
        Background = Tokens.Surface0;
        Icon = PanelAssets.WindowIcon();

        _coins = WizardPage.Box(host.GuildCoinsPerWin.ToString(CultureInfo.InvariantCulture), null, false, 160);
        _coins.TextChanged += (_, _) =>
        {
            _problem.IsVisible = false;
            WizardPage.Mark(_coins, false);
        };
        var save = new Button { Content = "Save", Height = 32, MinHeight = 0, Padding = new Thickness(18, 0), VerticalContentAlignment = VerticalAlignment.Center, FontWeight = FontWeight.SemiBold };
        save.Classes.Add("primary");
        save.Click += async (_, _) => await SaveAsync().ConfigureAwait(true);
        var cancel = new Button { Content = "Cancel", Height = 32, MinHeight = 0, Padding = new Thickness(18, 0), VerticalContentAlignment = VerticalAlignment.Center };
        cancel.Classes.Add("flat");
        cancel.Click += (_, _) => Close();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(cancel);
        buttons.Children.Add(save);

        var body = new StackPanel { Margin = new Thickness(22, 18, 22, 16), Spacing = 8 };
        body.Children.Add(new TextBlock { Text = "Coins a guild earns for each guild game it wins", FontSize = 15, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        body.Children.Add(new TextBlock
        {
            Text = $"A whole number from 0 to {GuildCoinRules.MaxPerWin}. 0 gives none. The servers read it when they start, so restart them after saving.",
            Foreground = Tokens.TextSecondary,
            TextWrapping = TextWrapping.Wrap,
        });
        body.Children.Add(_coins);
        body.Children.Add(_problem);
        var footer = new Border { Padding = new Thickness(22, 12), Background = Tokens.Surface1, BorderBrush = Tokens.BorderSubtle, BorderThickness = new Thickness(0, 1, 0, 0), Child = buttons };
        var stack = new StackPanel();
        stack.Children.Add(body);
        stack.Children.Add(footer);
        Content = stack;
    }

    private async Task SaveAsync()
    {
        if (!int.TryParse(_coins.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var amount) || !await _host.ChooseGuildCoinsPerWinAsync(amount).ConfigureAwait(true))
        {
            WizardPage.Mark(_coins, true);
            _problem.Text = $"'{_coins.Text}' cannot be saved. Use a whole number from 0 to {GuildCoinRules.MaxPerWin}.";
            _problem.IsVisible = true;
            return;
        }

        Close();
    }
}
