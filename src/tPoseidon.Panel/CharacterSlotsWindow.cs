using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using tPoseidon.Core.Data;

namespace tPoseidon.Panel;

public sealed class CharacterSlotsWindow : Window
{
    private readonly PanelHost _host;
    private readonly TextBox _slots;
    private readonly TextBlock _problem = new() { Foreground = Tokens.Danger, TextWrapping = TextWrapping.Wrap, IsVisible = false };

    public CharacterSlotsWindow(PanelHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        Title = "Character slots";
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

        _slots = WizardPage.Box(host.CharacterSlots.ToString(CultureInfo.InvariantCulture), null, false, 160);
        _slots.TextChanged += (_, _) =>
        {
            _problem.IsVisible = false;
            WizardPage.Mark(_slots, false);
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
        body.Children.Add(new TextBlock { Text = "Characters each account can have", FontSize = 15, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        body.Children.Add(new TextBlock
        {
            Text = $"A whole number from {CharacterSlotRules.Min} to {CharacterSlotRules.Max}. The original servers allow {CharacterSlotRules.Default}; the game client shows {CharacterSlotRules.Max} character cards. Lowering it never removes characters: an account that already has more keeps them and cannot make another until it is under the limit. The servers read it when they start, so restart them after saving.",
            Foreground = Tokens.TextSecondary,
            TextWrapping = TextWrapping.Wrap,
        });
        body.Children.Add(_slots);
        body.Children.Add(_problem);
        var footer = new Border { Padding = new Thickness(22, 12), Background = Tokens.Surface1, BorderBrush = Tokens.BorderSubtle, BorderThickness = new Thickness(0, 1, 0, 0), Child = buttons };
        var stack = new StackPanel();
        stack.Children.Add(body);
        stack.Children.Add(footer);
        Content = stack;
    }

    private async Task SaveAsync()
    {
        if (!int.TryParse(_slots.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var count) || !await _host.ChooseCharacterSlotsAsync(count).ConfigureAwait(true))
        {
            WizardPage.Mark(_slots, true);
            _problem.Text = $"'{_slots.Text}' cannot be saved. Use a whole number from {CharacterSlotRules.Min} to {CharacterSlotRules.Max}.";
            _problem.IsVisible = true;
            return;
        }

        Close();
    }
}
