using System.Globalization;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Npgsql;
using tPoseidon.Core.Data.Postgres;
using tPoseidon.Hosting;
using tPoseidon.Hosting.Database;
using tPoseidon.Hosting.Servers;

namespace tPoseidon.Panel;

public sealed class SetupState
{
    public SetupState(InstallPaths paths, HostSettings settings)
    {
        Paths = paths;
        Settings = settings;
    }

    public InstallPaths Paths { get; }

    public HostSettings Settings { get; }

    public string AdminConnection { get; set; } = "";

    public string DatabaseName { get; set; } = DatabaseSettings.DefaultDatabaseName;

    public string DatabaseServer { get; set; } = "";

    public string Account { get; set; } = "admin";

    public string Password { get; set; } = "";

    public string PlayWith { get; set; } = "";
}

public static class OsHint
{
    public static string Name => OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsMacOS() ? "macOS" : "Linux";

    public static string Firewall(IReadOnlyList<int> ports) =>
        OperatingSystem.IsWindows() ? "Windows Defender Firewall > Advanced settings > Inbound Rules > New Rule > Port > TCP."
        : OperatingSystem.IsMacOS() ? "System Settings > Network > Firewall > Options: allow incoming connections for tPoseidon."
        : $"For ufw: sudo ufw allow {string.Join(',', ports)}/tcp";

    public const string InstallGuide = "If it isn't installed yet, see docs/install/postgresql.md.";
}

public sealed class ChoiceCard : Border
{
    private readonly Border _ring = new() { Width = 16, Height = 16, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(2), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 12, 0) };
    private readonly Border _dot = new() { Width = 6, Height = 6, CornerRadius = new CornerRadius(3), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

    public ChoiceCard(string title, string description)
    {
        Cursor = new Cursor(StandardCursorType.Hand);
        CornerRadius = new CornerRadius(8);
        BorderThickness = new Thickness(1);
        Padding = new Thickness(14, 10);
        _ring.Child = _dot;
        var text = new StackPanel { Spacing = 2 };
        text.Children.Add(new TextBlock { Text = title, FontWeight = FontWeight.SemiBold });
        text.Children.Add(new TextBlock { Text = description, Foreground = Tokens.TextSecondary, FontSize = Tokens.FontSizeCaption, TextWrapping = TextWrapping.Wrap });
        var dock = new DockPanel();
        DockPanel.SetDock(_ring, Dock.Left);
        dock.Children.Add(_ring);
        dock.Children.Add(text);
        Child = dock;
        PointerPressed += (_, _) => Chosen?.Invoke();
        Selected = false;
    }

    public event Action? Chosen;

    public bool Selected
    {
        set
        {
            Background = value ? Tokens.AccentSoft : Tokens.Surface1;
            BorderBrush = value ? Tokens.Accent : Tokens.BorderSubtle;
            _ring.BorderBrush = value ? Tokens.Accent : Tokens.TextMuted;
            _dot.Background = value ? Tokens.Accent : Brushes.Transparent;
        }
    }
}

public abstract class WizardPage : StackPanel
{
    protected WizardPage()
    {
        Spacing = 14;
        Margin = new Thickness(28, 4, 28, 16);
    }

    public abstract string Title { get; }

    public abstract string Subtitle { get; }

    public virtual string? StepName => null;

    public virtual string NextText => "Next";

    public virtual bool CanContinue => true;

    public virtual bool CanGoBack => true;

    public virtual bool HidesFooter => false;

    public event Action? StateChanged;

    public virtual Task<bool> LeaveAsync() => Task.FromResult(true);

    public virtual void Entered()
    {
    }

    public virtual void Left()
    {
    }

    protected void RaiseState() => StateChanged?.Invoke();

    protected static TextBox Box(string text, string? watermark = null, bool password = false, double width = double.NaN)
    {
        var box = new TextBox { Text = text, Watermark = watermark, Height = 30, MinHeight = 0, Padding = new Thickness(9, 5), VerticalContentAlignment = VerticalAlignment.Center, Width = width };
        if (password)
        {
            box.PasswordChar = '●';
        }

        return box;
    }

    protected static Control Labeled(string label, Control control, string? hint = null)
    {
        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(new TextBlock { Text = label, FontSize = Tokens.FontSizeCaption, Foreground = Tokens.TextSecondary });
        stack.Children.Add(control);
        if (hint is not null)
        {
            stack.Children.Add(new TextBlock { Text = hint, FontSize = 10.5, Foreground = Tokens.TextMuted, TextWrapping = TextWrapping.Wrap });
        }

        return stack;
    }

    protected static void Mark(TextBox box, bool bad)
    {
        if (bad)
        {
            box.Classes.Add("bad");
        }
        else
        {
            box.Classes.Remove("bad");
        }
    }

    protected static TextBlock Note(string text) =>
        new() { Text = text, Foreground = Tokens.TextSecondary, FontSize = Tokens.FontSizeCaption, TextWrapping = TextWrapping.Wrap };
}

public sealed class WelcomePage : WizardPage
{
    private readonly SetupState _state;
    private readonly Banner _result = new();

