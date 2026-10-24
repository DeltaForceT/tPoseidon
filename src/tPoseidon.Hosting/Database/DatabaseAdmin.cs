using System.Security.Cryptography;
using Npgsql;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Data.Postgres;

namespace tPoseidon.Hosting.Database;

public sealed record AdminAccountResult(uint AccountId, bool Created);

public sealed record ProvisionedLogin(string Connection, string Login);

public static class DatabaseAdmin
{
    public const int MinimumMajorVersion = 14;
    public const int TestedMajorVersion = 16;
    public const string AdminDatabase = "postgres";
    private const string PasswordAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
    private const int GeneratedPasswordLength = 32;
    private const int ProbeTimeoutSeconds = 3;

    public static string AdminConnection(string host, int port, string user, string password) =>
        new NpgsqlConnectionStringBuilder { Host = host, Port = port, Database = AdminDatabase, Username = user, Password = password }.ConnectionString;

    public static async Task<Version> CheckServerAsync(string connectionString, CancellationToken ct)
    {
        var target = new NpgsqlConnectionStringBuilder(connectionString);
        Version version;
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(ct).ConfigureAwait(false);
            version = connection.PostgreSqlVersion;
        }
        catch (Exception ex) when (ex is NpgsqlException or TimeoutException)
        {
            throw DatabaseErrors.Explain(ex, target);
        }

        if (version.Major < MinimumMajorVersion)
        {
            throw DatabaseErrors.TooOld(version, target, MinimumMajorVersion, TestedMajorVersion);
        }

