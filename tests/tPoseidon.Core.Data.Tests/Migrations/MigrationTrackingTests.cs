using Npgsql;
using tPoseidon.Core.Data.Postgres;

namespace tPoseidon.Core.Data.Tests.Migrations;

public sealed class MigrationTrackingTests : IAsyncLifetime
{
    private const string ConnectionVariable = "TPOSEIDON_TEST_PG";
    private const string SkipReason = "set TPOSEIDON_TEST_PG to a throwaway PostgreSQL connection string to run the migration tests";

    private readonly string? _connection = Environment.GetEnvironmentVariable(ConnectionVariable);
    private readonly string _schema = "migration_track_" + Guid.NewGuid().ToString("N")[..12];
    private readonly string _workDirectory = Path.Combine(Path.GetTempPath(), "tposeidon-migrations-" + Guid.NewGuid().ToString("N"));
    private NpgsqlDataSource? _dataSource;

    public Task InitializeAsync()
    {
        if (_connection is null) return Task.CompletedTask;
        _dataSource = NpgsqlDataSource.Create(_connection);
        Directory.CreateDirectory(_workDirectory);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_dataSource is not null)
        {
            await using var command = _dataSource.CreateCommand($"DROP SCHEMA IF EXISTS \"{_schema}\" CASCADE");
            await command.ExecuteNonQueryAsync();
            await _dataSource.DisposeAsync();
        }
        if (Directory.Exists(_workDirectory)) Directory.Delete(_workDirectory, recursive: true);
    }

    private static string[] RealMigrationFiles() =>
        Directory.GetFiles(PostgresMigrations.DefaultMigrationsDirectory(), "*.sql")
            .Where(p => tPoseidon.Core.Common.MigrationFiles.IsNumbered(Path.GetFileName(p)))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();

    private string CopyMigrations(string name, int count = int.MaxValue)
    {
        var directory = Path.Combine(_workDirectory, name);
        Directory.CreateDirectory(directory);
        foreach (var file in RealMigrationFiles().Take(count))
        {
            File.Copy(file, Path.Combine(directory, Path.GetFileName(file)));
        }
        return directory;
    }

    private static string[] NamesIn(string directory) =>
        Directory.GetFiles(directory).Select(p => Path.GetFileName(p)).OrderBy(n => n, StringComparer.Ordinal).ToArray();

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = await _dataSource!.OpenConnectionAsync();
        await using (var path = new NpgsqlCommand($"SET search_path TO \"{_schema}\", public", connection))
        {
            await path.ExecuteNonQueryAsync();
        }
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = await _dataSource!.OpenConnectionAsync();
        await using (var path = new NpgsqlCommand($"SET search_path TO \"{_schema}\", public", connection))
        {
            await path.ExecuteNonQueryAsync();
        }
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    private async Task ApplyRawLikeTheOldRunnerAsync(string directory, string[] names)
    {
        await ExecuteAsync("SELECT 1");
        foreach (var name in names)
        {
            await ExecuteAsync(await File.ReadAllTextAsync(Path.Combine(directory, name)));
        }
    }

    private async Task InsertCharacterWithZeroMpAsync()
    {
        await ExecuteAsync("INSERT INTO accounts (account_id, username, password_placeholder) VALUES (1, 'sample', 'x')");
        await ExecuteAsync(
            "INSERT INTO characters (character_id, account_id, slot, name, species, ftype, money, level, hp, mp, exp, tm_exp) VALUES " +
            "(1001, 1, 0, 'Alpha', 1, 1, 0, 30, 41, 0, 0, 0)");
    }

    private Task<string> CharacterSnapshotAsync() =>
        ScalarAsync<string>("SELECT COALESCE(string_agg(row_to_json(t)::text, E'\\n' ORDER BY character_id), '') FROM characters t");

    [SkippableFact]
    public async Task FreshDatabaseAppliesEveryMigrationOnce()
    {
        Skip.If(_connection is null, SkipReason);
        var directory = CopyMigrations("all");

        var outcome = await PostgresMigrations.ApplyAsync(_dataSource!, directory, _schema);

        var expected = NamesIn(directory);
        Assert.Equal(expected, outcome.Applied);
        Assert.Empty(outcome.Adopted);
        Assert.Empty(outcome.ChangedAfterApply);
        Assert.Equal(expected.Length, await ScalarAsync<long>("SELECT count(*) FROM schema_migrations"));
    }

    [SkippableFact]
    public async Task SecondStartAppliesNothingAndLeavesRowsUnchanged()
    {
        Skip.If(_connection is null, SkipReason);
        var directory = CopyMigrations("all");
        await PostgresMigrations.ApplyAsync(_dataSource!, directory, _schema);
        await InsertCharacterWithZeroMpAsync();
        var before = await CharacterSnapshotAsync();

        var outcome = await PostgresMigrations.ApplyAsync(_dataSource!, directory, _schema);

        Assert.Empty(outcome.Applied);
        Assert.Empty(outcome.Adopted);
        Assert.Equal(before, await CharacterSnapshotAsync());
        Assert.Equal(0L, await ScalarAsync<long>("SELECT mp FROM characters WHERE character_id = 1001"));
    }

    [SkippableFact]
    public async Task NewMigrationFileIsAppliedExactlyOnce()
    {
        Skip.If(_connection is null, SkipReason);
        var directory = CopyMigrations("all");
        await PostgresMigrations.ApplyAsync(_dataSource!, directory, _schema);
        var next = RealMigrationFiles().Length + 1;
        var name = $"{next:000}_probe.sql";
        await File.WriteAllTextAsync(Path.Combine(directory, name),
            "CREATE TABLE IF NOT EXISTS migration_probe (n integer NOT NULL);\nINSERT INTO migration_probe (n) VALUES (1);\n");

        var first = await PostgresMigrations.ApplyAsync(_dataSource!, directory, _schema);
        var second = await PostgresMigrations.ApplyAsync(_dataSource!, directory, _schema);

        Assert.Equal(new[] { name }, first.Applied);
        Assert.Empty(second.Applied);
        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM migration_probe"));
    }

    [SkippableFact]
    public async Task FailedMigrationRollsBackAndIsNotRecorded()
    {
        Skip.If(_connection is null, SkipReason);
        var directory = CopyMigrations("all");
        await PostgresMigrations.ApplyAsync(_dataSource!, directory, _schema);
        var name = $"{RealMigrationFiles().Length + 1:000}_broken.sql";
        await File.WriteAllTextAsync(Path.Combine(directory, name),
            "CREATE TABLE migration_half (n integer);\nSELECT 1 / 0;\n");

        await Assert.ThrowsAsync<PostgresException>(() => PostgresMigrations.ApplyAsync(_dataSource!, directory, _schema));

        Assert.Equal(0L, await ScalarAsync<long>($"SELECT count(*) FROM schema_migrations WHERE name = '{name}'"));
        Assert.False(await ScalarAsync<bool>("SELECT to_regclass('migration_half') IS NOT NULL"));
    }

    [SkippableFact]
    public async Task OldStyleDatabaseIsAdoptedWithoutRerunningMigrations()
    {
        Skip.If(_connection is null, SkipReason);
        var directory = CopyMigrations("all");
        var names = NamesIn(directory);
        await ExecuteAsync($"CREATE SCHEMA IF NOT EXISTS \"{_schema}\"");
        await ApplyRawLikeTheOldRunnerAsync(directory, names);
        await InsertCharacterWithZeroMpAsync();
        var before = await CharacterSnapshotAsync();
        Assert.False(await ScalarAsync<bool>("SELECT to_regclass('schema_migrations') IS NOT NULL"));

        var outcome = await PostgresMigrations.ApplyAsync(_dataSource!, directory, _schema);

        Assert.Equal(names, outcome.Adopted);
        Assert.Empty(outcome.Applied);
        Assert.Equal(before, await CharacterSnapshotAsync());
        Assert.Equal(0L, await ScalarAsync<long>("SELECT mp FROM characters WHERE character_id = 1001"));
    }

    [SkippableFact]
    public async Task OldStyleDatabaseBehindTheNewestMigrationGetsOnlyTheMissingOnes()
    {
        Skip.If(_connection is null, SkipReason);
        const int oldCount = 60;
        var directory = CopyMigrations("all");
        var names = NamesIn(directory);
        await ExecuteAsync($"CREATE SCHEMA IF NOT EXISTS \"{_schema}\"");
        await ApplyRawLikeTheOldRunnerAsync(directory, names.Take(oldCount).ToArray());
        await InsertCharacterWithZeroMpAsync();

        var outcome = await PostgresMigrations.ApplyAsync(_dataSource!, directory, _schema);

        Assert.NotEmpty(outcome.Adopted);
        Assert.True(outcome.Adopted.Count <= oldCount);
        Assert.Equal(names, outcome.Adopted.Concat(outcome.Applied).ToArray());
        Assert.Contains(names[19], outcome.Adopted);
        Assert.Equal(0L, await ScalarAsync<long>("SELECT mp FROM characters WHERE character_id = 1001"));
        Assert.Equal((long)names.Length, await ScalarAsync<long>("SELECT count(*) FROM schema_migrations"));
    }

    [SkippableFact]
    public async Task ChangedFileIsReportedAndNotRunAgain()
    {
        Skip.If(_connection is null, SkipReason);
        var directory = CopyMigrations("all");
        await PostgresMigrations.ApplyAsync(_dataSource!, directory, _schema);
        var target = Directory.GetFiles(directory).OrderBy(p => p, StringComparer.Ordinal).First(p => Path.GetFileName(p).StartsWith("005_"));
        await File.AppendAllTextAsync(target, "\nUPDATE characters SET map_id = 1;\n");
        await InsertCharacterWithZeroMpAsync();
        var before = await CharacterSnapshotAsync();

        var outcome = await PostgresMigrations.ApplyAsync(_dataSource!, directory, _schema);

        Assert.Equal(new[] { Path.GetFileName(target) }, outcome.ChangedAfterApply);
        Assert.Empty(outcome.Applied);
        Assert.Equal(before, await CharacterSnapshotAsync());
    }

    [SkippableFact]
    public async Task LineEndingDifferencesAreNotTreatedAsChanges()
    {
        Skip.If(_connection is null, SkipReason);
        var directory = CopyMigrations("all");
        await PostgresMigrations.ApplyAsync(_dataSource!, directory, _schema);
        foreach (var file in Directory.GetFiles(directory))
        {
            var text = (await File.ReadAllTextAsync(file)).Replace("\r\n", "\n").Replace("\n", "\r\n");
            await File.WriteAllTextAsync(file, text);
        }

        var outcome = await PostgresMigrations.ApplyAsync(_dataSource!, directory, _schema);

        Assert.Empty(outcome.ChangedAfterApply);
        Assert.Empty(outcome.Applied);
    }
}