    public WelcomePage(SetupState state)
    {
        _state = state;
        Spacing = 16;
        Children.Add(new Image { Source = new Bitmap(AssetLoader.Open(PanelAssets.Uri("logo-1024.png"))), Width = 330, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 4) });
        Children.Add(new TextBlock { Text = "A few questions, then your server is ready. It takes about five minutes.", TextWrapping = TextWrapping.Wrap, FontSize = 14 });
        var needs = new StackPanel { Spacing = 8 };
        needs.Children.Add(Need("Your own game client folder", "tPoseidon reads the game data from it."));
        needs.Children.Add(Need($"PostgreSQL {DatabaseAdmin.MinimumMajorVersion} or newer", "It stores accounts and characters. The admin login is used once and never saved."));
        needs.Children.Add(Need("A name and password for your first account", "You can add a GM character afterwards."));
        Children.Add(new Border { Background = Tokens.Surface1, BorderBrush = Tokens.BorderSubtle, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(14, 12), Child = needs });
        Children.Add(_result);
    }

    public override string Title => "Welcome";

    public override string Subtitle => "Set up your own server";

    public override string NextText => "Get started";

    public override Task<bool> LeaveAsync()
    {
        if (new ServerSupervisor(_state.Paths, _state.Settings).AnyRunning())
        {
            _result.Show(BannerKind.Error, "The servers are running.", "Stop them from the panel before changing the setup.");
            return Task.FromResult(false);
        }

        _result.Hide();
        return Task.FromResult(true);
    }

    private static DockPanel Need(string title, string detail)
    {
        var dock = new DockPanel();
        var icon = Tokens.Glyphs(Glyph.Check, 13, Tokens.Success);
        icon.VerticalAlignment = VerticalAlignment.Top;
        icon.Margin = new Thickness(0, 2, 10, 0);
        DockPanel.SetDock(icon, Dock.Left);
        dock.Children.Add(icon);
        var text = new StackPanel();
        text.Children.Add(new TextBlock { Text = title, FontWeight = FontWeight.SemiBold });
        text.Children.Add(Note(detail));
        dock.Children.Add(text);
        return dock;
    }
}

public sealed class ClientPage : WizardPage
{
    private readonly SetupState _state;
    private readonly TextBox _path;
    private readonly Banner _result = new();

    public ClientPage(SetupState state)
    {
        _state = state;
        _path = Box(state.Settings.ClientFolder);
        _path.TextChanged += (_, _) => { _result.Hide(); Mark(_path, false); };
        var browse = new Button { Content = "Browse...", Height = 30, MinHeight = 0, Padding = new Thickness(14, 0), VerticalContentAlignment = VerticalAlignment.Center };
        browse.Classes.Add("secondary");
        browse.Click += async (_, _) => await BrowseAsync().ConfigureAwait(true);
        var check = new Button { Content = "Check folder", Height = 30, MinHeight = 0, Padding = new Thickness(14, 0), VerticalContentAlignment = VerticalAlignment.Center };
        check.Classes.Add("secondary");
        check.Click += (_, _) => Validate();
        var row = new DockPanel { LastChildFill = true };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(6, 0, 0, 0) };
        buttons.Children.Add(browse);
        buttons.Children.Add(check);
        DockPanel.SetDock(buttons, Dock.Right);
        row.Children.Add(buttons);
        row.Children.Add(_path);
        Children.Add(Labeled("Client folder", row, "The folder that contains the game's executable and its data files."));
        Children.Add(_result);
    }

    public override string Title => "Game client";

    public override string Subtitle => "tPoseidon reads the game data from your own KR 2014 client folder.";

    public override string StepName => "Game client";

    public bool Validate()
    {
        var text = (_path.Text ?? "").Trim().Trim('"');
        if (text.Length == 0)
        {
            Mark(_path, true);
            _result.Show(BannerKind.Error, "Choose your client folder.", "Paste the folder path, or use Browse.");
            return false;
        }

        string full;
        try
        {
            full = Path.GetFullPath(text);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            Mark(_path, true);
            _result.Show(BannerKind.Error, "That is not a folder path.", ex.Message);
            return false;
        }

        var check = ClientFolder.Check(full);
        if (!check.Ok)
        {
            Mark(_path, true);
            var problems = string.Join("; ", check.Problems);
            _result.Show(BannerKind.Error, "That does not look like the KR 2014 client.", $"{char.ToUpperInvariant(problems[0])}{problems[1..]}. Pick the folder you installed the game into.");
            return false;
        }

        IReadOnlyList<ClientFileCopy> added;
        try
        {
            added = ClientFolder.PrepareEnglishMode(full);
        }
        catch (HostingException ex)
        {
            Mark(_path, true);
            _result.Show(BannerKind.Error, ex.Summary, ex.Hint ?? "");
            return false;
        }

        Mark(_path, false);
        _state.Settings.ClientFolder = full;
        _state.Settings.ClientExecutable = check.Executable;
        var addedText = added.Count == 0
            ? ""
            : $" Added {string.Join(", ", added.Select(copy => $"{Path.GetFileName(copy.Target)} (a copy of {Path.GetFileName(copy.Source)})"))}, which the client needs to start.";
        _result.Show(BannerKind.Success, "Found the client.", $"{check.Executable} and the data folder are present.{addedText}");
        return true;
    }

    public override Task<bool> LeaveAsync() => Task.FromResult(Validate());

    private async Task BrowseAsync()
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanPickFolder: true } storage)
        {
            return;
        }

        var picked = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Choose your game client folder", AllowMultiple = false }).ConfigureAwait(true);
        if (picked.Count > 0 && picked[0].TryGetLocalPath() is { } local)
        {
            _path.Text = local;
            Validate();
        }
    }
}

public sealed class PlayersPage : WizardPage
{
    private readonly SetupState _state;
    private readonly ChoiceCard[] _cards;
    private readonly TextBox _address = Box("");
    private readonly TextBlock _addressLabel = new() { FontSize = Tokens.FontSizeCaption, Foreground = Tokens.TextSecondary };
    private readonly StackPanel _addressBox = new() { Spacing = 4 };
    private readonly Banner _ports = new();
    private readonly Banner _error = new();
    private PlayerAudience _audience;

    public PlayersPage(SetupState state)
    {
        _state = state;
        _cards =
        [
            new ChoiceCard("Only this computer", "You play alone, or test. The servers listen on 127.0.0.1."),
            new ChoiceCard("Friends on my network", "Other computers in your home or office connect over the local network."),
            new ChoiceCard("Anyone on the internet", "Players connect with your public IP or a domain name. Needs port forwarding."),
        ];
        for (var i = 0; i < _cards.Length; i++)
        {
            var audience = (PlayerAudience)i;
            _cards[i].Chosen += () => Select(audience);
            Children.Add(_cards[i]);
        }

        if (state.Settings.Network.Audience != PlayerAudience.Local)
        {
            _address.Text = state.Settings.Network.PublicAddress;
        }

        _addressBox.Children.Add(_addressLabel);
        _addressBox.Children.Add(_address);
        _address.TextChanged += (_, _) => { Mark(_address, false); _error.Hide(); };
        Children.Add(_addressBox);
        Children.Add(_error);
        Children.Add(_ports);
        Select(state.Settings.Network.Audience);
    }

