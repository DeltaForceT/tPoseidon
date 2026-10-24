using System.Data.Common;
using System.Globalization;
using Microsoft.Data.Sqlite;
using tPoseidon.Core.Data;
using tPoseidon.DrillEgg.Connectors;

namespace tPoseidon.DrillEgg.Storage;

internal static class DrawStatus
{
    public const string Pending = "pending";
    public const string Owed = "owed";
    public const string Delivered = "delivered";
    public const string Interrupted = "interrupted";
}

internal sealed record DrawRecord(
    long DrawId,
    GachaPlayer Player,
    short Area,
    bool Deep,
    GachaPrize? Prize,
    string? ItemName,
    string Status,
    int Attempts,
    string? LastError,
    DateTimeOffset DrawnAt)
{
    public Delivery ToDelivery() => new(DrawId, Player, Prize ?? throw new InvalidOperationException($"Draw {DrawId} has no prize."), ItemName ?? string.Empty, Area, Deep);
}

internal sealed class LocalStore
{
    public const string DatabaseName = "local";

    private const string DrawColumns = "draw_id, account, account_id, character, area, deep, item_id, item_name, item_count, item_class, use_hours, item_flag, status, attempts, last_error, drawn_at";

    private const string Schema = """
        PRAGMA journal_mode = WAL;
        CREATE TABLE IF NOT EXISTS players (
            account     TEXT     NOT NULL PRIMARY KEY,
            coupons     INTEGER  NOT NULL CHECK (coupons >= 0),
            created_at  TEXT     NOT NULL
        );
        CREATE TABLE IF NOT EXISTS draws (
            draw_id       INTEGER  PRIMARY KEY AUTOINCREMENT,
            account       TEXT     NOT NULL,
            account_id    INTEGER  NULL,
            character     TEXT     NULL,
            area          INTEGER  NOT NULL,
            deep          INTEGER  NOT NULL,
            item_id       INTEGER  NULL,
            item_name     TEXT     NULL,
            item_count    INTEGER  NULL,
            item_class    INTEGER  NULL,
            use_hours     INTEGER  NULL,
            item_flag     INTEGER  NULL,
            status        TEXT     NOT NULL,
            attempts      INTEGER  NOT NULL DEFAULT 0,
            last_error    TEXT     NULL,
            drawn_at      TEXT     NOT NULL,
            delivered_at  TEXT     NULL
        );
        CREATE INDEX IF NOT EXISTS draws_status_idx ON draws (status, draw_id);
        CREATE INDEX IF NOT EXISTS draws_account_idx ON draws (account, draw_id);
        """;

    private readonly TimeProvider _clock;

    private LocalStore(SqlDatabase database, TimeProvider clock)
    {
        Database = database;
        _clock = clock;
    }

    public SqlDatabase Database { get; }

