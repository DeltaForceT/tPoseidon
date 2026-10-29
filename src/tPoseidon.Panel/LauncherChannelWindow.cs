using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using tPoseidon.Hosting;

namespace tPoseidon.Panel;

public enum ChannelSection
{
    News,
    Pictures,
    Maintenance,
    Files,
    Publish,
    History,
}

public sealed class LauncherChannelWindow : Window
{
    private static readonly (ChannelSection Section, string Title)[] Sections =
    [
        (ChannelSection.News, "News"),
        (ChannelSection.Pictures, "Banner"),
        (ChannelSection.Maintenance, "Maintenance"),
        (ChannelSection.Files, "Client files"),
        (ChannelSection.Publish, "Publish"),
        (ChannelSection.History, "History"),
    ];

    private readonly Banner _notice = new();
    private readonly Dictionary<ChannelSection, Control> _pages = [];
    private readonly Dictionary<ChannelSection, Button> _buttons = [];

    public LauncherChannelWindow(InstallPaths paths, HostSettings settings)
    {
        Context = new ChannelContext(paths, settings, _notice);
        Title = "Launcher updates";
        Width = 1040;
        Height = 700;
        MinWidth = 900;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = Tokens.UiFont;
        FontSize = Tokens.FontSizeBody;
        Foreground = Tokens.TextPrimary;
        Background = Tokens.Surface0;
        Icon = PanelAssets.WindowIcon();
        News = new ChannelNewsPage(Context);
        Pictures = new ChannelPicturesPage(Context);
        Maintenance = new ChannelMaintenancePage(Context);
        Files = new ChannelFilesPage(Context);
        Publish = new ChannelPublishPage(Context);
        History = new ChannelHistoryPage(Context);
        _pages[ChannelSection.News] = News;
        _pages[ChannelSection.Pictures] = Pictures;
        _pages[ChannelSection.Maintenance] = Maintenance;
        _pages[ChannelSection.Files] = Files;
        _pages[ChannelSection.Publish] = Publish;
        _pages[ChannelSection.History] = History;
        foreach (var (key, page) in _pages)
        {
            page.IsVisible = key == Current;
        }

        Content = BuildLayout();
        Opened += async (_, _) => await ShowAsync(ChannelSection.News).ConfigureAwait(true);
    }

    public ChannelContext Context { get; }

    public ChannelNewsPage News { get; }

    public ChannelPicturesPage Pictures { get; }

    public ChannelMaintenancePage Maintenance { get; }

    public ChannelFilesPage Files { get; }

    public ChannelPublishPage Publish { get; }

    public ChannelHistoryPage History { get; }

    public Banner Notice => _notice;

    public ChannelSection Current { get; private set; } = ChannelSection.News;

    public bool PageVisible(ChannelSection section) => _pages[section].IsVisible;

    public async Task ShowAsync(ChannelSection section)
    {
        Current = section;
        foreach (var (key, page) in _pages)
        {
            page.IsVisible = key == section;
        }

        foreach (var (key, button) in _buttons)
        {
            button.FontWeight = key == section ? FontWeight.Bold : FontWeight.Normal;
        }

        switch (section)
        {
            case ChannelSection.Files:
                await Files.RefreshAsync().ConfigureAwait(true);
                break;
            case ChannelSection.Publish:
                await Publish.RefreshAsync().ConfigureAwait(true);
                break;
            case ChannelSection.History:
                History.Reload();
                break;
            case ChannelSection.Pictures:
                Pictures.Reload();
                break;
            case ChannelSection.Maintenance:
                Maintenance.Reload();
                break;
            default:
                News.Reload(News.EditingId);
                break;
        }
    }

    private Grid BuildLayout()
    {
        var nav = new StackPanel { Spacing = 4, Margin = new Thickness(16, 20) };
        nav.Children.Add(new TextBlock { Text = "Launcher updates", FontSize = 16, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
        foreach (var (section, title) in Sections)
        {
            var button = new Button { Content = title, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Height = 34, MinHeight = 0 };
            button.Classes.Add("flat");
            button.Click += async (_, _) => await ShowAsync(section).ConfigureAwait(true);
            _buttons[section] = button;
            nav.Children.Add(button);
        }

        var content = new StackPanel { Spacing = 14, Margin = new Thickness(24, 20, 24, 16) };
        foreach (var page in _pages.Values)
        {
            content.Children.Add(page);
        }

        content.Children.Add(_notice);
        var scroll = new ScrollViewer { Content = content, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(190)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        Grid.SetColumn(scroll, 1);
        grid.Children.Add(new Border { Background = Tokens.Surface1, BorderBrush = Tokens.BorderSubtle, BorderThickness = new Thickness(0, 0, 1, 0), Child = nav });
        grid.Children.Add(scroll);
        return grid;
    }
}
