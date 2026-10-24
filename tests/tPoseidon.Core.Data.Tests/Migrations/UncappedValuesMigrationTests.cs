using Npgsql;
using tPoseidon.Core.Data.Postgres;

namespace tPoseidon.Core.Data.Tests.Migrations;

public sealed class UncappedValuesMigrationTests : IAsyncLifetime
{
    private const string ConnectionVariable = "TPOSEIDON_TEST_PG";
    private const int FirstUncappedMigration = 75;

    private readonly string? _connection = Environment.GetEnvironmentVariable(ConnectionVariable);
    private readonly string _schema = "migration_test_" + Guid.NewGuid().ToString("N")[..12];
    private readonly string _beforeDirectory = Path.Combine(Path.GetTempPath(), "tposeidon-migrations-" + Guid.NewGuid().ToString("N"));
    private readonly string _widenDirectory = Path.Combine(Path.GetTempPath(), "tposeidon-migrations-" + Guid.NewGuid().ToString("N"));
    private NpgsqlDataSource? _dataSource;

    public async Task InitializeAsync()
    {
        if (_connection is null) return;
        _dataSource = NpgsqlDataSource.Create(_connection);
        Directory.CreateDirectory(_beforeDirectory);
        Directory.CreateDirectory(_widenDirectory);
        foreach (var file in Directory.GetFiles(PostgresMigrations.DefaultMigrationsDirectory(), "*.sql"))
        {
            var name = Path.GetFileName(file);
            if (name.Length < 4 || !int.TryParse(name[..3], out var number)) continue;
            if (number < FirstUncappedMigration) File.Copy(file, Path.Combine(_beforeDirectory, name));
            else if (number == FirstUncappedMigration) File.Copy(file, Path.Combine(_widenDirectory, name));
        }
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_dataSource is not null)
        {
            await using var command = _dataSource.CreateCommand($"DROP SCHEMA IF EXISTS \"{_schema}\" CASCADE");
            await command.ExecuteNonQueryAsync();
            await _dataSource.DisposeAsync();
        }
        foreach (var directory in new[] { _beforeDirectory, _widenDirectory })
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private async Task<string> DumpAsync(string columns)
    {
        await using var connection = await _dataSource!.OpenConnectionAsync();
        await using (var path = new NpgsqlCommand($"SET search_path TO \"{_schema}\", public", connection))
        {
            await path.ExecuteNonQueryAsync();
        }
        await using var command = new NpgsqlCommand($"SELECT COALESCE(string_agg(row_to_json(t)::text, E'\n' ORDER BY character_id), '') FROM (SELECT character_id, {columns} FROM characters) t", connection);
        return (string)(await command.ExecuteScalarAsync())!;
    }

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

    private async Task<string> ColumnTypeAsync(string column)
    {
        await using var command = _dataSource!.CreateCommand(
            "SELECT data_type FROM information_schema.columns WHERE table_schema = @s AND table_name = 'characters' AND column_name = @c");
        command.Parameters.AddWithValue("s", _schema);
        command.Parameters.AddWithValue("c", column);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    [SkippableFact]
    public async Task WidensTheColumnsAndLeavesEveryStoredValueUnchanged()
    {
        Skip.If(_connection is null, "set TPOSEIDON_TEST_PG to a throwaway PostgreSQL connection string to run the migration tests");
        await PostgresMigrations.ApplyAsync(_dataSource!, _beforeDirectory, _schema);
        await ExecuteAsync("INSERT INTO accounts (account_id, username, password_placeholder) VALUES (1, 'sample', 'x')");
        await ExecuteAsync(
            "INSERT INTO characters (character_id, account_id, slot, name, species, ftype, money, level, hp, mp, exp, tm_exp) VALUES " +
            "(1001, 1, 0, 'Alpha', 1, 1, 0, 1, 41, 5, 0, 0), " +
            "(1002, 1, 1, 'Beta', 2, 2, 2100000000, 120, 2147483647, 1234, 987654321012, 2147483647), " +
            "(1003, 1, 2, 'Gamma', 3, 3, 4294967295, 395, 1, 0, 5, 77)");
        var before = await DumpAsync("money, level, hp, mp, exp, tm_exp");
        Assert.Contains("\"hp\":2147483647", before);
        Assert.Equal("integer", await ColumnTypeAsync("hp"));

        await PostgresMigrations.ApplyAsync(_dataSource!, _widenDirectory, _schema);

        Assert.Equal(before, await DumpAsync("money, level, hp, mp, exp, tm_exp"));
        Assert.Equal("bigint", await ColumnTypeAsync("hp"));
        Assert.Equal("bigint", await ColumnTypeAsync("mp"));
        Assert.Equal("bigint", await ColumnTypeAsync("tm_exp"));
        Assert.Equal("bigint", await ColumnTypeAsync("money"));

        await PostgresMigrations.ApplyAsync(_dataSource!, _widenDirectory, _schema);
        Assert.Equal(before, await DumpAsync("money, level, hp, mp, exp, tm_exp"));
    }

    [SkippableFact]
    public async Task StoresValuesBeyondTheOldColumnLimits()
    {
        Skip.If(_connection is null, "set TPOSEIDON_TEST_PG to a throwaway PostgreSQL connection string to run the migration tests");
        await PostgresMigrations.ApplyAsync(_dataSource!, PostgresMigrations.DefaultMigrationsDirectory(), _schema);
        await ExecuteAsync("INSERT INTO accounts (account_id, username, password_placeholder) VALUES (1, 'sample', 'x')");

        await ExecuteAsync(
            "INSERT INTO characters (character_id, account_id, slot, name, species, ftype, money, hp, mp, tm_exp) VALUES " +
            "(2001, 1, 0, 'Big', 1, 1, 9000000000000, 9000000000, 8000000000, 7000000000)");

        var dump = await DumpAsync("money, hp, mp, tm_exp");
        Assert.Contains("\"money\":9000000000000", dump);
        Assert.Contains("\"hp\":9000000000", dump);
        Assert.Contains("\"mp\":8000000000", dump);
        Assert.Contains("\"tm_exp\":7000000000", dump);
        await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync("UPDATE characters SET money = -1 WHERE character_id = 2001"));
    }
}