    public override string Title => "Who will play";

    public override string Subtitle => "This decides which address the servers give to the game client.";

    public override string StepName => "Players";

    public void Select(PlayerAudience audience)
    {
        var previous = _audience;
        _audience = audience;
        for (var i = 0; i < _cards.Length; i++)
        {
            _cards[i].Selected = i == (int)audience;
        }

        _addressBox.IsVisible = audience != PlayerAudience.Local;
        _addressLabel.Text = audience == PlayerAudience.Lan ? "Address of this computer on your network" : "Public address players connect to (your public IP or domain)";
        var lanDefault = NetworkAddresses.DefaultLan(_state.Settings);
        if (audience == PlayerAudience.Lan && string.IsNullOrWhiteSpace(_address.Text))
        {
            _address.Text = lanDefault;
        }

        if (audience == PlayerAudience.Internet && previous == PlayerAudience.Lan && _address.Text == lanDefault)
        {
            _address.Text = "";
        }

        _error.Hide();
        if (audience == PlayerAudience.Local)
        {
            _ports.Hide();
        }
        else
        {
            var ports = ServerCatalog.PlayerFacingPorts(_state.Settings);
            _ports.Show(
                BannerKind.Info,
                $"Open these TCP ports in your firewall{(audience == PlayerAudience.Internet ? " and forward them on your router" : "")}: {string.Join(", ", ports)}",
                $"On {OsHint.Name}: {OsHint.Firewall(ports)}");
        }
    }

    public override Task<bool> LeaveAsync()
    {
        var address = _address.Text?.Trim() ?? "";
        if (_audience != PlayerAudience.Local)
        {
            if (address.Length == 0)
            {
                Mark(_address, true);
                _error.Show(BannerKind.Error, "No address given.", _audience == PlayerAudience.Lan
                    ? "Enter the address of this computer on your network."
                    : "Enter your public IP address or a domain name that points to this computer.");
                return Task.FromResult(false);
            }

            if (Uri.CheckHostName(address) == UriHostNameType.Unknown)
            {
                Mark(_address, true);
                _error.Show(BannerKind.Error, $"'{address}' is not an IP address or a domain name.", "Use a form like 203.0.113.7 or play.example.org, without http:// or a port.");
                return Task.FromResult(false);
            }
        }

        _state.Settings.Network.Audience = _audience;
        _state.Settings.Network.PublicAddress = _audience == PlayerAudience.Local ? NetworkAddresses.Loopback : address;
        return Task.FromResult(true);
    }
}

public sealed class DatabasePage : WizardPage
{
    private const int DefaultPostgresPort = 5432;
    private const string DefaultAdminLogin = "postgres";

    private readonly SetupState _state;
    private readonly TextBox _host;
    private readonly TextBox _port;
    private readonly TextBox _name;
    private readonly TextBox _adminUser = Box(DefaultAdminLogin);
    private readonly TextBox _adminPassword = Box("", "Admin password", true);
    private readonly Banner _result = new();
    private readonly Button _test = new() { Content = "Test connection", Height = 30, MinHeight = 0, Padding = new Thickness(16, 0), VerticalContentAlignment = VerticalAlignment.Center };
    private bool _passed;

    public DatabasePage(SetupState state)
    {
        _state = state;
        var previous = string.IsNullOrEmpty(state.Settings.Database.Connection)
            ? new NpgsqlConnectionStringBuilder { Host = "localhost", Port = DefaultPostgresPort, Database = DatabaseSettings.DefaultDatabaseName }
            : new NpgsqlConnectionStringBuilder(state.Settings.Database.Connection);
        _host = Box(previous.Host ?? "localhost");
        _port = Box(previous.Port.ToString(CultureInfo.InvariantCulture), null, false, 90);
        _name = Box(previous.Database ?? DatabaseSettings.DefaultDatabaseName);
        var server = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,90,12,*") };
        Put(server, Labeled("PostgreSQL host", _host), 0);
        Put(server, Labeled("Port", _port), 2);
        Put(server, Labeled("Database name", _name), 4);
        Children.Add(server);

        var admin = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,*") };
        Put(admin, Labeled("Admin login", _adminUser), 0);
        Put(admin, Labeled("Admin password", _adminPassword), 2);
        Children.Add(admin);
        Children.Add(Note("tPoseidon creates its own database and a limited login that can only use that database. The admin login is used once for this and the password is never saved."));

        _test.Classes.Add("primary");
        _test.Click += async (_, _) => await RunTestAsync().ConfigureAwait(true);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        row.Children.Add(_test);
        Children.Add(row);
        Children.Add(_result);
        foreach (var box in new[] { _host, _port, _name, _adminUser, _adminPassword })
        {
            box.TextChanged += (_, _) => { _passed = false; _result.Hide(); Mark(box, false); RaiseState(); };
        }
    }

    public override string Title => "Database";

    public override string Subtitle => $"tPoseidon stores accounts and characters in PostgreSQL {DatabaseAdmin.MinimumMajorVersion} or newer.";

    public override string StepName => "Database";

    private static void Put(Grid grid, Control control, int column)
    {
        Grid.SetColumn(control, column);
        grid.Children.Add(control);
    }

