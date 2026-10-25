using System.Globalization;
using Npgsql;
using tPoseidon.Cli.Terminal;
using tPoseidon.Hosting;
using tPoseidon.Hosting.Database;
using tPoseidon.Hosting.Servers;

namespace tPoseidon.Cli.Commands;

internal sealed class SetupCommand
{
    private const int DefaultPostgresPort = 5432;

    private readonly InstallPaths _paths;
    private readonly CommandLine _commandLine;
    private readonly Prompt _prompt;

    public SetupCommand(InstallPaths paths, CommandLine commandLine, Prompt prompt)
    {
        _paths = paths;
        _commandLine = commandLine;
        _prompt = prompt;
    }

    public async Task<int> RunAsync(CancellationToken ct)
    {
        _commandLine.RejectUnknown(
            "client", "audience", "public-ip",
            "db-host", "db-port", "db-name", "db-admin-user", "db-admin-password", "db-user", "db-password",
            "admin-user", "admin-password", "play-account", "cash", "cash-start");
        var settings = HostSettings.Load(_paths) ?? new HostSettings();

        Console.WriteLine($"tPoseidon {VersionCommand.Current} setup");
        Console.WriteLine($"Folder: {_paths.Root}");
        Console.WriteLine();

        var supervisor = new ServerSupervisor(_paths, settings);
        if (supervisor.AnyRunning())
        {
            throw new CliException("The servers are running. Run 'tposeidon stop' before changing the setup.");
        }

        AskClientFolder(settings);
        AskNetwork(settings);
        AskCash(settings);
        await ConfigureDatabaseAsync(settings, ct).ConfigureAwait(false);
        settings.Save(_paths);

        Console.WriteLine();
        Console.WriteLine("4. Game data");
        ServerDataStep.Run(_paths, settings, new ConsoleProgress<StepProgress>(step => Console.WriteLine("   " + step.Message)));

        Console.WriteLine();
        Console.WriteLine("5. Database tables");
        await DatabaseAdmin.ApplyMigrationsAsync(_paths, settings.Database, null, ct).ConfigureAwait(false);
        Console.WriteLine("   Up to date.");
        CreateAccount(settings);

        settings.Save(_paths);
        Console.WriteLine();
        Console.WriteLine($"Saved {_paths.SettingsFile}. It holds the database password, so keep it private.");
        Console.WriteLine();
        Console.WriteLine("Next: tposeidon start");
        return 0;
    }

    private void AskClientFolder(HostSettings settings)
    {
        Console.WriteLine("1. Game client");
        Console.WriteLine("   tPoseidon reads the game data from your own KR 2014 client folder.");
        var given = _commandLine.Option("client");
        while (true)
        {
            var folder = given ?? _prompt.Ask("   Client folder", settings.ClientFolder);
            given = null;
            if (string.IsNullOrWhiteSpace(folder))
            {
                if (!_prompt.Interactive)
                {
                    throw new CliException("No client folder given. Pass --client <folder>.");
                }

                continue;
            }

            var full = Path.GetFullPath(folder.Trim().Trim('"'));
            var check = ClientFolder.Check(full);
            if (check.Ok)
            {
                settings.ClientFolder = full;
                settings.ClientExecutable = check.Executable;
                Console.WriteLine($"   Found the client ({check.Executable}).");
                foreach (var copy in ClientFolder.PrepareEnglishMode(full))
                {
                    Console.WriteLine($"   Added {Path.GetFileName(copy.Target)} (a copy of {Path.GetFileName(copy.Source)}), which the client needs to start.");
                }
                Console.WriteLine();
                return;
            }

            var problems = $"That does not look like the KR 2014 client: {string.Join("; ", check.Problems)}.";
            if (!_prompt.Interactive)
            {
                throw new CliException(problems);
            }

            Console.WriteLine("   " + problems);
        }
    }

