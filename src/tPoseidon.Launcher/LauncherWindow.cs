using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using tPoseidon.Hosting;
using tPoseidon.LauncherKit;
using tPoseidon.Ui;

namespace tPoseidon.Launcher;

public sealed class LauncherWindow : Window
{
    private const double FieldHeight = 32;
    private const double SideWidth = 330;
    private const double BannerHeight = 210;
    private static readonly Color DefaultAccent = Color.Parse("#5B9DFF");
    private static readonly Color Ground = Color.Parse("#14171C");
    private static readonly TimeSpan Fade = TimeSpan.FromMilliseconds(180);

    private readonly LauncherModel _model;
    private readonly OptionsView _options;
    private readonly Button _signOut = Small("Not you? Sign out");
    private readonly StackPanel _links = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
    private readonly TextBox _clientFolder = Field("Folder with Trickster.bin");
    private readonly TextBlock _clientStatus = Tokens.Caption();
    private readonly ComboBox _servers = new() { Height = FieldHeight, Width = 280, MinHeight = 0 };
    private readonly ComboBox _language = new() { Height = FieldHeight, MinHeight = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly Button _addServer = Small("Add...");
    private readonly Button _importServer = Small("Import file...");
    private readonly Button _removeServer = Small("Remove");
    private readonly Button _undo = Small("Undo last update");
    private readonly StackPanel _addForm = new() { Spacing = 8, IsVisible = false };
    private readonly TextBox _newName = Field("Name (optional)");
    private readonly TextBox _newAddress = Field("Server address, for example 203.0.113.7");
    private readonly TextBox _newWebUrl = Field("Web address (optional, for HTTPS)");
    private readonly Button _saveServer = Small("Add server");
    private readonly Button _settingsButton = Tokens.IconButton(Glyph.Settings, "Client folder, language and servers");
    private readonly Border _settingsLayer = new() { Opacity = 0, IsHitTestVisible = false };
    private readonly TextBox _account = Field("Account name");
    private readonly TextBox _password = Field("Password");
    private readonly Button _play = new()
    {
        Content = "Play",
        Height = 48,
        MinHeight = 0,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        HorizontalContentAlignment = HorizontalAlignment.Center,
        VerticalContentAlignment = VerticalAlignment.Center,
        FontWeight = FontWeight.Bold,
        FontSize = 18,
        CornerRadius = new CornerRadius(8),
    };
    private readonly ProgressBar _progress = new() { Minimum = 0, Maximum = 1, Height = 6, MinHeight = 0, Opacity = 0 };
    private readonly TextBlock _progressText = Tokens.Caption();
    private readonly TextBlock _updateSummary = Tokens.Caption();
    private readonly Banner _banner = new();
    private readonly StackPanel _signIn = new() { Spacing = 10 };
    private readonly Border _bannerArt = new() { Height = BannerHeight };
    private readonly Image _bannerImage = new() { Stretch = Stretch.UniformToFill };
    private readonly TextBlock _serverName = new() { FontSize = 30, FontWeight = FontWeight.Bold, Foreground = Brushes.White };
    private readonly TextBlock _serverNote = new() { Foreground = Tokens.TextSecondary, TextWrapping = TextWrapping.Wrap, MaxWidth = 520 };
    private readonly Ellipse _statusDot = new() { Width = 9, Height = 9, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _statusText = new() { FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };
    private readonly StackPanel _news = new() { Spacing = 8 };
    private readonly Image _headerLogo = new() { Width = 30, Height = 30, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
    private bool _loading;
    private byte[]? _shownBanner;
    private byte[]? _brandingBackground;

    public LauncherWindow(LauncherModel model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _options = new OptionsView(model);
        Title = _model.Branding?.Spec.WindowTitle ?? "tPoseidon Launcher";
        Width = 960;
        Height = 600;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = Tokens.UiFont;
        FontSize = Tokens.FontSizeBody;
        Foreground = Tokens.TextPrimary;
        Background = Tokens.Surface0;
        Icon = BrandedIcon() ?? new WindowIcon(AssetLoader.Open(Asset(OperatingSystem.IsWindows() ? "tposeidon.ico" : "icon-256.png")));
        _password.PasswordChar = '*';
        _newName.MaxLength = ServerFile.MaxNameLength;
        _newAddress.MaxLength = 253;
        _account.MaxLength = 64;
        _password.MaxLength = 128;
        _settingsLayer.Transitions = new Transitions { new DoubleTransition { Property = OpacityProperty, Duration = Fade } };
        _progress.Transitions = new Transitions
        {
            new DoubleTransition { Property = Avalonia.Controls.Primitives.RangeBase.ValueProperty, Duration = Fade },
            new DoubleTransition { Property = OpacityProperty, Duration = Fade },
        };

        foreach (var choice in LauncherModel.Languages)
        {
            _language.Items.Add(new ComboBoxItem { Content = choice.Label, Tag = choice.Service });
        }

        Content = Build();
        Wire();
        Refresh();
        SettingsOpen = _model.NeedsSetup;
        Opened += async (_, _) => await RefreshChannelAsync().ConfigureAwait(true);
    }

    public bool SettingsOpen
    {
        get => _settingsLayer.IsHitTestVisible;
        set
        {
            if (value && !_settingsLayer.IsHitTestVisible)
            {
                _options.Reload();
            }

            _settingsLayer.IsHitTestVisible = value;
            _settingsLayer.Opacity = value ? 1 : 0;
        }
    }

    internal TextBox ClientFolderBox => _clientFolder;

    internal TextBlock ClientStatus => _clientStatus;

    internal ComboBox Servers => _servers;

    internal Button AddServerButton => _addServer;

    internal StackPanel AddForm => _addForm;

    internal TextBox NewName => _newName;

    internal TextBox NewAddress => _newAddress;

    internal TextBox NewWebUrl => _newWebUrl;

    internal Button SaveServerButton => _saveServer;

    internal Button RemoveServerButton => _removeServer;

    internal Button UndoButton => _undo;

    internal Button SettingsButton => _settingsButton;

    internal TextBox AccountBox => _account;

    internal TextBox PasswordBox => _password;

    internal ComboBox LanguageBox => _language;

    internal Button PlayButton => _play;

    internal OptionsView Options => _options;

    internal Button SignOutButton => _signOut;

    internal StackPanel Links => _links;

    internal Image HeaderLogo => _headerLogo;

    internal TextBlock NoteText => _serverNote;

    internal ProgressBar UpdateProgress => _progress;

    internal TextBlock ProgressText => _progressText;

    internal TextBlock UpdateSummary => _updateSummary;

    internal Banner NoticeBanner => _banner;

    internal StackPanel SignIn => _signIn;

    internal TextBlock ServerName => _serverName;

    internal TextBlock StatusText => _statusText;

    internal Ellipse StatusDot => _statusDot;

    internal StackPanel NewsList => _news;

    internal Image BannerPicture => _bannerImage;

    internal Border BannerArt => _bannerArt;

    internal async Task PrimaryAsync()
    {
        switch (_model.Session)
        {
            case SessionState.SignedOut:
                _model.SetClientFolder(_clientFolder.Text ?? "");
                await _model.SignInAsync(_account.Text ?? "", _password.Text ?? "", CancellationToken.None).ConfigureAwait(true);
                if (_model.Session is SessionState.Ready or SessionState.UpdateFailed)
                {
                    _password.Text = "";
                }

                break;
            case SessionState.UpdateFailed:
                await _model.RetryUpdateAsync(CancellationToken.None).ConfigureAwait(true);
                break;
            case SessionState.Ready:
                await _model.PlayAsync(CancellationToken.None).ConfigureAwait(true);
                break;
            default:
                break;
        }
    }

    private WindowIcon? BrandedIcon()
    {
        if (_model.Branding is not { } branding)
        {
            return null;
        }

        using var stream = File.OpenRead(branding.IconPath);
        return new WindowIcon(stream);
    }

    internal Task RefreshChannelAsync() => _model.RefreshChannelAsync(CancellationToken.None);

    private static Uri Asset(string name) => new($"avares://tPoseidon.Launcher/Assets/{name}");

    private static TextBox Field(string watermark) =>
        new() { Watermark = watermark, Height = FieldHeight, MinHeight = 0, VerticalContentAlignment = VerticalAlignment.Center };

    private static Button Small(string text)
    {
        var button = new Button { Content = text, Height = FieldHeight, MinHeight = 0, Padding = new Thickness(10, 0), VerticalContentAlignment = VerticalAlignment.Center };
        button.Classes.Add("secondary");
        return button;
    }

    private static Grid Row(Control main, params Control[] buttons)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        Grid.SetColumn(main, 0);
        grid.Children.Add(main);
        for (var i = 0; i < buttons.Length; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            buttons[i].Margin = new Thickness(8, 0, 0, 0);
            Grid.SetColumn(buttons[i], i + 1);
            grid.Children.Add(buttons[i]);
        }

        return grid;
    }

    private static Color AccentOf(string? hex) =>
        hex is not null && Color.TryParse(hex, out var color) ? color : DefaultAccent;

    private static IBrush TextOn(Color c) =>
        (0.299 * c.R) + (0.587 * c.G) + (0.114 * c.B) > 150 ? Tokens.OnAccent : Brushes.White;

    private static string StatusLabel(ChannelStatus status) => status switch
    {
        ChannelStatus.Online => "Online",
        ChannelStatus.Maintenance => "Maintenance",
        ChannelStatus.Offline => "Offline",
        ChannelStatus.Checking => "Checking...",
        _ => "Not checked",
    };

    private static IBrush StatusBrush(ChannelStatus status) => status switch
    {
        ChannelStatus.Online => Tokens.Success,
        ChannelStatus.Maintenance => Tokens.Warning,
        ChannelStatus.Offline => Tokens.Danger,
        _ => Tokens.TextMuted,
    };

    private Grid Build()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        root.RowDefinitions.Add(new RowDefinition(GridLength.Star));

        var header = BuildHeader();
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        body.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(SideWidth)));
        var left = BuildLeft();
        var right = BuildRight();
        Grid.SetColumn(left, 0);
        Grid.SetColumn(right, 1);
        body.Children.Add(left);
        body.Children.Add(right);
        Grid.SetRow(body, 1);
        root.Children.Add(body);