        return version;
    }

    public static Task<Version> ProbeAsync(string connectionString, CancellationToken ct) =>
        CheckServerAsync(QuickConnection(connectionString), ct);

    public static string QuickConnection(string connectionString) =>
        new NpgsqlConnectionStringBuilder(connectionString) { Timeout = ProbeTimeoutSeconds, Pooling = false }.ConnectionString;

    public static async Task WaitUntilReadyAsync(string connectionString, TimeSpan timeout, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            try
            {
                await CheckServerAsync(connectionString, ct).ConfigureAwait(false);
                return;
            }
            catch (HostingException) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
            }
        }
    }

    public static async Task<ProvisionedLogin> ProvisionOwnLoginAsync(string adminConnection, string databaseName, CancellationToken ct)
    {
        var login = DatabaseSettings.DefaultAppLogin;
        var password = RandomNumberGenerator.GetString(PasswordAlphabet, GeneratedPasswordLength);
        var connection = await ProvisionAsync(adminConnection, databaseName, login, password, ct).ConfigureAwait(false);
        await CheckServerAsync(connection, ct).ConfigureAwait(false);
        return new ProvisionedLogin(connection, login);
    }

    public static async Task<string> ProvisionAsync(string adminConnection, string databaseName, string appLogin, string appPassword, CancellationToken ct)
    {
        var admin = new NpgsqlConnectionStringBuilder(adminConnection);
        try
        {
            await using var connection = new NpgsqlConnection(adminConnection);
            await connection.OpenAsync(ct).ConfigureAwait(false);

            var roleExists = await ExistsAsync(connection, "SELECT 1 FROM pg_roles WHERE rolname = @name", appLogin, ct).ConfigureAwait(false);
            var roleCommand = roleExists
                ? $"ALTER ROLE {Identifier(appLogin)} WITH LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE PASSWORD {Literal(appPassword)}"
                : $"CREATE ROLE {Identifier(appLogin)} WITH LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE PASSWORD {Literal(appPassword)}";
            await ExecuteAsync(connection, roleCommand, ct).ConfigureAwait(false);

            if (!await ExistsAsync(connection, "SELECT 1 FROM pg_database WHERE datname = @name", databaseName, ct).ConfigureAwait(false))
            {
                await ExecuteAsync(connection, $"CREATE DATABASE {Identifier(databaseName)} OWNER {Identifier(appLogin)} ENCODING 'UTF8' TEMPLATE template0", ct).ConfigureAwait(false);
            }
            else
            {
                await ExecuteAsync(connection, $"ALTER DATABASE {Identifier(databaseName)} OWNER TO {Identifier(appLogin)}", ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is NpgsqlException or TimeoutException)
        {
            throw DatabaseErrors.Explain(ex, admin);
        }

        return new NpgsqlConnectionStringBuilder
        {
            Host = admin.Host,
            Port = admin.Port,
            Database = databaseName,
            Username = appLogin,
            Password = appPassword,
        }.ConnectionString;
    }

    public static async Task ApplyMigrationsAsync(InstallPaths paths, DatabaseSettings settings, IProgress<MigrationStep>? progress, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(settings);
        if (!Directory.Exists(paths.Migrations))
        {
            throw new HostingException($"The database migrations are missing from {paths.Migrations}. Unpack the full release package again.");
        }

        try
        {
            await using var dataSource = NpgsqlDataSource.Create(settings.Connection);
            await PostgresMigrations.ApplyAsync(dataSource, paths.Migrations, settings.Schema, progress, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is NpgsqlException or TimeoutException)
        {
            throw DatabaseErrors.Explain(ex, new NpgsqlConnectionStringBuilder(settings.Connection));
        }
    }

    public static AdminAccountResult EnsureAccount(DatabaseSettings settings, string username, string? password)
    {
        ArgumentNullException.ThrowIfNull(settings);
        using var dataSource = NpgsqlDataSource.Create(settings.Connection);
        ActiveSchemaProvider.Set(settings.Schema);
        var store = new PostgresAccountStore(dataSource);
        try
        {
            var account = store.GetAccountByUsername(username);
            if (account is not null)
            {
                return new AdminAccountResult(account.AccountId, Created: false);
            }

            if (string.IsNullOrEmpty(password))
            {
                throw new HostingException($"Account '{username}' does not exist yet and no password was given.");
            }

            account = store.RegisterAccount(username, PasswordHasher.Hash(password));
            return new AdminAccountResult(account.AccountId, Created: true);
        }
        catch (Exception ex) when (ex is NpgsqlException or TimeoutException)
        {
            throw DatabaseErrors.Explain(ex, new NpgsqlConnectionStringBuilder(settings.Connection));
        }
    }

    public static string? FindAccountName(DatabaseSettings settings, string username) =>
        WithDataSource(settings, dataSource => new PostgresAccountStore(dataSource).GetAccountByUsername(username)?.Username);

    public static void IssueLaunchTicket(DatabaseSettings settings, string username, IReadOnlyCollection<System.Net.IPAddress> clientIps) =>
        WithDataSource(settings, dataSource =>
        {
            var account = new PostgresAccountStore(dataSource).GetAccountByUsername(username)
                ?? throw new HostingException($"There is no account named '{username}'.", "Create it first with tposeidon setup, or pick an account that exists.", null);
            new PostgresLaunchTicketStore(dataSource).Issue(account.AccountId, clientIps, LaunchTicket.Lifetime);
            return true;
        });

    public static IReadOnlyList<string> ListAccountNames(DatabaseSettings settings) =>
        WithDataSource(settings, dataSource => new PostgresAccountStore(dataSource).ListUsernames());

    public static GmCharacterRegistration? RegisterGmCharacter(DatabaseSettings settings, string characterName, string permittedIp) =>
        WithDataSource(settings, dataSource => new PostgresGmAuthorityStore(dataSource).RegisterGmCharacter(characterName, permittedIp));

    public static GmCharacterEntry? UnregisterGmCharacter(DatabaseSettings settings, string characterName) =>
        WithDataSource(settings, dataSource => new PostgresGmAuthorityStore(dataSource).UnregisterGmCharacter(characterName));

    public static IReadOnlyList<GmCharacterEntry> ListGmCharacters(DatabaseSettings settings) =>
        WithDataSource(settings, dataSource => new PostgresGmAuthorityStore(dataSource).ListGmCharacters());

    public static IReadOnlyList<CharacterSummary> FindCharacters(DatabaseSettings settings, string text, int limit) =>
        WithDataSource(settings, dataSource => new PostgresCharacterSearch(dataSource).Find(text, limit));

    public static T WithPenalties<T>(DatabaseSettings settings, Func<PostgresPenaltyStore, T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return WithDataSource(settings, dataSource => action(new PostgresPenaltyStore(dataSource)));
    }

    private static T WithDataSource<T>(DatabaseSettings settings, Func<NpgsqlDataSource, T> action)
    {
        ArgumentNullException.ThrowIfNull(settings);
        try
        {
            using var dataSource = NpgsqlDataSource.Create(settings.Connection);
            ActiveSchemaProvider.Set(settings.Schema);
            return action(dataSource);
        }
        catch (Exception ex) when (ex is NpgsqlException or TimeoutException)
        {
            throw DatabaseErrors.Explain(ex, new NpgsqlConnectionStringBuilder(settings.Connection));
        }
    }

    private static async Task<bool> ExistsAsync(NpgsqlConnection connection, string sql, string name, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("name", name);
        return await command.ExecuteScalarAsync(ct).ConfigureAwait(false) is not null;
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static string Identifier(string name) => "\"" + name.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    private static string Literal(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
}