    public async Task<bool> RunTestAsync()
    {
        var host = _host.Text?.Trim() ?? "";
        var login = _adminUser.Text?.Trim() ?? "";
        var name = _name.Text?.Trim() ?? "";
        if (host.Length == 0)
        {
            Mark(_host, true);
            _result.Show(BannerKind.Error, "Enter the PostgreSQL host.", "Use localhost if PostgreSQL runs on this computer.");
            return Fail();
        }

        if (!int.TryParse(_port.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var port) || port is < 1 or > 65535)
        {
            Mark(_port, true);
            _result.Show(BannerKind.Error, $"'{_port.Text}' is not a valid port.", $"Use a number from 1 to 65535. PostgreSQL normally listens on {DefaultPostgresPort}.");
            return Fail();
        }

        if (name.Length == 0)
        {
            Mark(_name, true);
            _result.Show(BannerKind.Error, "Enter a database name.", $"tPoseidon creates it for you. The default is {DatabaseSettings.DefaultDatabaseName}.");
            return Fail();
        }

        if (login.Length == 0)
        {
            Mark(_adminUser, true);
            _result.Show(BannerKind.Error, "Enter the admin login.", $"It is usually {DefaultAdminLogin}.");
            return Fail();
        }

        _test.IsEnabled = false;
        _result.Show(BannerKind.Info, $"Connecting to {host}:{port}...");
        var admin = DatabaseAdmin.AdminConnection(host, port, login, _adminPassword.Text ?? "");
        try
        {
            var version = await DatabaseAdmin.ProbeAsync(admin, CancellationToken.None).ConfigureAwait(true);
            _state.AdminConnection = admin;
            _state.DatabaseName = name;
            _state.DatabaseServer = $"{host}:{port}";
            _passed = true;
            _result.Show(BannerKind.Success, $"Connected to PostgreSQL {version.Major}.{version.Minor}.", $"Database '{name}' and a limited login will be created when you continue. They can only use that database.");
            RaiseState();
            return true;
        }
        catch (DatabaseException ex)
        {
            MarkProblem(ex.Problem);
            var detail = ex.Problem == DatabaseProblem.Unreachable ? $"{ex.Hint} {OsHint.InstallGuide}" : ex.Hint;
            _result.Show(BannerKind.Error, ex.Summary, detail);
            return Fail();
        }
        catch (HostingException ex)
        {
            _result.Show(BannerKind.Error, ex.Summary, ex.Hint);
            return Fail();
        }
        finally
        {
            _test.IsEnabled = true;
        }
    }

    private void MarkProblem(DatabaseProblem problem)
    {
        switch (problem)
        {
            case DatabaseProblem.Unreachable:
                Mark(_host, true);
                Mark(_port, true);
                break;
            case DatabaseProblem.UnknownHost or DatabaseProblem.TimedOut:
                Mark(_host, true);
                break;
            case DatabaseProblem.WrongPassword:
                Mark(_adminPassword, true);
                break;
            case DatabaseProblem.LoginRefused or DatabaseProblem.NotAllowed:
                Mark(_adminUser, true);
                break;
            case DatabaseProblem.TooOld:
                Mark(_port, true);
                break;
        }
    }

    private bool Fail()
    {
        _passed = false;
        RaiseState();
        return false;
    }

    public override async Task<bool> LeaveAsync()
    {
        if (_passed)
        {
            return true;
        }

        return await RunTestAsync().ConfigureAwait(true);
    }
}

public sealed class PortsPage : WizardPage
{
    private readonly SetupState _state;
    private readonly (ServerDefinition Server, TextBox Box)[] _rows;
    private readonly Banner _result = new();

    public PortsPage(SetupState state)
    {
        _state = state;
        _rows = ServerCatalog.Enabled(state.Settings)
            .Select(server => (server, Box(server.Port(state.Settings.Ports).ToString(CultureInfo.InvariantCulture), null, false, 100)))
            .ToArray();
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("120,100,*") };
        for (var i = 0; i < _rows.Length; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var name = new TextBlock { Text = _rows[i].Server.Title, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 8) };
            Grid.SetRow(name, i);
            grid.Children.Add(name);
            _rows[i].Box.Margin = new Thickness(0, 0, 0, 8);
            Grid.SetRow(_rows[i].Box, i);
            Grid.SetColumn(_rows[i].Box, 1);
            grid.Children.Add(_rows[i].Box);
            _rows[i].Box.TextChanged += (_, _) => { _result.Hide(); foreach (var r in _rows) Mark(r.Box, false); };
        }

        Children.Add(new Border { Background = Tokens.Surface1, BorderBrush = Tokens.BorderSubtle, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 14), Child = grid });
        Children.Add(Note("The defaults work for most setups. Change a port only if another program already uses it."));
        Children.Add(_result);
    }

    public override string Title => "Ports";

    public override string Subtitle => "Each server listens on its own TCP port.";

    public override string StepName => "Ports";

    public bool Validate()
    {
        var entries = _rows.Select(row => (row.Server.Title, row.Box.Text ?? "")).ToList();
        if (PortPlan.Validate(entries, port => PortProbe.IsListening(port, _state.Settings.Network.BindAddress)) is { } issue)
        {
            foreach (var row in _rows.Where(row => issue.Servers.Contains(row.Server.Title)))
            {
                Mark(row.Box, true);
            }

            _result.Show(BannerKind.Error, issue.Summary, issue.Hint);
            return false;
        }

        foreach (var (server, box) in _rows)
        {
            server.SetPort(_state.Settings.Ports, int.Parse(box.Text!.Trim(), NumberStyles.None, CultureInfo.InvariantCulture));
        }

        return true;
    }

    public override Task<bool> LeaveAsync() => Task.FromResult(Validate());
}

public sealed class AccountPage : WizardPage
{
    private readonly SetupState _state;
    private readonly TextBox _name;
    private readonly TextBox _password = Box("", "Password", true);
    private readonly TextBox _confirm = Box("", "Repeat the password", true);
    private readonly TextBox _other = Box("");
    private readonly ChoiceCard _playCreated = new("The account I'm creating", "Play starts the game client logged in with this account.");
    private readonly ChoiceCard _playOther = new("Another account", "One that already exists in the database.");
    private readonly Control _otherBox;
    private readonly Banner _result = new();
    private bool _playsOther;