        var settings = BuildSettings();
        Grid.SetRow(settings, 0);
        Grid.SetRowSpan(settings, 2);
        root.Children.Add(settings);
        return root;
    }

    private Border BuildHeader()
    {
        var brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        if (_model.Branding is { } branding)
        {
            _headerLogo.Source = new Bitmap(branding.LogoPath);
            brand.Children.Add(_headerLogo);
            brand.Children.Add(new TextBlock { Text = branding.Spec.ServerName, FontWeight = FontWeight.SemiBold, FontSize = 15, VerticalAlignment = VerticalAlignment.Center });
        }
        else
        {
            _headerLogo.Source = new Bitmap(AssetLoader.Open(Asset("emblem-48.png")));
            brand.Children.Add(_headerLogo);
            brand.Children.Add(new TextBlock { Text = "tPoseidon", FontWeight = FontWeight.SemiBold, FontSize = 15, VerticalAlignment = VerticalAlignment.Center });
        }

        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        if (!_model.Locked)
        {
            right.Children.Add(_servers);
        }

        right.Children.Add(_settingsButton);
        DockPanel.SetDock(right, Dock.Right);

        var dock = new DockPanel { Margin = new Thickness(16, 0), Height = 52 };
        dock.Children.Add(right);
        dock.Children.Add(brand);
        return new Border { BorderBrush = Tokens.BorderSubtle, BorderThickness = new Thickness(0, 0, 0, 1), Child = dock };
    }

    private Grid BuildLeft()
    {
        var overlay = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(24, 0, 24, 18), Spacing = 6 };
        overlay.Children.Add(_serverName);
        var status = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        status.Children.Add(_statusDot);
        status.Children.Add(_statusText);
        overlay.Children.Add(status);
        overlay.Children.Add(_serverNote);

        var shade = new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse("#00000000"), 0.2), new GradientStop(Color.Parse("#E614171C"), 1) },
            },
        };
        var art = new Grid();
        art.Children.Add(_bannerImage);
        art.Children.Add(shade);
        art.Children.Add(overlay);
        _bannerArt.Child = art;
        _bannerArt.ClipToBounds = true;

        var newsTitle = new TextBlock { Text = "News", FontWeight = FontWeight.SemiBold, FontSize = 14, Margin = new Thickness(24, 16, 24, 8) };
        var scroll = new ScrollViewer { Content = new Border { Padding = new Thickness(24, 0, 24, 16), Child = _news }, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };

        var left = new Grid();
        left.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        left.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        left.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        Grid.SetRow(_bannerArt, 0);
        Grid.SetRow(newsTitle, 1);
        Grid.SetRow(scroll, 2);
        left.Children.Add(_bannerArt);
        left.Children.Add(newsTitle);
        left.Children.Add(scroll);
        return left;
    }

    private Border BuildRight()
    {
        _signIn.Children.Add(new TextBlock { Text = "Sign in", FontWeight = FontWeight.SemiBold, FontSize = 14 });
        _signIn.Children.Add(_account);
        _signIn.Children.Add(_password);

        _progressText.TextWrapping = TextWrapping.Wrap;
        _updateSummary.TextWrapping = TextWrapping.Wrap;
        var stack = new StackPanel { Margin = new Thickness(22, 20), Spacing = 14 };
        stack.Children.Add(_signIn);
        stack.Children.Add(_updateSummary);
        stack.Children.Add(_play);
        stack.Children.Add(_signOut);
        stack.Children.Add(_progress);
        stack.Children.Add(_progressText);
        stack.Children.Add(_banner);
        stack.Children.Add(_links);
        return new Border
        {
            Background = Tokens.Surface1,
            BorderBrush = Tokens.BorderSubtle,
            BorderThickness = new Thickness(1, 0, 0, 0),
            Child = stack,
        };
    }

    private Border BuildSettings()
    {
        var browse = Small("Browse...");
        browse.Click += async (_, _) => await BrowseClientAsync().ConfigureAwait(true);
        _clientStatus.TextWrapping = TextWrapping.Wrap;
        var cancelAdd = Small("Cancel");
        cancelAdd.Click += (_, _) => _addForm.IsVisible = false;
        _addForm.Children.Add(_newName);
        _addForm.Children.Add(_newAddress);
        _addForm.Children.Add(_newWebUrl);
        _addForm.Children.Add(Row(new Border(), cancelAdd, _saveServer));

        var close = new Button { Content = "Done", Height = 34, MinHeight = 0, Padding = new Thickness(22, 0), VerticalContentAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, FontWeight = FontWeight.SemiBold };
        close.Classes.Add("primary");
        close.Click += (_, _) => SettingsOpen = false;

        var card = new StackPanel { Spacing = 10 };
        card.Children.Add(new TextBlock { Text = _model.Locked ? "Options" : "Settings", FontSize = 18, FontWeight = FontWeight.SemiBold });
        card.Children.Add(Section("Game client"));
        card.Children.Add(Row(_clientFolder, browse));
        card.Children.Add(_clientStatus);
        if (!_model.Locked)
        {
            card.Children.Add(Section("Client language"));
            card.Children.Add(_language);
            card.Children.Add(Section("Servers"));
            card.Children.Add(Row(Tokens.Caption("Add a server by address or file."), _addServer, _importServer, _removeServer));
            card.Children.Add(_addForm);
            card.Children.Add(Section("Updates"));
            card.Children.Add(Row(Tokens.Caption("Put back the files the last update replaced."), _undo));
        }

        card.Children.Add(_options);
        card.Children.Add(close);
        var scroll = new ScrollViewer { Content = card, MaxHeight = 540, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };

        _settingsLayer.Background = Brush.Parse("#F214171C");
        _settingsLayer.Child = new Border
        {
            Width = 560,
            Background = Tokens.Surface1,
            BorderBrush = Tokens.BorderSubtle,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(24, 20),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = scroll,
        };
        return _settingsLayer;
    }

    private static TextBlock Section(string text) =>
        new() { Text = text, FontWeight = FontWeight.SemiBold, Foreground = Tokens.TextSecondary, Margin = new Thickness(0, 6, 0, 0) };

    private void Wire()
    {
        _model.Changed += Refresh;
        _settingsButton.Click += (_, _) => SettingsOpen = !SettingsOpen;
        _clientFolder.LostFocus += async (_, _) => await CommitClientFolderAsync().ConfigureAwait(true);
        _clientFolder.KeyDown += async (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                await CommitClientFolderAsync().ConfigureAwait(true);
            }
        };
        _servers.SelectionChanged += async (_, _) =>
        {
            if (!_loading && _servers.SelectedItem is ComboBoxItem { Tag: string address })
            {
                _model.SelectServer(address);
                await RefreshChannelAsync().ConfigureAwait(true);
            }
        };
        _language.SelectionChanged += (_, _) =>
        {
            if (!_loading && _language.SelectedItem is ComboBoxItem { Tag: string service })
            {
                _model.SetLanguage(service);
            }
        };
        _addServer.Click += (_, _) => _addForm.IsVisible = !_addForm.IsVisible;
        _saveServer.Click += async (_, _) =>
        {
            if (_model.AddServer(_newName.Text ?? "", _newAddress.Text ?? "", _newWebUrl.Text, null))
            {
                _newName.Text = "";
                _newAddress.Text = "";
                _newWebUrl.Text = "";
                _addForm.IsVisible = false;
                await RefreshChannelAsync().ConfigureAwait(true);
            }
        };
        _importServer.Click += async (_, _) => await ImportAsync().ConfigureAwait(true);
        _removeServer.Click += async (_, _) =>
        {
            _model.RemoveSelectedServer();
            await RefreshChannelAsync().ConfigureAwait(true);
        };
        _undo.Click += async (_, _) =>
        {
            if (_model.UndoLastUpdate())
            {
                await RefreshChannelAsync().ConfigureAwait(true);
            }
        };
        _play.Click += async (_, _) => await PrimaryAsync().ConfigureAwait(true);
        _signOut.Click += (_, _) => _model.SignOut();
        _password.KeyDown += async (_, e) =>
        {
            if (e.Key == Key.Enter && _play.IsEnabled)
            {
                await PrimaryAsync().ConfigureAwait(true);
            }
        };
    }

    private async Task CommitClientFolderAsync()
    {
        if ((_clientFolder.Text ?? "") == _model.ClientFolderPath)
        {
            return;
        }

        _model.SetClientFolder(_clientFolder.Text ?? "");
        _options.Reload();
        await RefreshChannelAsync().ConfigureAwait(true);
    }

    private void Refresh()
    {
        _loading = true;
        RefreshClient();
        RefreshServers();
        RefreshBranding();
        RefreshNews();
        RefreshPlay();
        _loading = false;
    }

    private void RefreshClient()
    {
        if ((_clientFolder.Text ?? "") != _model.ClientFolderPath && !_clientFolder.IsFocused)
        {
            _clientFolder.Text = _model.ClientFolderPath;
        }

        if (_model.ClientFolderPath.Length == 0)
        {
            _clientStatus.Text = "Choose the folder with your KR 2014 client.";
            _clientStatus.Foreground = Tokens.TextSecondary;
        }
        else if (_model.ClientCheck.Ok)
        {
            _clientStatus.Text = "The client folder looks right.";
            _clientStatus.Foreground = Tokens.Success;
        }
        else
        {
            _clientStatus.Text = "Not usable: " + string.Join("; ", _model.ClientCheck.Problems) + ".";
            _clientStatus.Foreground = Tokens.Danger;
        }

        _clientFolder.Classes.Set("bad", _model.ClientFolderPath.Length > 0 && !_model.ClientCheck.Ok);
        _language.SelectedItem = _language.Items.OfType<ComboBoxItem>().First(item => (string?)item.Tag == _model.Language);
    }

    private void RefreshServers()
    {
        _servers.Items.Clear();
        foreach (var server in _model.Servers)
        {
            _servers.Items.Add(new ComboBoxItem { Content = server.Name, Tag = server.Address });
        }

        var selected = _model.SelectedServer;
        _servers.SelectedItem = _servers.Items.OfType<ComboBoxItem>().FirstOrDefault(item => selected is not null && (string?)item.Tag == selected.Address);
        _removeServer.IsEnabled = selected is not null;
        if (_model.Servers.Count == 0)
        {
            _addForm.IsVisible = true;
        }
    }

    private void RefreshBranding()
    {
        var color = AccentOf(_model.AccentColor);
        var accent = new SolidColorBrush(color);
        _bannerArt.Background = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Blend(color, Ground, 0.45), 0), new GradientStop(Ground, 1) },
        };
        ShowBannerImage(_model.BannerImage ?? BrandingBackground());
        _serverName.Text = _model.DisplayName;
        _serverNote.Text = _model.Note ?? "";
        _serverNote.IsVisible = !string.IsNullOrEmpty(_model.Note);
        _statusDot.Fill = StatusBrush(_model.Status);
        _statusText.Text = StatusLabel(_model.Status);
        _statusText.Foreground = StatusBrush(_model.Status);
        var onAccent = TextOn(color);
        foreach (var (key, brush) in new (string, IBrush)[] { ("ButtonBackground", accent), ("ButtonBackgroundPointerOver", new SolidColorBrush(Blend(color, Colors.White, 0.18))), ("ButtonBackgroundPressed", accent), ("ButtonBackgroundDisabled", Tokens.Surface3) })
        {
            _play.Resources[key] = brush;
        }

        foreach (var key in new[] { "ButtonForeground", "ButtonForegroundPointerOver", "ButtonForegroundPressed" })
        {
            _play.Resources[key] = onAccent;
        }

        _play.Resources["ButtonForegroundDisabled"] = Tokens.TextMuted;
        _progress.Foreground = accent;
        _progress.Background = Tokens.Surface3;
    }

    private void ShowBannerImage(byte[]? image)
    {
        if (ReferenceEquals(image, _shownBanner))
        {
            return;
        }

        _shownBanner = image;
        if (image is null)
        {
            _bannerImage.Source = null;
            return;
        }

        try
        {
            using var stream = new MemoryStream(image);
            _bannerImage.Source = new Bitmap(stream);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            _bannerImage.Source = null;
            _model.ReportProblem("The server's banner picture could not be shown: " + ex.Message);
        }
    }

    private void RefreshNews()
    {
        var items = _model.Manifest?.News ?? [];
        var pictures = _model.NewsPictures;
        var key = string.Join('\n', items.Select(item => item.Date + item.Title + item.Pinned + item.Picture + (item.Picture is not null && pictures.TryGetValue(item.Picture, out var bytes) ? bytes.Length : -1)));
        if ((string?)_news.Tag == key)
        {
            return;
        }

        _news.Tag = key;
        _news.Children.Clear();
        if (items.Count == 0)
        {
            _news.Children.Add(new TextBlock { Text = "No news from this server yet.", Foreground = Tokens.TextMuted });
            return;
        }

        foreach (var item in items)
        {
            var card = NewsCard.Create(item.Title, item.Date, item.Text, item.Pinned, item.Picture is not null && pictures.TryGetValue(item.Picture, out var image) ? NewsPicture(image) : null);
            card.Opacity = 0;
            card.Transitions = new Transitions { new DoubleTransition { Property = OpacityProperty, Duration = Fade } };
            _news.Children.Add(card);
            Dispatcher.UIThread.Post(() => card.Opacity = 1);
        }
    }

    private Bitmap? NewsPicture(byte[] image)
    {
        try
        {
            using var stream = new MemoryStream(image);
            return new Bitmap(stream);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            _model.ReportProblem("A news picture could not be shown: " + ex.Message);
            return null;
        }
    }

    private byte[]? BrandingBackground()
    {
        if (_model.Branding?.BackgroundPath is not { } path)
        {
            return null;
        }

        return _brandingBackground ??= File.ReadAllBytes(path);
    }

    private string UpdateSummaryText()
    {
        if (_model.Session == SessionState.Ready)
        {
            return "You are logged in and your game is up to date.";
        }

        return _model.Plan.IsEmpty ? "" : $"An update is ready: {_model.Plan.Changes.Count} files, {(_model.Plan.Bytes / 1048576.0):0.#} MB. It downloads after you log in.";
    }

    private void RefreshLinks()
    {
        if (_model.Branding is not { } branding)
        {
            return;
        }

        var key = branding.Spec.Website + "|" + branding.Spec.Discord;
        if ((string?)_links.Tag == key)
        {
            return;
        }

        _links.Tag = key;
        _links.Children.Clear();
        foreach (var (label, address) in new[] { ("Website", branding.Spec.Website), ("Discord", branding.Spec.Discord) })
        {
            if (address is null)
            {
                continue;
            }

            var button = Small(label);
            button.Click += async (_, _) => await Launcher.LaunchUriAsync(new Uri(address)).ConfigureAwait(true);
            _links.Children.Add(button);
        }
    }

    private void RefreshPlay()
    {
        RefreshLinks();
        if (!_account.IsFocused && string.IsNullOrEmpty(_account.Text))
        {
            _account.Text = _model.Account;
        }

        _signIn.IsEnabled = !_model.NeedsSetup && _model.Session == SessionState.SignedOut;
        _play.IsEnabled = !_model.Busy && !_model.NeedsSetup && _model.Status != ChannelStatus.Maintenance;
        _play.Content = _model.Session switch
        {
            SessionState.Working => "Please wait...",
            SessionState.UpdateFailed => "Retry",
            SessionState.Ready => "Play",
            _ => "Log in",
        };
        _signOut.IsVisible = _model.Session is SessionState.Ready or SessionState.UpdateFailed;
        _progress.Value = _model.ProgressFraction;
        _progress.Opacity = _model.Busy && _model.ProgressFraction > 0 ? 1 : 0;
        _progressText.Text = _model.ProgressText;
        _updateSummary.Text = UpdateSummaryText();
        _updateSummary.IsVisible = _updateSummary.Text.Length > 0;
        if (_model.Notice is { } notice)
        {
            _banner.Show(notice.Kind, notice.Title, notice.Detail);
        }
        else if (_model.NeedsSetup)
        {
            _banner.Show(BannerKind.Info, _model.Locked ? "First, choose your game client" : "First, set up the client and a server", _model.Locked ? "Open Options (the gear) and pick the folder with your game client." : "Open the settings (the gear), pick your client folder, then add the server's address or import the server file you were given.");
        }
        else if (_model.Status == ChannelStatus.Maintenance)
        {
            _banner.Show(BannerKind.Warning, "The server is in maintenance", _model.StatusDetail);
        }
        else
        {
            _banner.Hide();
        }
    }

    private static Color Blend(Color from, Color to, double amount) => Color.FromRgb(
        (byte)(from.R + ((to.R - from.R) * amount)),
        (byte)(from.G + ((to.G - from.G) * amount)),
        (byte)(from.B + ((to.B - from.B) * amount)));

    private async Task BrowseClientAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Choose your KR 2014 client folder", AllowMultiple = false }).ConfigureAwait(true);
        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
        {
            _model.SetClientFolder(path);
            _options.Reload();
            await RefreshChannelAsync().ConfigureAwait(true);
        }
    }

    private async Task ImportAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open a server file",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("tPoseidon server file") { Patterns = ["*" + ServerFile.Extension] }, FilePickerFileTypes.All],
        }).ConfigureAwait(true);
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path && _model.ImportServerFile(path))
        {
            await RefreshChannelAsync().ConfigureAwait(true);
        }
    }
}
