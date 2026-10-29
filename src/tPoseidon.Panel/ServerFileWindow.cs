using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using tPoseidon.Hosting;

namespace tPoseidon.Panel;

public sealed class ServerFileWindow : Window
{
    private readonly HostSettings _settings;
    private readonly TextBox _name = Field("Name players see");
    private readonly TextBox _webUrl = Field("Web address players log in through");
    private readonly TextBox _note = Field("Note (optional)");
    private readonly TextBox _color = Field("Accent colour, like #3B82F6 (optional)");
    private readonly Banner _banner = new();
    private readonly Button _save = new() { Content = "Save server file...", Height = 32, MinHeight = 0, Padding = new Thickness(18, 0), VerticalContentAlignment = VerticalAlignment.Center, FontWeight = FontWeight.SemiBold };

    public ServerFileWindow(HostSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Title = "Share your server";
        Width = 480;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = Tokens.UiFont;
        FontSize = Tokens.FontSizeBody;
        Foreground = Tokens.TextPrimary;
        Background = Tokens.Surface0;
        Icon = PanelAssets.WindowIcon();
        _name.Text = _settings.Launch.WorldName;
        _webUrl.Text = $"http://{_settings.Network.PublicAddress}:{_settings.Ports.Shop}";
        _note.MaxLength = ServerFile.MaxNoteLength;
        _save.Classes.Add("primary");
        _save.Click += async (_, _) => await SaveAsync().ConfigureAwait(true);

        var body = new StackPanel { Margin = new Thickness(22, 18, 22, 18), Spacing = 8 };
        body.Children.Add(new TextBlock { Text = "Server file for your friends", FontSize = 15, FontWeight = FontWeight.SemiBold });
        body.Children.Add(new TextBlock
        {
            Text = $"Friends open this file in the tPoseidon launcher to add your server at {_settings.Network.PublicAddress}. Use an https:// web address when the web port is behind HTTPS.",
            Foreground = Tokens.TextSecondary,
            TextWrapping = TextWrapping.Wrap,
        });
        body.Children.Add(Labelled("Name", _name));
        body.Children.Add(Labelled("Web address", _webUrl));
        body.Children.Add(Labelled("Note", _note));
        body.Children.Add(Labelled("Colour", _color));
        body.Children.Add(_banner);
        body.Children.Add(new Border { HorizontalAlignment = HorizontalAlignment.Right, Child = _save });
        Content = body;
    }

    public TextBox NameBox => _name;

    public TextBox WebUrlBox => _webUrl;

    public TextBox NoteBox => _note;

    public TextBox ColorBox => _color;

    public Banner Notice => _banner;

    public Button SaveButton => _save;

    public ServerFile? Build()
    {
        try
        {
            var file = ServerFile.ForServer(_settings, _name.Text ?? "", _webUrl.Text, _note.Text, _color.Text);
            _banner.Hide();
            return file;
        }
        catch (HostingException ex)
        {
            _banner.Show(BannerKind.Error, ex.Summary, ex.Hint);
            return null;
        }
    }

    public bool Write(ServerFile file, string path)
    {
        try
        {
            file.Write(path);
        }
        catch (HostingException ex)
        {
            _banner.Show(BannerKind.Error, ex.Summary, ex.Hint);
            return false;
        }

        _banner.Show(BannerKind.Success, "Server file saved", path);
        return true;
    }

    private static TextBox Field(string watermark) =>
        new() { Watermark = watermark, Height = 30, MinHeight = 0, VerticalContentAlignment = VerticalAlignment.Center };

    private static StackPanel Labelled(string label, Control field)
    {
        var stack = new StackPanel { Spacing = 3 };
        stack.Children.Add(Tokens.Caption(label));
        stack.Children.Add(field);
        return stack;
    }

    private async Task SaveAsync()
    {
        if (Build() is not { } file)
        {
            return;
        }

        var target = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save the server file",
            SuggestedFileName = file.Name + ServerFile.Extension,
            DefaultExtension = ServerFile.Extension.TrimStart('.'),
            FileTypeChoices = [new FilePickerFileType("tPoseidon server file") { Patterns = ["*" + ServerFile.Extension] }],
        }).ConfigureAwait(true);
        if (target?.TryGetLocalPath() is { } path)
        {
            Write(file, path);
        }
    }
}
