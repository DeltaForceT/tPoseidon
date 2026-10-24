using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Npgsql;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Data.Postgres;

public sealed record MigrationStep(int Index, int Count, string Name);

public sealed record MigrationOutcome(IReadOnlyList<string> Applied, IReadOnlyList<string> Adopted, IReadOnlyList<string> ChangedAfterApply);

internal sealed record MigrationFile(string Name, string Sql)
{
    public string Checksum { get; } = Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(Sql.Replace("\r\n", "\n"))));
}

public static class PostgresMigrations
{
    public const string MigrationsDirectoryVariable = "MT_MIGRATIONS_DIR";
    public const string PackagedMigrationsFolder = "migrations";
    public const string TrackingTable = "schema_migrations";

    private const string LegacyMarkerTable = "characters";
    private const string LockKeyPrefix = "tposeidon.migrations:";
    private const string CreateTrackingTableSql =
        "CREATE TABLE IF NOT EXISTS schema_migrations (name text PRIMARY KEY, applied_at timestamptz NOT NULL DEFAULT now(), checksum text NOT NULL)";

    private static readonly Regex CreateTablePattern = new(
        @"\bcreate\s+table\s+(?:if\s+not\s+exists\s+)?([a-z_][a-z_0-9]*)\s*\(",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static Task<MigrationOutcome> ApplyAsync(NpgsqlDataSource dataSource, string migrationsDirectory, string? schema = null, CancellationToken ct = default) =>
        ApplyAsync(dataSource, migrationsDirectory, schema, null, ct);

    public static async Task<MigrationOutcome> ApplyAsync(NpgsqlDataSource dataSource, string migrationsDirectory, string? schema, IProgress<MigrationStep>? progress, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(migrationsDirectory);

        if (!Directory.Exists(migrationsDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Migrations directory not found: {migrationsDirectory}");
        }

        if (!string.IsNullOrWhiteSpace(schema) && !IsSafeSchemaName(schema))
        {
            throw new InvalidOperationException(
                $"Refusing to create schema with unsafe name '{schema}' (only letters, digits, underscore allowed).");
        }

        var files = Directory.GetFiles(migrationsDirectory, "*.sql")
            .Where(p => MigrationFiles.IsNumbered(Path.GetFileName(p)))
            .OrderBy(p => p, StringComparer.Ordinal)
            .Select(p => new MigrationFile(Path.GetFileName(p), File.ReadAllText(p)))
            .ToArray();

        var lockKey = LockKeyPrefix + (schema ?? string.Empty);
        await using var connection = await dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await ExecuteAsync(connection, "SELECT pg_advisory_lock(hashtext(@key))", ct, null, ("key", lockKey)).ConfigureAwait(false);
        try
        {
            if (!string.IsNullOrWhiteSpace(schema))
            {
                await ExecuteAsync(connection, $"CREATE SCHEMA IF NOT EXISTS \"{schema}\"", ct).ConfigureAwait(false);
                await ExecuteAsync(connection, $"SET search_path TO \"{schema}\", public", ct).ConfigureAwait(false);
            }

            var adopted = new List<string>();
            if (!await TableExistsAsync(connection, TrackingTable, ct).ConfigureAwait(false))
            {
                var adoptedCount = await TableExistsAsync(connection, LegacyMarkerTable, ct).ConfigureAwait(false)
                    ? await FindAdoptedCountAsync(connection, files, ct).ConfigureAwait(false)
                    : 0;
                await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
                await ExecuteAsync(connection, CreateTrackingTableSql, ct, transaction: transaction).ConfigureAwait(false);
                for (var i = 0; i < adoptedCount; i++)
                {
                    await RecordAsync(connection, transaction, files[i], ct).ConfigureAwait(false);
                    adopted.Add(files[i].Name);
                }
                await transaction.CommitAsync(ct).ConfigureAwait(false);
            }

            var recorded = await ReadRecordedAsync(connection, ct).ConfigureAwait(false);
            var changed = new List<string>();
            var pending = new List<MigrationFile>();
            foreach (var file in files)
            {
                if (!recorded.TryGetValue(file.Name, out var checksum))
                {
                    pending.Add(file);
                }
                else if (!string.Equals(checksum, file.Checksum, StringComparison.Ordinal))
                {
                    changed.Add(file.Name);
                    Console.Error.WriteLine($"[postgres] WARNING: migration {file.Name} was changed after it was applied and will not be run again. Put further changes in a new numbered migration.");
                }
            }

            var applied = new List<string>();
            for (var index = 0; index < pending.Count; index++)
            {
                var file = pending[index];
                progress?.Report(new MigrationStep(index + 1, pending.Count, file.Name));
                await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
                await ExecuteAsync(connection, file.Sql, ct, transaction: transaction).ConfigureAwait(false);
                await RecordAsync(connection, transaction, file, ct).ConfigureAwait(false);
                await transaction.CommitAsync(ct).ConfigureAwait(false);
                applied.Add(file.Name);
            }

            return new MigrationOutcome(applied, adopted, changed);
        }
        finally
        {
            await ExecuteAsync(connection, "SELECT pg_advisory_unlock(hashtext(@key))", CancellationToken.None, null, ("key", lockKey)).ConfigureAwait(false);
        }
    }

    private static async Task<int> FindAdoptedCountAsync(NpgsqlConnection connection, MigrationFile[] files, CancellationToken ct)
    {
        for (var i = files.Length - 1; i >= 0; i--)
        {
            var tables = CreatedTables(files[i].Sql);
            if (tables.Count == 0) continue;
            var allPresent = true;
            foreach (var table in tables)
            {
                if (!await TableExistsAsync(connection, table, ct).ConfigureAwait(false))
                {
                    allPresent = false;
                    break;
                }
            }
            if (allPresent) return i + 1;
        }
        return 0;
    }

    private static List<string> CreatedTables(string sql) =>
        CreateTablePattern.Matches(sql).Select(m => m.Groups[1].Value.ToLowerInvariant()).Distinct().ToList();

    private static async Task<bool> TableExistsAsync(NpgsqlConnection connection, string table, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(
            "SELECT to_regclass(quote_ident(current_schema()) || '.' || quote_ident(@t)) IS NOT NULL", connection);
        command.Parameters.AddWithValue("t", table);
        return (bool)(await command.ExecuteScalarAsync(ct).ConfigureAwait(false))!;
    }

    private static async Task<Dictionary<string, string>> ReadRecordedAsync(NpgsqlConnection connection, CancellationToken ct)
    {
        var recorded = new Dictionary<string, string>(StringComparer.Ordinal);
        await using var command = new NpgsqlCommand($"SELECT name, checksum FROM {TrackingTable}", connection);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            recorded[reader.GetString(0)] = reader.GetString(1);
        }
        return recorded;
    }

    private static Task RecordAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, MigrationFile file, CancellationToken ct) =>
        ExecuteAsync(connection, $"INSERT INTO {TrackingTable} (name, checksum) VALUES (@name, @checksum)", ct, transaction,
            ("name", file.Name), ("checksum", file.Checksum));

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, CancellationToken ct, NpgsqlTransaction? transaction = null, params (string Name, string Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static bool IsSafeSchemaName(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        foreach (var c in s)
        {
            if (!char.IsLetterOrDigit(c) && c != '_') return false;
        }
        return true;
    }

    public static string DefaultMigrationsDirectory()
    {
        var configured = Environment.GetEnvironmentVariable(MigrationsDirectoryVariable);
        if (!string.IsNullOrWhiteSpace(configured)) return configured.Trim();
        var packaged = Path.Combine(AppContext.BaseDirectory, PackagedMigrationsFolder);
        if (Directory.Exists(packaged)) return packaged;
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 10 && dir is not null; i++, dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "tPoseidon.sln")))
            {
                return Path.Combine(dir.FullName, "scripts", "migrations");
            }
        }
        return Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "scripts", "migrations");
    }
}