    public AccountPage(SetupState state)
    {
        _state = state;
        _name = Box(state.Account);
        _otherBox = Labeled("Existing account name", _other);
        _other.Text = state.PlayWith;
        _playsOther = state.PlayWith.Length > 0;
        Children.Add(Labeled("Account name", _name));
        var passwords = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,*") };
        var a = Labeled("Password", _password);
        var b = Labeled("Repeat the password", _confirm);
        Grid.SetColumn(b, 2);
        passwords.Children.Add(a);
        passwords.Children.Add(b);
        Children.Add(passwords);
        Children.Add(Note("To make one of its characters the GM character, create the character in game first, then run: tposeidon gm register --character <name>"));
        Children.Add(new TextBlock { Text = "Play logs in with", FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 6, 0, 0) });
        _playCreated.Chosen += () => SelectPlay(false);
        _playOther.Chosen += () => SelectPlay(true);
        Children.Add(_playCreated);
        Children.Add(_playOther);
        Children.Add(_otherBox);
        SelectPlay(_playsOther);
        Children.Add(_result);
        foreach (var box in new[] { _name, _password, _confirm, _other })
        {
            box.TextChanged += (_, _) => { _result.Hide(); Mark(box, false); };
        }
    }

    public override string Title => "Your game account";

    public override string Subtitle => "The first account, used to sign in to your server.";

    public override string StepName => "Account";

    public void SelectPlay(bool other)
    {
        _playsOther = other;
        _playCreated.Selected = !other;
        _playOther.Selected = other;
        _otherBox.IsVisible = other;
        _result.Hide();
    }

    public bool Validate()
    {
        var name = _name.Text?.Trim() ?? "";
        var password = _password.Text ?? "";
        var playWith = _playsOther ? _other.Text?.Trim() ?? "" : "";
        if (_playsOther && playWith.Length == 0)
        {
            Mark(_other, true);
            _result.Show(BannerKind.Error, "Type the name of the account Play should use.", "Or choose the account you are creating.");
            return false;
        }

        if (name.Length == 0)
        {
            Mark(_name, true);
            _result.Show(BannerKind.Error, "The account name cannot be empty.");
            return false;
        }

        if (password.Length == 0)
        {
            Mark(_password, true);
            _result.Show(BannerKind.Error, "Choose a password for the account.", "If the account already exists, its password is kept.");
            return false;
        }

        if (password != _confirm.Text)
        {
            Mark(_confirm, true);
            _result.Show(BannerKind.Error, "The passwords did not match.", "Type the same password in both fields and try again.");
            return false;
        }

        _state.Account = name;
        _state.Password = password;
        _state.PlayWith = playWith;
        return true;
    }

    public override Task<bool> LeaveAsync() => Task.FromResult(Validate());
}

public sealed class CashPage : WizardPage
{
    private readonly SetupState _state;
    private readonly (string Mode, ChoiceCard Card)[] _cards;
    private readonly TextBox _start;
    private readonly Control _startBox;
    private readonly Banner _result = new();
    private string _mode = "free";

    public CashPage(SetupState state)
    {
        _state = state;
        _start = Box(state.Settings.ShopStartingCash.ToString(CultureInfo.InvariantCulture), null, false, 160);
        _cards =
        [
            ("free", new ChoiceCard("Free", "Every item costs nothing.")),
            ("balance", new ChoiceCard("Balance", "Each account has cash that you hand out with the cash command.")),
            ("disabled", new ChoiceCard("Disabled", "The shop can be browsed but not used.")),
        ];
        foreach (var (mode, card) in _cards)
        {
            card.Chosen += () => Select(mode);
            Children.Add(card);
        }

        _startBox = Labeled("Cash every account starts with", _start);
        Children.Add(_startBox);
        Children.Add(_result);
        _start.TextChanged += (_, _) => { _result.Hide(); Mark(_start, false); };
        Select(_cards.Any(card => card.Mode == state.Settings.ShopCash) ? state.Settings.ShopCash : "free");
    }

    public override string Title => "Item shop cash";

    public override string Subtitle => "A hosting choice: the original shop billed real money elsewhere.";

    public override string StepName => "Cash shop";

    public void Select(string mode)
    {
        _mode = mode;
        foreach (var (m, card) in _cards)
        {
            card.Selected = m == mode;
        }

        _startBox.IsVisible = mode == "balance";
        _result.Hide();
    }

    public override Task<bool> LeaveAsync()
    {
        long amount = 0;
        if (_mode == "balance" && !(long.TryParse(_start.Text, NumberStyles.None, CultureInfo.InvariantCulture, out amount) && amount >= 0))
        {
            Mark(_start, true);
            _result.Show(BannerKind.Error, $"'{_start.Text}' is not an amount.", "Use a whole number of zero or more.");
            return Task.FromResult(false);
        }

        _state.Settings.ShopCash = _mode;
        if (_mode == "balance")
        {
            _state.Settings.ShopStartingCash = amount;
        }

        return Task.FromResult(true);
    }
}

public sealed class DataPage : WizardPage
{
    private const double ProvisionShare = 8;
    private const double MigrationShare = 84;
    private const double AccountShare = 94;
    private static readonly TimeSpan TweenFrame = TimeSpan.FromMilliseconds(16);

