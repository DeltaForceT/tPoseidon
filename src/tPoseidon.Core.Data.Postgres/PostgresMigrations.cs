using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed record MigrationStep(int Index, int Count, string Name);

public static class PostgresMigrations
{
    public const string MigrationsDirectoryVariable = "MT_MIGRATIONS_DIR";
    public const string PackagedMigrationsFolder = "migrations";

        public static Task ApplyAsync(NpgsqlDataSource dataSource, string migrationsDirectory, string? schema = null, CancellationToken ct = default) =>
        ApplyAsync(dataSource, migrationsDirectory, schema, null, ct);

    public static async Task ApplyAsync(NpgsqlDataSource dataSource, string migrationsDirectory, string? schema, IProgress<MigrationStep>? progress, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(migrationsDirectory);

        if (!Directory.Exists(migrationsDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Migrations directory not found: {migrationsDirectory}");
        }

        

        
        var files = Directory.GetFiles(migrationsDirectory, "*.sql")
            .Where(p =>
            {
                var name = Path.GetFileName(p);
                return name.Length >= 4
                    && char.IsDigit(name[0])
                    && char.IsDigit(name[1])
                    && char.IsDigit(name[2])
                    && name[3] == '_';
            })
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();

        

        

        if (!string.IsNullOrWhiteSpace(schema))
        {
            if (!IsSafeSchemaName(schema))
            {
                throw new InvalidOperationException(
                    $"Refusing to create schema with unsafe name '{schema}' (only letters, digits, underscore allowed).");
            }
            await using var schemaConn = await dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
            await using var schemaCmd = new NpgsqlCommand(
                $"CREATE SCHEMA IF NOT EXISTS \"{schema}\"", schemaConn);
            await schemaCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        for (var index = 0; index < files.Length; index++)
        {
            var file = files[index];
            progress?.Report(new MigrationStep(index + 1, files.Length, Path.GetFileName(file)));
            var sql = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
            await using var connection = await dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);

            

            

            

            

            if (!string.IsNullOrWhiteSpace(schema))
            {
                await using var setSp = new NpgsqlCommand($"SET search_path TO \"{schema}\", public", connection);
                await setSp.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
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
