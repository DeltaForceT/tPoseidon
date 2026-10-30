using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using tPoseidon.Hosting;
using tPoseidon.LauncherKit;

namespace tPoseidon.Panel;

public sealed record LauncherWizardServer(InstallPaths Paths, Func<bool> AnyRunning, Func<Task> RestartAsync);

public sealed class LauncherWizardWindow : Window
{
    private const double FieldHeight = 30;

    private static readonly (WizardStep Step, string Title)[] Steps =
    [
        (WizardStep.Identity, "1  Identity"),
        (WizardStep.Look, "2  Look"),
        (WizardStep.Connection, "3  Connection"),
        (WizardStep.Links, "4  Links and text"),
        (WizardStep.Build, "5  Build"),
    ];

    private const double AudienceEditorMaxHeight = 330;
    private const double ProgressMaximum = 100;
    private const double ProgressBarHeight = 8;
    private const double FinishedGlyphSize = 40;
    private const double FinishedTitleSize = 18;

    private readonly HostSettings _settings;
    private readonly LauncherWizardServer? _server;
    private readonly LauncherDesign _design;
    private readonly string _kitFolder;
    private readonly IConnectionCheck _check;
    private readonly string _version;
    private readonly Func<byte[], byte[]> _iconFromLogo;
    private readonly Dictionary<WizardStep, StackPanel> _pages = [];
    private readonly Dictionary<WizardStep, Button> _stepButtons = [];
    private readonly CheckBox _preset = new() { Content = "Use the tPoseidon look (test)" };
    private readonly TextBox _name = Field("Name players see");
    private readonly TextBox _title = Field("Text in the launcher's title bar");
    private readonly TextBlock _logoName = Tokens.Caption("No logo chosen");
    private readonly Button _chooseLogo = Small("Choose PNG...");
    private readonly TextBlock _backgroundName = Tokens.Caption("No background: the accent colour is used");
    private readonly Button _chooseBackground = Small("Choose picture...");
    private readonly Button _removeBackground = Small("Remove");
    private readonly TextBox _accent = Field("#3B82F6");
    private readonly TextBox _address = Field("Public address players connect to");
    private readonly TextBox _port = Field("Web port");
    private readonly Button _checkConnection = Small("Check connection");
    private readonly TextBox _website = Field("https://example.org (optional)");
    private readonly TextBox _discord = Field("https://discord.gg/... (optional)");
    private readonly TextBox _welcome = Field("A short welcome note (optional)");
    private readonly ComboBox _language = new() { Height = FieldHeight, MinHeight = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBlock _summary = new() { TextWrapping = TextWrapping.Wrap, Foreground = Tokens.TextSecondary };
    private readonly Button _build = new() { Content = "Build launcher...", Height = 34, MinHeight = 0, Padding = new Thickness(20, 0), FontWeight = FontWeight.SemiBold, VerticalContentAlignment = VerticalAlignment.Center };
    private readonly Button _back = Small("Back");
    private readonly Button _next = Small("Next");
    private readonly Banner _banner = new();
    private readonly StackPanel _localNotice = new() { Spacing = 8 };
    private readonly TextBlock _localNoticeTitle = new() { FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _localNoticeBody = new() { Foreground = Tokens.TextSecondary, TextWrapping = TextWrapping.Wrap };
    private readonly Button _letJoin = Small("Let other players join...");
    private readonly Button _testBuild = Small("Build a test launcher for this computer");
    private readonly StackPanel _noticeActions = new() { Spacing = 8 };
    private readonly Button _restartNow = Small("Restart the servers now");
    private readonly StackPanel _joinPanel = new() { Spacing = 8, IsVisible = false };
    private readonly ScrollViewer _joinScroll = new() { MaxHeight = AudienceEditorMaxHeight, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
    private readonly ProgressBar _progressBar = new() { Minimum = 0, Maximum = ProgressMaximum, Height = ProgressBarHeight, MinHeight = 0, Foreground = Tokens.Accent, Background = Tokens.Surface2, CornerRadius = new CornerRadius(ProgressBarHeight / 2) };
    private readonly TextBlock _progressStep = Tokens.Caption();
    private readonly TextBlock _progressPercent = Tokens.Caption();
    private readonly StackPanel _progressPanel = new() { Spacing = 6, IsVisible = false };
    private readonly StackPanel _finished = new() { Spacing = 12, IsVisible = false };
    private readonly TextBlock _finishedTitle = new() { FontSize = FinishedTitleSize, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _finishedBody = new() { Foreground = Tokens.TextSecondary, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _finishedFiles = new() { Foreground = Tokens.TextSecondary, TextWrapping = TextWrapping.Wrap };
    private readonly Button _openFolder = new() { Content = "Open the folder", Height = 34, MinHeight = 0, Padding = new Thickness(20, 0), FontWeight = FontWeight.SemiBold, VerticalContentAlignment = VerticalAlignment.Center };
    private readonly Button _buildAgain = Small("Build again");
    private readonly Button _useForUpdates = Small("Hand it out as an update");
    private readonly Button _closeFinished = Small("Close");
    private readonly StackPanel _nav = new() { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
    private string _finishedFolder = "";
    private readonly Button _saveJoin = Small("Save");
    private readonly Button _cancelJoin = Small("Cancel");
    private readonly Border _noticeCard = new() { Background = Tokens.Surface1, BorderBrush = Tokens.BorderSubtle, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(14, 12), IsVisible = false };
    private StackPanel _connectionFields = new();
    private PlayerAudienceEditor? _audienceEditor;
    private readonly Border _previewArt = new() { Height = 150, CornerRadius = new CornerRadius(8), ClipToBounds = true };
    private readonly Image _previewBackground = new() { Stretch = Stretch.UniformToFill };
    private readonly Image _previewLogo = new() { Width = 32, Height = 32, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _previewName = new() { FontSize = 18, FontWeight = FontWeight.Bold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _previewTitle = new() { FontSize = 11, Foreground = Tokens.TextSecondary };
    private readonly TextBlock _previewWelcome = new() { Foreground = Tokens.TextSecondary, TextWrapping = TextWrapping.Wrap, FontSize = 11 };
    private readonly Border _previewButton = new() { Height = 30, CornerRadius = new CornerRadius(6), Child = new TextBlock { Text = "Log in", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeight.SemiBold } };
    private bool _loading;
    private LauncherBuildResult? _lastBuild;

    public LauncherWizardWindow(HostSettings settings, string kitFolder, IConnectionCheck check, string version, Func<byte[], byte[]>? iconFromLogo = null, LauncherWizardServer? server = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _server = server;
        _iconFromLogo = iconFromLogo ?? LauncherIcons.FromLogo;
        _design = LauncherDesign.From(settings);
        _kitFolder = kitFolder ?? throw new ArgumentNullException(nameof(kitFolder));
        _check = check ?? throw new ArgumentNullException(nameof(check));
        _version = version ?? throw new ArgumentNullException(nameof(version));
        Title = "Make a launcher for your players";
        Width = 920;
        Height = 640;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = Tokens.UiFont;
        FontSize = Tokens.FontSizeBody;
        Foreground = Tokens.TextPrimary;
        Background = Tokens.Surface0;
        Icon = PanelAssets.WindowIcon();
        _name.MaxLength = ServerFile.MaxNameLength;
        _title.MaxLength = BrandingSpec.MaxTitleLength;
        _welcome.MaxLength = BrandingSpec.MaxWelcomeLength;
        _port.MaxLength = 5;
        _build.Classes.Add("primary");
        _openFolder.Classes.Add("primary");
        foreach (var action in new[] { _letJoin, _testBuild })
        {
            action.HorizontalAlignment = HorizontalAlignment.Stretch;
            action.HorizontalContentAlignment = HorizontalAlignment.Center;
        }
        _next.Classes.Add("primary");
        foreach (var choice in new[] { (ClientFolder.AutomaticService, "Automatic (from the game data)"), (ClientFolder.KoreanService, "Korean"), (ClientFolder.EnglishService, "English") })
        {
            _language.Items.Add(new ComboBoxItem { Content = choice.Item2, Tag = choice.Item1 });
        }

        Content = BuildLayout();
        Wire();
        Load();
        Show(WizardStep.Identity);
    }

    public LauncherDesign Design => _design;

    public WizardStep Current { get; private set; }

    public LauncherBuildResult? LastBuild => _lastBuild;

    public CheckBox PresetBox => _preset;

    public TextBox NameBox => _name;

    public TextBox TitleBox => _title;

    public TextBox AccentBox => _accent;

    public TextBox AddressBox => _address;

    public TextBox PortBox => _port;

    public TextBox WebsiteBox => _website;

    public TextBox DiscordBox => _discord;

    public TextBox WelcomeBox => _welcome;

    public Button ChooseLogoButton => _chooseLogo;

    public Button ChooseBackgroundButton => _chooseBackground;

    public Button CheckConnectionButton => _checkConnection;

    public Border LocalNotice => _noticeCard;

    public TextBlock LocalNoticeTitle => _localNoticeTitle;

    public Button LetJoinButton => _letJoin;

    public Button TestBuildButton => _testBuild;

    public Button SaveJoinButton => _saveJoin;

    public Button RestartNowButton => _restartNow;

    public PlayerAudienceEditor? AudienceEditor => _audienceEditor;

    public bool ConnectionFieldsVisible => _connectionFields.IsVisible;

    public Button NextButton => _next;

    public Button BackButton => _back;

    public Button BuildButton => _build;

    public Banner Notice => _banner;

    public ProgressBar BuildProgress => _progressBar;

    public StackPanel FinishedView => _finished;

    public Button BuildAgainButton => _buildAgain;

    public Button UseForUpdatesButton => _useForUpdates;

    public Border PreviewButton => _previewButton;

    public TextBlock PreviewName => _previewName;

    public TextBlock PreviewTitle => _previewTitle;

    public Image PreviewLogo => _previewLogo;

    public Image PreviewBackground => _previewBackground;

    public bool PageVisible(WizardStep step) => _pages[step].IsVisible;

    public void SetLogo(byte[] png)
    {
        ArgumentNullException.ThrowIfNull(png);
        try
        {
            var (width, height) = IconMaker.Dimensions(png);
            _design.Icon = _iconFromLogo(png);
            _design.Logo = png;
            _logoName.Text = $"{width} x {height} PNG";
        }
        catch (HostingException ex)
        {
            _design.Logo = null;
            _design.Icon = null;
            _logoName.Text = "No logo chosen";
            _banner.Show(BannerKind.Error, ex.Summary, ex.Hint);
        }

        RefreshPreview();
    }

    public void SetBackground(byte[]? picture)
    {
        _design.Background = picture;
        _backgroundName.Text = picture is null ? "No background: the accent colour is used" : $"{picture.Length / 1024} KB picture";
        _removeBackground.IsEnabled = picture is not null;
        RefreshPreview();
    }

    public bool GoNext()
    {
        if (Current == WizardStep.Connection && _design.NeedsReachableAddress)
        {
            ShowLocalNotice();
            return false;
        }

        var problems = _design.Problems(Current);
        if (problems.Count > 0)
        {
            _banner.Show(BannerKind.Error, problems[0], problems.Count > 1 ? string.Join(" ", problems.Skip(1)) : null);
            return false;
        }

        _banner.Hide();
        Show(Steps[Math.Min(Steps.Length - 1, Array.FindIndex(Steps, entry => entry.Step == Current) + 1)].Step);
        return true;
    }

    public void GoBack()
    {
        _banner.Hide();
        Show(Steps[Math.Max(0, Array.FindIndex(Steps, entry => entry.Step == Current) - 1)].Step);
    }

    public void OpenAudienceEditor()
    {
        _audienceEditor = new PlayerAudienceEditor(_settings);
        _joinScroll.Content = _audienceEditor;
        _joinPanel.IsVisible = true;
        _noticeActions.IsVisible = false;
        _connectionFields.IsVisible = false;
        _banner.Hide();
    }

    public void CloseAudienceEditor()
    {
        _joinPanel.IsVisible = false;
        _joinScroll.Content = null;
        _audienceEditor = null;
        _connectionFields.IsVisible = true;
        RefreshLocalNotice();
    }

    public bool SaveWhoCanJoin()
    {
        if (_audienceEditor is null || _server is null)
        {
            return false;
        }

        var previousAudience = _settings.Network.Audience;
        var previousAddress = _settings.Network.PublicAddress;
        if (!_audienceEditor.TryApply())
        {
            return false;
        }

        if (_settings.Network.Audience == PlayerAudience.Local)
        {
            _banner.Show(BannerKind.Info, "Players still cannot join from other computers.", "Choose Friends on my network or Anyone on the internet.");
            return false;
        }

        try
        {
            _settings.Save(_server.Paths);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _settings.Network.Audience = previousAudience;
            _settings.Network.PublicAddress = previousAddress;
            _banner.Show(BannerKind.Error, "The change could not be saved.", ex.Message);
            return false;
        }

        _design.ApplyNetwork(_settings);
        CloseAudienceEditor();
        Load();
        var running = _server.AnyRunning();
        _restartNow.IsVisible = running;
        _banner.Show(BannerKind.Success, "Saved. Other players can now join.", running
            ? "Restart the servers for the change to apply."
            : "The change applies the next time the servers start.");
        return true;
    }

    public async Task RestartServersAsync()
    {
        if (_server is null)
        {
            return;
        }

        _restartNow.IsEnabled = false;
        _banner.Show(BannerKind.Info, "Restarting the servers...", "This takes a moment.");
        try
        {
            await _server.RestartAsync().ConfigureAwait(true);
            _restartNow.IsVisible = false;
            _banner.Show(BannerKind.Success, "The servers restarted.", "The new address is in use.");
        }
        finally
        {
            _restartNow.IsEnabled = true;
        }
    }

    public void UseTestLauncher()
    {
        _design.BuildTestForThisComputer();
        Load();
        _banner.Show(BannerKind.Info, "Building a test launcher for this computer.", "It only works on this computer and cannot be given to players.");
    }

    public async Task<bool> CheckConnectionAsync()
    {
        if (_design.NeedsReachableAddress)
        {
            ShowLocalNotice();
            return false;
        }

        var problems = _design.Problems(WizardStep.Connection);
        if (problems.Count > 0)
        {
            _banner.Show(BannerKind.Error, problems[0]);
            return false;
        }

        _banner.Show(BannerKind.Info, "Checking the connection...");
        var result = await _check.CheckAsync(_design.ManifestUrl(), CancellationToken.None).ConfigureAwait(true);
        _banner.Show(result.Reachable ? BannerKind.Success : BannerKind.Error, result.Reachable ? "The server answers" : "The server did not answer", result.Message);
        return result.Reachable;
    }

    public async Task<bool> BuildAsync(string outputFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFolder);
        if (_design.NeedsReachableAddress)
        {
            ShowLocalNotice();
            return false;
        }

        var problems = _design.Problems(WizardStep.Build);
        if (problems.Count > 0)
        {
            _banner.Show(BannerKind.Error, problems[0]);
            return false;
        }

        _build.IsEnabled = false;
        _back.IsEnabled = false;
        _banner.Hide();
        _progressBar.Value = 0;
        ShowProgress(new LauncherBuildProgress(LauncherBuildStep.Copying, 0));
        _progressPanel.IsVisible = true;
        try
        {
            _design.PublicKey = _server is null ? null : SigningKeyStore.EnsureCreated(_server.Paths).PublicKey;
            var request = _design.ToRequest(_version, DateTimeOffset.UtcNow);
            var progress = new Progress<LauncherBuildProgress>(ShowProgress);
            _lastBuild = await Task.Run(() => LauncherBuilder.Build(request, LauncherKitFiles.Locate(_kitFolder), outputFolder, progress)).ConfigureAwait(true);
            ShowProgress(new LauncherBuildProgress(LauncherBuildStep.Done, LauncherBuilder.Finished));
            ShowFinished(_lastBuild, outputFolder);
            return true;
        }
        catch (HostingException ex)
        {
            _progressPanel.IsVisible = false;
            _banner.Show(BannerKind.Error, ex.Summary, ex.Hint);
            return false;
        }
        finally
        {
            _build.IsEnabled = true;
            _back.IsEnabled = true;
        }
    }

    private static string StepText(LauncherBuildStep step) => step switch
    {
        LauncherBuildStep.Copying => "Copying the launcher files",
        LauncherBuildStep.Branding => "Adding your branding",
        LauncherBuildStep.PackingInstaller => "Packing the installer",
        LauncherBuildStep.PackingZip => "Packing the zip",
        LauncherBuildStep.WritingInstaller => "Writing the installer",
        _ => "Done",
    };

    private void ShowProgress(LauncherBuildProgress progress)
    {
        _progressBar.Value = Math.Max(_progressBar.Value, Math.Clamp(progress.Fraction, 0, 1) * ProgressMaximum);
        _progressStep.Text = StepText(progress.Step);
        _progressPercent.Text = string.Create(CultureInfo.InvariantCulture, $"{Math.Round(_progressBar.Value)} %");
    }

    private void ShowFinished(LauncherBuildResult result, string folder)
    {
        _finishedFolder = folder;
        _finishedTitle.Text = _design.TestOnThisComputer ? "Your test launcher is ready" : "Your launcher is ready";
        _finishedBody.Text = _design.TestOnThisComputer
            ? "It only works on this computer. Do not give it to players."
            : "Give the installer to your players, or add the zip to your client pack.";
        _finishedFiles.Text = $"Installer: {result.SetupPath}{Environment.NewLine}Zip: {result.ZipPath}";
        _useForUpdates.IsVisible = _server is not null;
        SetFinished(true);
    }

    private void SetFinished(bool finished)
    {
        _finished.IsVisible = finished;
        _preset.IsVisible = !finished;
        _nav.IsVisible = !finished;
        _progressPanel.IsVisible = false;
        foreach (var (key, page) in _pages)
        {
            page.IsVisible = !finished && key == Current;
        }

        foreach (var button in _stepButtons.Values)
        {
            button.IsEnabled = !finished;
        }
    }

    public async Task<bool> UseForUpdatesAsync()
    {
        if (_server is null || _lastBuild is null)
        {
            return false;
        }

        try
        {
            var import = await Task.Run(() => LauncherUpdatePackage.Import(_server.Paths, _lastBuild.ZipPath)).ConfigureAwait(true);
            _banner.Show(import.Info.TestOnThisComputer ? BannerKind.Warning : BannerKind.Success, import.Info.TestOnThisComputer ? "This test launcher was added to the updates" : "The launcher is ready to go out as an update", import.Info.TestOnThisComputer ? LauncherUpdatePackage.TestLauncherWarning : "Press Publish in the launcher updates window to send it to your players.");
            return true;
        }
        catch (HostingException ex)
        {
            _banner.Show(BannerKind.Error, ex.Summary, ex.Hint);
            return false;
        }
    }

    private async Task OpenFinishedFolderAsync()
    {
        if (!await Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(_finishedFolder)).ConfigureAwait(true))
        {
            _banner.Show(BannerKind.Warning, "The folder could not be opened.", _finishedFolder);
        }
    }

    private static TextBox Field(string watermark) =>
        new() { Watermark = watermark, Height = FieldHeight, MinHeight = 0, VerticalContentAlignment = VerticalAlignment.Center };

    private static Button Small(string text)
    {
        var button = new Button { Content = text, Height = FieldHeight, MinHeight = 0, Padding = new Thickness(14, 0), VerticalContentAlignment = VerticalAlignment.Center };
        button.Classes.Add("secondary");
        return button;
    }

    private static StackPanel Labelled(string label, params Control[] controls)
    {
        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(Tokens.Caption(label));
        foreach (var control in controls)
        {
            stack.Children.Add(control);
        }

        return stack;
    }

    private static StackPanel RowOf(params Control[] controls)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var control in controls)
        {
            row.Children.Add(control);
        }

        return row;
    }

    private static Color AccentOf(string text) => Color.TryParse(text, out var color) ? color : Color.Parse(LauncherDesign.DefaultAccent);

    private Grid BuildLayout()
    {
        var steps = new StackPanel { Spacing = 4, Margin = new Thickness(16, 20) };
        steps.Children.Add(new TextBlock { Text = "Launcher", FontSize = 16, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
        foreach (var (step, title) in Steps)
        {
            var button = new Button { Content = title, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Height = 34, MinHeight = 0 };
            button.Classes.Add("flat");
            button.Click += (_, _) => GoTo(step);
            _stepButtons[step] = button;
            steps.Children.Add(button);
        }

        var middle = new StackPanel { Spacing = 14, Margin = new Thickness(24, 20, 16, 12) };
        middle.Children.Add(_preset);
        _pages[WizardStep.Identity] = Page(
            Labelled("Server name", _name),
            Labelled("Window title", _title),
            Labelled("Logo", RowOf(_chooseLogo, _logoName)),
            Tokens.Caption("The icon for the shortcuts is made from the logo."));
        _pages[WizardStep.Look] = Page(
            Labelled("Background picture", RowOf(_chooseBackground, _removeBackground), _backgroundName),
            Labelled("Accent colour", _accent));
        _connectionFields = Page(
            Labelled("Public address", _address),
            Labelled("Web port", _port),
            RowOf(_checkConnection),
            Tokens.Caption("The launcher logs players in and downloads updates through this web address. A launcher for players needs an address they can reach."));
        _pages[WizardStep.Connection] = Page(BuildLocalNotice(), _connectionFields);
        _pages[WizardStep.Links] = Page(
            Labelled("Website", _website),
            Labelled("Discord", _discord),
            Labelled("Welcome note", _welcome),
            Labelled("Default client language", _language));
        var progressLine = new DockPanel();
        DockPanel.SetDock(_progressPercent, Dock.Right);
        progressLine.Children.Add(_progressPercent);
        progressLine.Children.Add(_progressStep);
        _progressPanel.Children.Add(progressLine);
        _progressPanel.Children.Add(_progressBar);
        _pages[WizardStep.Build] = Page(_summary, RowOf(_build), _progressPanel);
        foreach (var page in _pages.Values)
        {
            middle.Children.Add(page);
        }

        _finished.Children.Add(Tokens.Glyphs(Glyph.Check, FinishedGlyphSize, Tokens.Success));
        _finished.Children.Add(_finishedTitle);
        _finished.Children.Add(_finishedBody);
        _finished.Children.Add(_finishedFiles);
        _finished.Children.Add(RowOf(_openFolder, _useForUpdates, _buildAgain, _closeFinished));
        middle.Children.Add(_finished);
        _nav.Children.Add(_back);
        _nav.Children.Add(_next);
        middle.Children.Add(_banner);
        middle.Children.Add(_nav);

        var preview = new StackPanel { Spacing = 8, Margin = new Thickness(8, 20, 20, 20) };
        preview.Children.Add(Tokens.Caption("What your players will see"));
        preview.Children.Add(BuildPreview());

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(190)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(340)));
        Grid.SetColumn(middle, 1);
        Grid.SetColumn(preview, 2);
        var side = new Border { Background = Tokens.Surface1, BorderBrush = Tokens.BorderSubtle, BorderThickness = new Thickness(0, 0, 1, 0), Child = steps };
        grid.Children.Add(side);
        grid.Children.Add(middle);
        grid.Children.Add(preview);
        return grid;
    }

    private Border BuildLocalNotice()
    {
        _noticeActions.Children.Add(_letJoin);
        _noticeActions.Children.Add(_testBuild);
        _joinPanel.Children.Add(_joinScroll);
        _joinPanel.Children.Add(RowOf(_saveJoin, _cancelJoin));
        _localNotice.Children.Add(_localNoticeTitle);
        _localNotice.Children.Add(_localNoticeBody);
        _localNotice.Children.Add(_noticeActions);
        _localNotice.Children.Add(_restartNow);
        _localNotice.Children.Add(_joinPanel);
        _restartNow.IsVisible = false;
        _noticeCard.Child = _localNotice;
        return _noticeCard;
    }

    private void RefreshLocalNotice()
    {
        var local = _design.IsLocalOnly;
        _noticeCard.IsVisible = local;
        _localNoticeTitle.Text = _design.TestOnThisComputer
            ? "This will be a test launcher for this computer only"
            : "Your server only accepts players on this computer";
        _localNoticeBody.Text = _design.TestOnThisComputer
            ? "It uses 127.0.0.1, so it works here and nowhere else. It cannot be given to players. You can still let other players join to build a real one."
            : "A launcher for players needs an address they can reach, so it cannot be built yet. You can let other players join, or build a test launcher that only works on this computer and try the whole flow here.";
        _testBuild.IsVisible = !_design.TestOnThisComputer;
        _letJoin.IsVisible = _server is not null;
    }

    private void ShowLocalNotice()
    {
        Show(WizardStep.Connection);
        _banner.Hide();
        RefreshLocalNotice();
    }

    private static StackPanel Page(params Control[] controls)
    {
        var page = new StackPanel { Spacing = 12 };
        foreach (var control in controls)
        {
            page.Children.Add(control);
        }

        return page;
    }

    private Border BuildPreview()
    {
        var shade = new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse("#00000000"), 0.2), new GradientStop(Color.Parse("#E614171C"), 1) },
            },
        };
        var identity = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(14) };
        identity.Children.Add(_previewLogo);
        identity.Children.Add(_previewName);
        var art = new Grid();
        art.Children.Add(_previewBackground);
        art.Children.Add(shade);
        art.Children.Add(identity);
        _previewArt.Child = art;