    private readonly SetupState _state;
    private readonly ProgressBar _data = Bar();
    private readonly ProgressBar _migrate = Bar();
    private readonly TextBlock _dataLabel = new() { Foreground = Tokens.TextSecondary, FontSize = Tokens.FontSizeCaption, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _migrateLabel = new() { Foreground = Tokens.TextSecondary, FontSize = Tokens.FontSizeCaption, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _dataPercent = new() { FontSize = Tokens.FontSizeCaption, Foreground = Tokens.TextSecondary };
    private readonly TextBlock _migratePercent = new() { FontSize = Tokens.FontSizeCaption, Foreground = Tokens.TextSecondary };
    private readonly Banner _result = new();
    private readonly Button _retry = new() { Content = "Try again", Height = 30, MinHeight = 0, Padding = new Thickness(16, 0), VerticalContentAlignment = VerticalAlignment.Center, IsVisible = false, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly Dictionary<ProgressBar, (double Target, TextBlock Percent)> _targets = [];
    private readonly DispatcherTimer _tween = new() { Interval = TweenFrame };
    private bool _running;
    private bool _done;

    public DataPage(SetupState state)
    {
        _state = state;
        Children.Add(Section("Game data from your client", _data, _dataLabel, _dataPercent));
        Children.Add(Section("Database tables", _migrate, _migrateLabel, _migratePercent));
        Children.Add(_result);
        _retry.Classes.Add("secondary");
        _retry.Click += (_, _) => Begin();
        Children.Add(_retry);
        _dataLabel.Text = "Waiting";
        _migrateLabel.Text = "Waiting";
        _tween.Tick += (_, _) => StepTween();
    }

    public override string Title => "Preparing your server";

    public override string Subtitle => "tPoseidon generates the game data and prepares the database. This runs once.";

    public override string StepName => "Game data";

    public override bool CanContinue => _done;

    public override bool CanGoBack => !_running;

    public string AccountOutcome { get; private set; } = "";

    private static ProgressBar Bar() => new() { Minimum = 0, Maximum = 100, Height = 6, MinHeight = 0, Foreground = Tokens.Accent, Background = Tokens.Surface2, CornerRadius = new CornerRadius(3) };

    private static Border Section(string title, ProgressBar bar, TextBlock label, TextBlock percent)
    {
        var head = new DockPanel();
        DockPanel.SetDock(percent, Dock.Right);
        head.Children.Add(percent);
        head.Children.Add(new TextBlock { Text = title, FontWeight = FontWeight.SemiBold });
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(head);
        stack.Children.Add(bar);
        stack.Children.Add(label);
        return new Border { Background = Tokens.Surface1, BorderBrush = Tokens.BorderSubtle, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 14), Child = stack };
    }

    public override void Entered()
    {
        if (!_done && !_running)
        {
            Begin();
        }
    }

    private async void Begin()
    {
        _done = false;
        _retry.IsVisible = false;
        _result.Hide();
        SetNow(_data, _dataPercent, 0);
        SetNow(_migrate, _migratePercent, 0);
        _dataLabel.Text = "Waiting";
        _migrateLabel.Text = "Waiting";
        await RunAsync().ConfigureAwait(true);
    }

    private async Task RunAsync()
    {
        _running = true;
        RaiseState();
        var paths = _state.Paths;
        var settings = _state.Settings;
        try
        {
            var dataProgress = new Progress<StepProgress>(step =>
            {
                _dataLabel.Text = step.Message;
                MoveTo(_data, _dataPercent, (step.Step - 1) * 100.0 / step.Steps);
            });
            await Task.Run(() => ServerDataStep.Run(paths, settings, dataProgress)).ConfigureAwait(true);
            MoveTo(_data, _dataPercent, 100);
            _dataLabel.Text = "Game data generated.";

            _migrateLabel.Text = "Creating the database and login...";
            MoveTo(_migrate, _migratePercent, 2);
            var login = await DatabaseAdmin.ProvisionOwnLoginAsync(_state.AdminConnection, _state.DatabaseName, CancellationToken.None).ConfigureAwait(true);
            settings.Database.Connection = login.Connection;
            settings.Save(paths);
            MoveTo(_migrate, _migratePercent, ProvisionShare);

            var migrationProgress = new Progress<MigrationStep>(step =>
            {
                _migrateLabel.Text = $"Applying {step.Name} ({step.Index} of {step.Count})...";
                MoveTo(_migrate, _migratePercent, ProvisionShare + (MigrationShare - ProvisionShare) * step.Index / step.Count);
            });
            await DatabaseAdmin.ApplyMigrationsAsync(paths, settings.Database, migrationProgress, CancellationToken.None).ConfigureAwait(true);
            MoveTo(_migrate, _migratePercent, MigrationShare);

            _migrateLabel.Text = $"Creating the account '{_state.Account}'...";
            var account = await Task.Run(() => DatabaseAdmin.EnsureAccount(settings.Database, _state.Account, _state.Password)).ConfigureAwait(true);
            MoveTo(_migrate, _migratePercent, AccountShare);
            AccountOutcome = account.Created ? $"Created account '{_state.Account}'." : $"Account '{_state.Account}' already exists; kept its password.";
            var wanted = _state.PlayWith.Length == 0 ? _state.Account : _state.PlayWith;
            var playAccount = await Task.Run(() => PlayAccount.Choose(settings, new DatabaseAccountDirectory(settings.Database), wanted)).ConfigureAwait(true);
            AccountOutcome += $" Play logs in with '{playAccount}'.";
            settings.Save(paths);

            MoveTo(_migrate, _migratePercent, 100);
            _migrateLabel.Text = "Up to date.";
            _result.Show(BannerKind.Success, "Everything is ready.", $"Game data is generated and the database tables are up to date. {AccountOutcome}");
            _done = true;
        }
        catch (HostingException ex)
        {
            Failed(ex.Summary, ex.Hint);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Failed("Reading the client or writing the server data failed.", ex.Message);
        }
        finally
        {
            _running = false;
            RaiseState();
        }
    }

    private void Failed(string title, string? detail)
    {
        _result.Show(BannerKind.Error, title, detail);
        _retry.IsVisible = true;
    }

    private void MoveTo(ProgressBar bar, TextBlock percent, double value)
    {
        if (Motion.Shared.Reduced)
        {
            SetNow(bar, percent, value);
            return;
        }

        _targets[bar] = (value, percent);
        _tween.Start();
    }

    private void SetNow(ProgressBar bar, TextBlock percent, double value)
    {
        _targets.Remove(bar);
        bar.Value = value;
        percent.Text = $"{value:0} %";
    }

    private void StepTween()
    {
        foreach (var (bar, (target, percent)) in _targets.ToList())
        {
            var next = bar.Value + (target - bar.Value) * 0.18;
            if (Math.Abs(target - next) < 0.4)
            {
                SetNow(bar, percent, target);
                continue;
            }

            bar.Value = next;
            percent.Text = $"{next:0} %";
        }

        if (_targets.Count == 0)
        {
            _tween.Stop();
        }
    }
}

public sealed class FinishPage : WizardPage
{
    private readonly SetupState _state;
    private readonly DataPage _data;
    private readonly StackPanel _summary = new() { Spacing = 8 };

    public FinishPage(SetupState state, DataPage data, Action<bool> finish)
    {
        _state = state;
        _data = data;
        var badge = new Border { Width = 56, Height = 56, CornerRadius = new CornerRadius(28), Background = Tokens.SuccessTint, BorderBrush = Tokens.Success, BorderThickness = new Thickness(2), HorizontalAlignment = HorizontalAlignment.Left, Child = Tokens.Glyphs(Glyph.Check, 24, Tokens.Success) };
        Children.Add(badge);
        Children.Add(new Border { Background = Tokens.Surface1, BorderBrush = Tokens.BorderSubtle, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 14), Child = _summary });
        var start = new Button { Content = "Start servers", Height = 34, MinHeight = 0, Padding = new Thickness(22, 0), VerticalContentAlignment = VerticalAlignment.Center, FontWeight = FontWeight.SemiBold };
        start.Classes.Add("primary");
        start.Click += (_, _) => finish(true);
        var open = new Button { Content = "Open panel", Height = 34, MinHeight = 0, Padding = new Thickness(22, 0), VerticalContentAlignment = VerticalAlignment.Center };
        open.Classes.Add("secondary");
        open.Click += (_, _) => finish(false);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(start);
        row.Children.Add(open);
        Children.Add(row);
        Children.Add(Note($"Settings are saved in {_state.Paths.SettingsFile}. They include the database password, so keep that folder private."));
    }

    public override string Title => "You're all set";

    public override string Subtitle => "tPoseidon is ready to run your server.";

    public override string StepName => "Finish";

    public override bool HidesFooter => true;

    public override void Entered()
    {
        var settings = _state.Settings;
        _summary.Children.Clear();
        Add("Game client", settings.ClientFolder);
        Add("Players", settings.Network.Audience switch
        {
            PlayerAudience.Local => "Only this computer (127.0.0.1)",
            PlayerAudience.Lan => $"Local network ({settings.Network.PublicAddress})",
            _ => $"Internet ({settings.Network.PublicAddress})",
        });
        Add("Database", $"{_state.DatabaseName} on {_state.DatabaseServer}");
        Add("Account", _data.AccountOutcome.Length > 0 ? _data.AccountOutcome : _state.Account);
        Add("Item shop", settings.ShopCash == "balance" ? $"balance, {settings.ShopStartingCash.ToString("N0", CultureInfo.InvariantCulture)} to start" : settings.ShopCash);
    }

    private void Add(string label, string value)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("110,*") };
        grid.Children.Add(new TextBlock { Text = label, Foreground = Tokens.TextSecondary });
        var v = new TextBlock { Text = value, TextTrimming = TextTrimming.CharacterEllipsis };
        ToolTip.SetTip(v, value);
        Grid.SetColumn(v, 1);
        grid.Children.Add(v);
        _summary.Children.Add(grid);
    }
}