    public static async Task<LocalStore> OpenAsync(string file, TimeProvider clock, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(file);
        ArgumentNullException.ThrowIfNull(clock);
        var folder = Path.GetDirectoryName(Path.GetFullPath(file));
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
        var connection = new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(file),
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = true,
            DefaultTimeout = 30,
        }.ToString();
        var store = new LocalStore(new SqlDatabase(DatabaseName, SqliteFactory.Instance, connection), clock);
        await using var open = await store.Database.OpenAsync(ct).ConfigureAwait(false);
        await using var command = open.CreateCommand();
        command.CommandText = Schema;
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        return store;
    }

    public async Task<int> CouponsAsync(string account, int starting, CancellationToken ct)
    {
        await using var connection = await Database.OpenAsync(ct).ConfigureAwait(false);
        await EnsurePlayerAsync(connection, null, account, starting, ct).ConfigureAwait(false);
        return await CouponsAsync(connection, null, account, ct).ConfigureAwait(false);
    }

    public async Task<int> GrantAsync(string account, int starting, int count, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfZero(count);
        await using var connection = await Database.OpenAsync(ct).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        await EnsurePlayerAsync(connection, transaction, account, starting, ct).ConfigureAwait(false);
        await using (var command = Command(connection, transaction, "UPDATE players SET coupons = MAX(coupons + @n, 0) WHERE account = @a"))
        {
            Add(command, "@n", count);
            Add(command, "@a", account);
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        var left = await CouponsAsync(connection, transaction, account, ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return left;
    }

    public async Task<ICouponSpend?> SpendAsync(string account, int starting, int cost, CancellationToken ct)
    {
        var connection = await Database.OpenAsync(ct).ConfigureAwait(false);
        DbTransaction? transaction = null;
        try
        {
            transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
            await EnsurePlayerAsync(connection, transaction, account, starting, ct).ConfigureAwait(false);
            int changed;
            await using (var command = Command(connection, transaction, "UPDATE players SET coupons = coupons - @c WHERE account = @a AND coupons >= @c"))
            {
                Add(command, "@c", cost);
                Add(command, "@a", account);
                changed = await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
            if (changed != 1)
            {
                await transaction.RollbackAsync(ct).ConfigureAwait(false);
                return null;
            }
            var left = await CouponsAsync(connection, transaction, account, ct).ConfigureAwait(false);
            var spend = new TransactionSpend(new SqlUnit(Database, connection, transaction), left);
            transaction = null;
            connection = null;
            return spend;
        }
        finally
        {
            if (transaction is not null) await transaction.DisposeAsync().ConfigureAwait(false);
            if (connection is not null) await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async Task<long> BeginDrawAsync(GachaPlayer player, short area, bool deep, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(player);
        await using var connection = await Database.OpenAsync(ct).ConfigureAwait(false);
        await using var command = Command(connection, null, """
            INSERT INTO draws (account, account_id, character, area, deep, status, drawn_at)
            VALUES (@a, @id, @ch, @area, @deep, @s, @t) RETURNING draw_id
            """);
        Add(command, "@a", player.Account);
        Add(command, "@id", player.AccountId);
        Add(command, "@ch", player.Character);
        Add(command, "@area", (int)area);
        Add(command, "@deep", deep);
        Add(command, "@s", DrawStatus.Pending);
        Add(command, "@t", Now());
        return (long)(await command.ExecuteScalarAsync(ct).ConfigureAwait(false))!;
    }

    public async Task DiscardDrawAsync(long drawId, CancellationToken ct)
    {
        await using var connection = await Database.OpenAsync(ct).ConfigureAwait(false);
        await using var command = Command(connection, null, "DELETE FROM draws WHERE draw_id = @d AND status <> @s");
        Add(command, "@d", drawId);
        Add(command, "@s", DrawStatus.Delivered);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task RecordPrizeAsync(long drawId, GachaPrize prize, string itemName, string status, SqlUnit? unit, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(prize);
        if (unit is not null && ReferenceEquals(unit.Database, Database))
        {
            await RecordPrizeAsync(unit.Connection, unit.Transaction, drawId, prize, itemName, status, ct).ConfigureAwait(false);
            return;
        }
        await using var connection = await Database.OpenAsync(ct).ConfigureAwait(false);
        await RecordPrizeAsync(connection, null, drawId, prize, itemName, status, ct).ConfigureAwait(false);
    }

    public async Task<long> RecordDeliveredAsync(GachaPlayer player, short area, bool deep, GachaPrize prize, string itemName, CancellationToken ct)
    {
        var drawId = await BeginDrawAsync(player, area, deep, ct).ConfigureAwait(false);
        await RecordPrizeAsync(drawId, prize, itemName, DrawStatus.Delivered, null, ct).ConfigureAwait(false);
        return drawId;
    }

    public async Task MarkDeliveredAsync(long drawId, CancellationToken ct)
    {
        await using var connection = await Database.OpenAsync(ct).ConfigureAwait(false);
        await using var command = Command(connection, null, "UPDATE draws SET status = @s, delivered_at = @t, attempts = attempts + 1, last_error = NULL WHERE draw_id = @d");
        Add(command, "@s", DrawStatus.Delivered);
        Add(command, "@t", Now());
        Add(command, "@d", drawId);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task MarkFailedAsync(long drawId, string error, CancellationToken ct)
    {
        await using var connection = await Database.OpenAsync(ct).ConfigureAwait(false);
        await using var command = Command(connection, null, "UPDATE draws SET attempts = attempts + 1, last_error = @e WHERE draw_id = @d");
        Add(command, "@e", error);
        Add(command, "@d", drawId);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<int> InterruptPendingAsync(CancellationToken ct)
    {
        await using var connection = await Database.OpenAsync(ct).ConfigureAwait(false);
        await using var command = Command(connection, null, "UPDATE draws SET status = @i WHERE status = @p");
        Add(command, "@i", DrawStatus.Interrupted);
        Add(command, "@p", DrawStatus.Pending);
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<DrawRecord>> OwedAsync(int limit, CancellationToken ct) =>
        QueryAsync($"SELECT {DrawColumns} FROM draws WHERE status = @s ORDER BY draw_id LIMIT @n", [("@s", DrawStatus.Owed), ("@n", limit)], ct);

    public Task<IReadOnlyList<DrawRecord>> HistoryAsync(string? account, int limit, CancellationToken ct) =>
        account is null
            ? QueryAsync($"SELECT {DrawColumns} FROM draws ORDER BY draw_id DESC LIMIT @n", [("@n", limit)], ct)
            : QueryAsync($"SELECT {DrawColumns} FROM draws WHERE account = @a ORDER BY draw_id DESC LIMIT @n", [("@a", account), ("@n", limit)], ct);

    public async Task<DrawRecord?> FindAsync(long drawId, CancellationToken ct)
    {
        var found = await QueryAsync($"SELECT {DrawColumns} FROM draws WHERE draw_id = @d", [("@d", drawId)], ct).ConfigureAwait(false);
        return found.Count == 0 ? null : found[0];
    }

    private async Task<IReadOnlyList<DrawRecord>> QueryAsync(string sql, IReadOnlyList<(string Name, object Value)> values, CancellationToken ct)
    {
        await using var connection = await Database.OpenAsync(ct).ConfigureAwait(false);
        await using var command = Command(connection, null, sql);
        foreach (var (name, value) in values) Add(command, name, value);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var records = new List<DrawRecord>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var prize = reader.IsDBNull(6)
                ? null
                : new GachaPrize(reader.GetInt32(6), (byte)reader.GetInt32(9), (short)reader.GetInt32(8), (short)reader.GetInt32(10), (byte)reader.GetInt32(11), 1);
            records.Add(new DrawRecord(
                reader.GetInt64(0),
                new GachaPlayer(reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetInt64(2), reader.IsDBNull(3) ? null : reader.GetString(3)),
                (short)reader.GetInt32(4),
                reader.GetInt32(5) != 0,
                prize,
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.GetString(12),
                reader.GetInt32(13),
                reader.IsDBNull(14) ? null : reader.GetString(14),
                DateTimeOffset.Parse(reader.GetString(15), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)));
        }
        return records;
    }

    private async Task RecordPrizeAsync(DbConnection connection, DbTransaction? transaction, long drawId, GachaPrize prize, string itemName, string status, CancellationToken ct)
    {
        await using var command = Command(connection, transaction, """
            UPDATE draws SET item_id = @i, item_name = @n, item_count = @c, item_class = @k, use_hours = @h, item_flag = @f, status = @s,
                delivered_at = CASE WHEN @s = @done THEN @t ELSE NULL END
            WHERE draw_id = @d
            """);
        Add(command, "@i", prize.ItemIndex);
        Add(command, "@n", itemName);
        Add(command, "@c", (int)prize.Count);
        Add(command, "@k", (int)prize.ItemClass);
        Add(command, "@h", (int)prize.UseTimeHours);
        Add(command, "@f", (int)prize.ItemFlag);
        Add(command, "@s", status);
        Add(command, "@done", DrawStatus.Delivered);
        Add(command, "@t", Now());
        Add(command, "@d", drawId);
        if (await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) != 1) throw new InvalidOperationException($"Draw {drawId} is missing from the draw log.");
    }

    private async Task EnsurePlayerAsync(DbConnection connection, DbTransaction? transaction, string account, int starting, CancellationToken ct)
    {
        await using var command = Command(connection, transaction, "INSERT INTO players (account, coupons, created_at) VALUES (@a, @c, @t) ON CONFLICT (account) DO NOTHING");
        Add(command, "@a", account);
        Add(command, "@c", starting);
        Add(command, "@t", Now());
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static async Task<int> CouponsAsync(DbConnection connection, DbTransaction? transaction, string account, CancellationToken ct)
    {
        await using var command = Command(connection, transaction, "SELECT coupons FROM players WHERE account = @a");
        Add(command, "@a", account);
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture);
    }

    private static DbCommand Command(DbConnection connection, DbTransaction? transaction, string sql)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return command;
    }

    private static void Add(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private string Now() => _clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture);
}