    private void AskNetwork(HostSettings settings)
    {
        Console.WriteLine("2. Who will play");
        var audience = ParseAudience(_commandLine.Option("audience")) ?? _prompt.Choose(
            "   Who connects to this server?",
            new (PlayerAudience, string)[]
            {
                (PlayerAudience.Local, "only this computer"),
                (PlayerAudience.Lan, "computers on my local network"),
                (PlayerAudience.Internet, "players over the internet"),
            },
            settings.Network.Audience);
        settings.Network.Audience = audience;

        var givenAddress = _commandLine.Option("public-ip");
        settings.Network.PublicAddress = audience switch
        {
            PlayerAudience.Local => NetworkAddresses.Loopback,
            PlayerAudience.Lan => givenAddress ?? _prompt.Ask("   Address of this computer on your network", NetworkAddresses.DefaultLan(settings)),
            PlayerAudience.Internet => givenAddress ?? _prompt.Ask("   Public address players connect to (your public IP or domain)", settings.Network.Audience == PlayerAudience.Internet ? settings.Network.PublicAddress : ""),
            _ => throw new CliException($"unknown audience {audience}"),
        };
        if (string.IsNullOrWhiteSpace(settings.Network.PublicAddress))
        {
            throw new CliException("No public address given. Pass --public-ip <address>.");
        }

        if (audience == PlayerAudience.Local)
        {
            Console.WriteLine("   Only this computer. The servers listen on 127.0.0.1.");
        }
        else
        {
            Console.WriteLine($"   Players connect to {settings.Network.PublicAddress}. Open these TCP ports in your firewall{(audience == PlayerAudience.Internet ? " and forward them on your router" : "")}:");
            Console.WriteLine("   " + string.Join(", ", ServerCatalog.PlayerFacingPorts(settings)));
        }

        Console.WriteLine();
    }

    private void AskCash(HostSettings settings)
    {
        Console.WriteLine("   Item shop cash (a hosting choice; the original shop billed real money elsewhere):");
        var given = _commandLine.Option("cash")?.Trim().ToLowerInvariant();
        var mode = given ?? _prompt.Choose(
            "   How should the item shop charge?",
            new (string, string)[]
            {
                ("free", "free: every item costs nothing"),
                ("balance", "balance: each account has cash you hand out with 'tposeidon cash'"),
                ("disabled", "disabled: the shop can be browsed but not used"),
            },
            settings.ShopCash);
        if (mode is not ("free" or "balance" or "disabled"))
        {
            throw new CliException($"--cash must be free, balance or disabled (got '{mode}').");
        }

        settings.ShopCash = mode;
        if (mode == "balance")
        {
            var start = _commandLine.Option("cash-start") ?? _prompt.Ask("   Cash every account starts with", settings.ShopStartingCash.ToString(CultureInfo.InvariantCulture));
            settings.ShopStartingCash = long.TryParse(start, NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount) && amount >= 0
                ? amount
                : throw new CliException($"'{start}' is not an amount.");
        }

        Console.WriteLine();
    }

    private async Task ConfigureDatabaseAsync(HostSettings settings, CancellationToken ct)
    {
        Console.WriteLine("3. Database");
        Console.WriteLine($"   tPoseidon stores accounts and characters in PostgreSQL {DatabaseAdmin.MinimumMajorVersion} or newer.");
        Console.WriteLine("   If it isn't installed yet, see docs/install/postgresql.md.");

        var previous = string.IsNullOrEmpty(settings.Database.Connection)
            ? new NpgsqlConnectionStringBuilder { Host = "localhost", Port = DefaultPostgresPort }
            : new NpgsqlConnectionStringBuilder(settings.Database.Connection);
        var host = _commandLine.Option("db-host") ?? _prompt.Ask("   PostgreSQL host", previous.Host ?? "localhost");
        var port = ParsePort(_commandLine.Option("db-port") ?? _prompt.Ask("   PostgreSQL port", previous.Port.ToString(CultureInfo.InvariantCulture)));
        var databaseName = _commandLine.Option("db-name") ?? _prompt.Ask("   Database name", previous.Database ?? DatabaseSettings.DefaultDatabaseName);

        var existingLogin = _commandLine.Option("db-user");
        var useExistingLogin = existingLogin is not null
            || (_commandLine.Option("db-admin-user") is null && _prompt.Interactive && _prompt.Confirm("   Do you already have a login for tPoseidon with its own database?", defaultValue: false));
        if (useExistingLogin)
        {
            var user = existingLogin ?? _prompt.Ask("   Login", previous.Username ?? DatabaseSettings.DefaultAppLogin);
            var password = _commandLine.Option("db-password") ?? _prompt.AskSecret("   Password");
            var connection = new NpgsqlConnectionStringBuilder { Host = host, Port = port, Database = databaseName, Username = user, Password = password }.ConnectionString;
            var version = await DatabaseAdmin.CheckServerAsync(connection, ct).ConfigureAwait(false);
            Console.WriteLine($"   Connected to PostgreSQL {version.Major}.{version.Minor}.");
            settings.Database.Connection = connection;
            return;
        }

        Console.WriteLine("   tPoseidon will create its own database and login. It needs an admin login once for that; the admin password is not saved.");
        var adminUser = _commandLine.Option("db-admin-user") ?? _prompt.Ask("   Admin login", "postgres");
        var adminPassword = _commandLine.Option("db-admin-password") ?? _prompt.AskSecret($"   Password for {adminUser}");
        var admin = DatabaseAdmin.AdminConnection(host, port, adminUser, adminPassword);
        var serverVersion = await DatabaseAdmin.CheckServerAsync(admin, ct).ConfigureAwait(false);
        Console.WriteLine($"   Connected to PostgreSQL {serverVersion.Major}.{serverVersion.Minor}.");

        var provisioned = await DatabaseAdmin.ProvisionOwnLoginAsync(admin, databaseName, ct).ConfigureAwait(false);
        settings.Database.Connection = provisioned.Connection;
        Console.WriteLine($"   Database '{databaseName}' and login '{provisioned.Login}' are ready (the login can only use its own database).");
    }