public sealed class SetupWindow : Window
{
    private readonly SetupState _state;
    private readonly List<WizardPage> _pages = [];
    private readonly List<(Border Badge, GlyphView Check, TextBlock Number, TextBlock Label)> _stepViews = [];
    private readonly ContentControl _host = new();
    private readonly TextBlock _title = new() { FontSize = 20, FontWeight = FontWeight.SemiBold };
    private readonly TextBlock _subtitle = new() { Foreground = Tokens.TextSecondary, TextWrapping = TextWrapping.Wrap };
    private readonly Button _back = new() { Content = "Back", Height = 32, MinHeight = 0, Padding = new Thickness(18, 0), VerticalContentAlignment = VerticalAlignment.Center };
    private readonly Button _next = new() { Height = 32, MinHeight = 0, Padding = new Thickness(22, 0), VerticalContentAlignment = VerticalAlignment.Center, FontWeight = FontWeight.SemiBold };
    private readonly Border _footer;
    private readonly Transitions _pageTransitions = [new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(220) }, new ThicknessTransition { Property = MarginProperty, Duration = TimeSpan.FromMilliseconds(260), Easing = new Avalonia.Animation.Easings.CubicEaseOut() }];
    private int _index = -1;
    private bool _moving;

    public SetupWindow(InstallPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        Title = "tPoseidon setup";
        Width = 800;
        Height = 580;
        MinWidth = 720;
        MinHeight = 540;
        FontFamily = Tokens.UiFont;
        FontSize = Tokens.FontSizeBody;
        Foreground = Tokens.TextPrimary;
        Background = Tokens.Surface0;
        UseLayoutRounding = true;
        Icon = PanelAssets.WindowIcon();
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _state = new SetupState(paths, LoadExisting(paths));
        var data = new DataPage(_state);
        _pages.AddRange([new WelcomePage(_state), new ClientPage(_state), new PlayersPage(_state), new DatabasePage(_state), new PortsPage(_state), new AccountPage(_state), new CashPage(_state), data, new FinishPage(_state, data, OnFinish)]);
        foreach (var page in _pages)
        {
            page.StateChanged += UpdateFooter;
        }

        _back.Classes.Add("secondary");
        _next.Classes.Add("primary");
        _back.Click += (_, _) => { if (_index > 0 && _pages[_index].CanGoBack) Go(_index - 1); };
        _next.Click += async (_, _) => await NextAsync().ConfigureAwait(true);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(_back);
        buttons.Children.Add(_next);
        _footer = new Border { Padding = new Thickness(28, 12), BorderBrush = Tokens.BorderSubtle, BorderThickness = new Thickness(0, 1, 0, 0), Background = Tokens.Surface1, Child = buttons };

        var right = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        var head = new StackPanel { Margin = new Thickness(28, 22, 28, 12), Spacing = 4 };
        head.Children.Add(_title);
        head.Children.Add(_subtitle);
        right.Children.Add(head);
        var scroll = new ScrollViewer { Content = _host, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, ClipToBounds = true };
        Grid.SetRow(scroll, 1);
        right.Children.Add(scroll);
        Grid.SetRow(_footer, 2);
        right.Children.Add(_footer);

        var root = new Grid { ColumnDefinitions = new ColumnDefinitions("212,*") };
        root.Children.Add(BuildSidebar());
        Grid.SetColumn(right, 1);
        root.Children.Add(right);
        Content = root;

        Activated += (_, _) => Motion.Shared.SetFocused(true);
        Deactivated += (_, _) => Motion.Shared.SetFocused(false);
        Opened += (_, _) => Go(0, false);
        Closing += (_, e) => e.Cancel = _pages[Math.Max(0, _index)] is DataPage { CanGoBack: false };
    }

    public event Action<bool>? Completed;

    private static HostSettings LoadExisting(InstallPaths paths)
    {
        try
        {
            return HostSettings.Load(paths) ?? new HostSettings();
        }
        catch (HostingException ex)
        {
            LogHub.Write(LogHub.PanelSource, LogLevel.Warn, $"{ex.Summary} Setup starts from the defaults.");
            return new HostSettings();
        }
    }

    private Border BuildSidebar()
    {
        var panel = new Avalonia.Controls.Panel { Background = Tokens.Surface1 };
        panel.Children.Add(new SeaHost(new ListSeaHandler(), Motion.Shared));
        var stack = new StackPanel { Margin = new Thickness(20, 22, 12, 16), Spacing = 0 };
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 0, 0, 22) };
        brand.Children.Add(new Image { Source = new Bitmap(AssetLoader.Open(PanelAssets.Uri("emblem-48.png"))), Width = 34, Height = 34 });
        var names = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        names.Children.Add(new TextBlock { Text = "tPoseidon", FontWeight = FontWeight.SemiBold, FontSize = 15 });
        names.Children.Add(Tokens.Caption("First-time setup"));
        brand.Children.Add(names);
        stack.Children.Add(brand);
        var number = 0;
        foreach (var page in _pages.Where(p => p.StepName is not null))
        {
            number++;
            var badge = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(11), BorderThickness = new Thickness(1), VerticalAlignment = VerticalAlignment.Center };
            var num = new TextBlock { Text = number.ToString(CultureInfo.InvariantCulture), FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeight.SemiBold };
            var check = Tokens.Glyphs(Glyph.Check, 10, Tokens.OnAccent);
            check.IsVisible = false;
            var face = new Avalonia.Controls.Panel();
            face.Children.Add(num);
            face.Children.Add(check);
            badge.Child = face;
            var label = new TextBlock { Text = page.StepName, VerticalAlignment = VerticalAlignment.Center };
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 0, 0, 14) };
            row.Children.Add(badge);
            row.Children.Add(label);
            stack.Children.Add(row);
            _stepViews.Add((badge, check, num, label));
        }

        panel.Children.Add(stack);
        return new Border { BorderBrush = Tokens.BorderSubtle, BorderThickness = new Thickness(0, 0, 1, 0), Child = panel, ClipToBounds = true };
    }

    public void Go(int index, bool animate = true)
    {
        if (_index >= 0 && index != _index)
        {
            _pages[_index].Left();
        }

        var forward = index >= _index;
        _index = index;
        var page = _pages[index];
        _title.Text = page.Title;
        _subtitle.Text = page.Subtitle;
        RefreshSteps();
        UpdateFooter();
        if (animate && !Motion.Shared.Reduced)
        {
            _host.Transitions = null;
            _host.Opacity = 0;
            _host.Margin = new Thickness(forward ? 36 : -36, 0, forward ? -36 : 36, 0);
            _host.Content = page;
            Dispatcher.UIThread.Post(() =>
            {
                _host.Transitions = _pageTransitions;
                _host.Opacity = 1;
                _host.Margin = new Thickness(0);
            }, DispatcherPriority.Render);
        }
        else
        {
            _host.Transitions = null;
            _host.Opacity = 1;
            _host.Margin = new Thickness(0);
            _host.Content = page;
        }

        page.Entered();
    }

    private async Task NextAsync()
    {
        if (_moving || _index < 0 || _index >= _pages.Count - 1)
        {
            return;
        }

        _moving = true;
        _next.IsEnabled = false;
        try
        {
            if (await _pages[_index].LeaveAsync().ConfigureAwait(true))
            {
                Go(_index + 1);
            }
        }
        finally
        {
            _moving = false;
            UpdateFooter();
        }
    }

    private void RefreshSteps()
    {
        for (var i = 0; i < _stepViews.Count; i++)
        {
            var (badge, check, num, label) = _stepViews[i];
            var step = i + 1;
            var done = step < _index;
            var current = step == _index;
            badge.Background = done ? Tokens.Success : current ? Tokens.Accent : Brushes.Transparent;
            badge.BorderBrush = done ? Tokens.Success : current ? Tokens.Accent : Tokens.TextMuted;
            num.Foreground = done || current ? Tokens.OnAccent : Tokens.TextSecondary;
            num.IsVisible = !done;
            check.IsVisible = done;
            label.Foreground = current ? Tokens.TextPrimary : done ? Tokens.TextSecondary : Tokens.TextMuted;
            label.FontWeight = current ? FontWeight.SemiBold : FontWeight.Normal;
        }
    }

    private void UpdateFooter()
    {
        if (_index < 0)
        {
            return;
        }

        var page = _pages[_index];
        _footer.IsVisible = !page.HidesFooter;
        _back.IsVisible = _index > 0;
        _back.IsEnabled = page.CanGoBack;
        _next.Content = page.NextText;
        _next.IsEnabled = page.CanContinue && !_moving;
    }

    private void OnFinish(bool start)
    {
        Completed?.Invoke(start);
        Close();
    }
}