        var bar = new Border { Background = Tokens.Surface2, Padding = new Thickness(10, 5), Child = _previewTitle };
        var body = new StackPanel { Spacing = 8, Margin = new Thickness(12) };
        body.Children.Add(_previewArt);
        body.Children.Add(_previewWelcome);
        body.Children.Add(new Border { Height = 28, CornerRadius = new CornerRadius(4), Background = Tokens.Surface2, Child = new TextBlock { Text = "Account name", Foreground = Tokens.TextMuted, Margin = new Thickness(8, 0), VerticalAlignment = VerticalAlignment.Center } });
        body.Children.Add(new Border { Height = 28, CornerRadius = new CornerRadius(4), Background = Tokens.Surface2, Child = new TextBlock { Text = "Password", Foreground = Tokens.TextMuted, Margin = new Thickness(8, 0), VerticalAlignment = VerticalAlignment.Center } });
        body.Children.Add(_previewButton);
        var window = new StackPanel();
        window.Children.Add(bar);
        window.Children.Add(body);
        return new Border { Background = Tokens.Surface0, BorderBrush = Tokens.BorderSubtle, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), ClipToBounds = true, Child = window };
    }

    private void Wire()
    {
        _preset.IsCheckedChanged += (_, _) =>
        {
            if (_loading)
            {
                return;
            }

            if (_preset.IsChecked == true)
            {
                _design.UseTPoseidonLook(LauncherIcons.Preset());
                _logoName.Text = "The tPoseidon logo";
                _backgroundName.Text = "The tPoseidon background";
                _removeBackground.IsEnabled = true;
            }
            else
            {
                _design.ChangeLook();
            }

            Load();
        };
        OnText(_name, () => _design.ServerName = _name.Text ?? "");
        OnText(_title, () => _design.WindowTitle = _title.Text ?? "");
        OnText(_accent, () => _design.AccentColor = (_accent.Text ?? "").Trim());
        OnText(_address, () => _design.Address = (_address.Text ?? "").Trim());
        OnText(_port, () => _design.WebPort = int.TryParse(_port.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var port) ? port : 0);
        OnText(_website, () => _design.Website = _website.Text);
        OnText(_discord, () => _design.Discord = _discord.Text);
        OnText(_welcome, () => _design.Welcome = _welcome.Text);
        _language.SelectionChanged += (_, _) => Edit(() =>
        {
            if (_language.SelectedItem is ComboBoxItem { Tag: string service })
            {
                _design.Language = service;
            }
        });
        _chooseLogo.Click += async (_, _) => await ChoosePictureAsync("Choose the logo", ["*.png"], SetLogo).ConfigureAwait(true);
        _chooseBackground.Click += async (_, _) => await ChoosePictureAsync("Choose the background", ["*.png", "*.jpg", "*.jpeg"], SetBackground).ConfigureAwait(true);
        _removeBackground.Click += (_, _) => SetBackground(null);
        _letJoin.Click += (_, _) => OpenAudienceEditor();
        _testBuild.Click += (_, _) => UseTestLauncher();
        _saveJoin.Click += (_, _) => SaveWhoCanJoin();
        _cancelJoin.Click += (_, _) => CloseAudienceEditor();
        _restartNow.Click += async (_, _) => await RestartServersAsync().ConfigureAwait(true);
        _checkConnection.Click += async (_, _) => await CheckConnectionAsync().ConfigureAwait(true);
        _next.Click += (_, _) => GoNext();
        _back.Click += (_, _) => GoBack();
        _build.Click += async (_, _) => await ChooseFolderAndBuildAsync().ConfigureAwait(true);
        _openFolder.Click += async (_, _) => await OpenFinishedFolderAsync().ConfigureAwait(true);
        _useForUpdates.Click += async (_, _) => await UseForUpdatesAsync().ConfigureAwait(true);
        _buildAgain.Click += (_, _) =>
        {
            SetFinished(false);
            Show(WizardStep.Build);
        };
        _closeFinished.Click += (_, _) => Close();
    }

    private void OnText(TextBox box, Action change) =>
        box.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                Edit(change);
            }
        };

    private void Edit(Action change)
    {
        if (_loading)
        {
            return;
        }

        change();
        RefreshPreview();
    }

    private void Load()
    {
        _loading = true;
        _preset.IsChecked = _design.UsesTPoseidonLook;
        _name.Text = _design.ServerName;
        _title.Text = _design.WindowTitle;
        _accent.Text = _design.AccentColor;
        _address.Text = _design.Address;
        _port.Text = _design.WebPort.ToString(CultureInfo.InvariantCulture);
        _website.Text = _design.Website;
        _discord.Text = _design.Discord;
        _welcome.Text = _design.Welcome;
        _language.SelectedItem = _language.Items.OfType<ComboBoxItem>().First(item => (string?)item.Tag == _design.Language);
        var locked = _design.UsesTPoseidonLook;
        foreach (var control in new Control[] { _title, _chooseLogo, _chooseBackground, _removeBackground, _accent })
        {
            control.IsEnabled = !locked;
        }

        _removeBackground.IsEnabled = !locked && _design.Background is not null;
        _loading = false;
        RefreshLocalNotice();
        RefreshPreview();
    }

    private void RefreshPreview()
    {
        var color = AccentOf(_design.AccentColor);
        _previewArt.Background = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.FromRgb((byte)(color.R * 0.55), (byte)(color.G * 0.55), (byte)(color.B * 0.55)), 0), new GradientStop(Color.Parse("#14171C"), 1) },
        };
        _previewBackground.Source = Picture(_design.Background);
        _previewLogo.Source = Picture(_design.Logo);
        _previewName.Text = string.IsNullOrWhiteSpace(_design.ServerName) ? "Your server" : _design.ServerName;
        _previewTitle.Text = string.IsNullOrWhiteSpace(_design.WindowTitle) ? "Window title" : _design.WindowTitle;
        _previewWelcome.Text = _design.Welcome ?? "";
        _previewWelcome.IsVisible = !string.IsNullOrWhiteSpace(_design.Welcome);
        _previewButton.Background = new SolidColorBrush(color);
        ((TextBlock)_previewButton.Child!).Foreground = (0.299 * color.R) + (0.587 * color.G) + (0.114 * color.B) > 150 ? Tokens.OnAccent : Brushes.White;
        _summary.Text = _design.TestOnThisComputer
            ? $"{_design.ServerName} at {_design.Address}:{_design.WebPort}. This is a test launcher: it only works on this computer and cannot be given to players. The build makes '{LauncherBuilder.SetupFileName(_design.ServerName, true)}' and a zip of the same launcher."
            : $"{_design.ServerName} at {_design.Address}:{_design.WebPort}. The build makes '{LauncherBuilder.SetupFileName(_design.ServerName)}' for your players and a zip of the same launcher.";
    }

    private static Bitmap? Picture(byte[]? bytes)
    {
        if (bytes is null)
        {
            return null;
        }

        try
        {
            return new Bitmap(new MemoryStream(bytes));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    private void GoTo(WizardStep target)
    {
        var from = Array.FindIndex(Steps, entry => entry.Step == Current);
        var to = Array.FindIndex(Steps, entry => entry.Step == target);
        while (from < to)
        {
            if (!GoNext())
            {
                return;
            }

            from++;
        }

        if (to < from)
        {
            Show(target);
        }
    }

    private void Show(WizardStep step)
    {
        Current = step;
        foreach (var (key, page) in _pages)
        {
            page.IsVisible = key == step;
        }

        foreach (var (key, button) in _stepButtons)
        {
            button.FontWeight = key == step ? FontWeight.Bold : FontWeight.Normal;
        }

        _back.IsEnabled = step != WizardStep.Identity;
        _next.IsVisible = step != WizardStep.Build;
        RefreshPreview();
    }

    private async Task ChoosePictureAsync(string title, string[] patterns, Action<byte[]> accept)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Pictures") { Patterns = patterns }],
        }).ConfigureAwait(true);
        if (files.Count > 0)
        {
            await using var stream = await files[0].OpenReadAsync().ConfigureAwait(true);
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory).ConfigureAwait(true);
            accept(memory.ToArray());
        }
    }

    private async Task ChooseFolderAndBuildAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Where should the launcher files go?", AllowMultiple = false }).ConfigureAwait(true);
        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
        {
            await BuildAsync(path).ConfigureAwait(true);
        }
    }
}