    private void CreateAccount(HostSettings settings)
    {
        Console.WriteLine();
        Console.WriteLine("6. Your game account");
        var username = (_commandLine.Option("admin-user") ?? _prompt.Ask("   Account name", "admin")).Trim();
        if (username.Length == 0)
        {
            throw new CliException("The account name cannot be empty.");
        }

        var password = _commandLine.Option("admin-password")
            ?? (_prompt.Interactive ? AskNewPassword("   Password (leave empty to keep an existing account's password)") : null);

        var result = DatabaseAdmin.EnsureAccount(settings.Database, username, string.IsNullOrEmpty(password) ? null : password);
        Console.WriteLine(result.Created
            ? $"   Created account '{username}'."
            : $"   Account '{username}' already exists; kept its password.");
        ChoosePlayAccount(settings, username);
        Console.WriteLine("   To make one of its characters the GM character, create the character in game, then run:");
        Console.WriteLine("   tposeidon gm --character <name>");
    }

    private void ChoosePlayAccount(HostSettings settings, string created)
    {
        Console.WriteLine();
        Console.WriteLine("   Play logs in with this account when you start the game client.");
        var wanted = (_commandLine.Option("play-account") ?? _prompt.Ask("   Play logs in with", created)).Trim();
        var directory = new DatabaseAccountDirectory(settings.Database);
        if (directory.Find(wanted) is null)
        {
            if (!_prompt.Interactive || !_prompt.Confirm($"   There is no account named '{wanted}'. Create it now?", defaultValue: true))
            {
                throw new CliException($"There is no account named '{wanted}'. Create it first, or pick one that exists.");
            }

            DatabaseAdmin.EnsureAccount(settings.Database, wanted, AskNewPassword($"   Password for '{wanted}'"));
            Console.WriteLine($"   Created account '{wanted}'.");
        }

        var chosen = PlayAccount.Choose(settings, directory, wanted);
        Console.WriteLine($"   Play logs in with '{chosen}'. Change it later with: tposeidon play --account <name>");
    }

    private string AskNewPassword(string question)
    {
        while (true)
        {
            var password = _prompt.AskSecret(question);
            if (password.Length == 0 || _prompt.AskSecret("   Same password again") == password)
            {
                return password;
            }

            Console.WriteLine("   The passwords did not match. Try again.");
        }
    }

    private static int ParsePort(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port) && port is > 0 and < 65536
            ? port
            : throw new CliException($"'{value}' is not a port number.");

    private static PlayerAudience? ParseAudience(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        null => null,
        "LOCAL" => PlayerAudience.Local,
        "LAN" => PlayerAudience.Lan,
        "INTERNET" => PlayerAudience.Internet,
        _ => throw new CliException($"--audience must be local, lan or internet (got '{value}')."),
    };
}
