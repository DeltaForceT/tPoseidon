using System.Data.Common;
using System.Globalization;

namespace tPoseidon.DrillEgg.Connectors;

internal static class SqlColumns
{
    public const string Account = "account";
    public const string Id = "id";
    public const string Character = "character";

    public static int? Find(DbDataReader reader, string column)
    {
        ArgumentNullException.ThrowIfNull(reader);
        for (var i = 0; i < reader.FieldCount; i++)
        {
            if (string.Equals(reader.GetName(i), column, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return null;
    }

    public static long Number(object value, string what) => value switch
    {
        long l => l,
        int i => i,
        short s => s,
        byte b => b,
        decimal d when d == decimal.Truncate(d) => (long)d,
        string text when long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
        IConvertible convertible => convertible.ToInt64(CultureInfo.InvariantCulture),
        _ => throw new ConnectorException($"{what} returned {value.GetType().Name}, not a whole number."),
    };
}

internal sealed class SqlPlayers : IPlayerConnector
{
    private readonly SqlDatabase _database;
    private readonly string _query;

    public SqlPlayers(SqlDatabase database, string query, SignInKind kind)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _query = query ?? throw new ArgumentNullException(nameof(query));
        Kind = kind;
    }

    public SignInKind Kind { get; }

    public async Task<GachaPlayer?> SignInAsync(SignInRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [DrawValues.Account] = request.Account,
            [DrawValues.Password] = request.Password,
            [DrawValues.Token] = request.Token,
        };
        try
        {
            await using var connection = await _database.OpenAsync(ct).ConfigureAwait(false);
            await using var command = SqlDatabase.Command(connection, null, _query, values);
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;
            var account = SqlColumns.Find(reader, SqlColumns.Account) is { } a && !reader.IsDBNull(a)
                ? Convert.ToString(reader.GetValue(a), CultureInfo.InvariantCulture)
                : request.Account;
            if (string.IsNullOrWhiteSpace(account)) throw new ConnectorException("The players query returned a row without an account column, and no account name was typed.");
            long? id = SqlColumns.Find(reader, SqlColumns.Id) is { } i && !reader.IsDBNull(i) ? SqlColumns.Number(reader.GetValue(i), "The players query id column") : null;
            var character = SqlColumns.Find(reader, SqlColumns.Character) is { } c && !reader.IsDBNull(c) ? Convert.ToString(reader.GetValue(c), CultureInfo.InvariantCulture) : null;
            return new GachaPlayer(account, id, character);
        }
        catch (DbException e)
        {
            throw new ConnectorException($"The players query on database {_database.Name} failed: {e.Message}", e);
        }
    }
}

internal sealed class SqlCoupons : ICouponConnector
{
    private readonly SqlDatabase _database;
    private readonly string _count;
    private readonly string _spend;

    public SqlCoupons(SqlDatabase database, string count, string spend)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _count = count ?? throw new ArgumentNullException(nameof(count));
        _spend = spend ?? throw new ArgumentNullException(nameof(spend));
    }

    public async Task<int> CountAsync(GachaPlayer player, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(player);
        try
        {
            await using var connection = await _database.OpenAsync(ct).ConfigureAwait(false);
            return await CountAsync(connection, null, DrawValues.Of(player), ct).ConfigureAwait(false);
        }
        catch (DbException e)
        {
            throw new ConnectorException($"The coupon count query on database {_database.Name} failed: {e.Message}", e);
        }
    }

    public async Task<ICouponSpend?> SpendAsync(GachaPlayer player, int cost, long drawId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(player);
        var values = DrawValues.Of(player);
        values[DrawValues.Cost] = cost;
        values[DrawValues.DrawId] = drawId;
        DbConnection? connection = null;
        DbTransaction? transaction = null;
        try
        {
            connection = await _database.OpenAsync(ct).ConfigureAwait(false);
            transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
            bool spent;
            await using (var command = SqlDatabase.Command(connection, transaction, _spend, values))
            {
                spent = await SpentAsync(command, ct).ConfigureAwait(false);
            }
            if (!spent)
            {
                await transaction.RollbackAsync(ct).ConfigureAwait(false);
                return null;
            }
            var left = await CountAsync(connection, transaction, values, ct).ConfigureAwait(false);
            var spend = new TransactionSpend(new SqlUnit(_database, connection, transaction), left);
            connection = null;
            transaction = null;
            return spend;
        }
        catch (DbException e)
        {
            throw new ConnectorException($"The coupon spend query on database {_database.Name} failed: {e.Message}", e);
        }
        finally
        {
            if (transaction is not null) await transaction.DisposeAsync().ConfigureAwait(false);
            if (connection is not null) await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task<int> CountAsync(DbConnection connection, DbTransaction? transaction, IReadOnlyDictionary<string, object?> values, CancellationToken ct)
    {
        await using var command = SqlDatabase.Command(connection, transaction, _count, values);
        var value = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
        if (value is null or DBNull) return 0;
        var count = SqlColumns.Number(value, "The coupon count query");
        return count switch
        {
            < 0 => 0,
            > int.MaxValue => int.MaxValue,
            _ => (int)count,
        };
    }

    private static async Task<bool> SpentAsync(DbCommand command, CancellationToken ct)
    {
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (reader.FieldCount > 0 && await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return !reader.IsDBNull(0) && SqlColumns.Number(reader.GetValue(0), "The coupon spend query") > 0;
        }
        while (await reader.NextResultAsync(ct).ConfigureAwait(false))
        {
        }
        return reader.RecordsAffected > 0;
    }
}

internal sealed class SqlDelivery : IDeliveryConnector
{
    private readonly string _deliver;

    public SqlDelivery(SqlDatabase database, string deliver)
    {
        Database = database ?? throw new ArgumentNullException(nameof(database));
        _deliver = deliver ?? throw new ArgumentNullException(nameof(deliver));
    }

    public SqlDatabase? Database { get; }

    public async Task DeliverAsync(Delivery delivery, SqlUnit? joined, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        var values = DrawValues.Of(delivery);
        try
        {
            if (joined is not null)
            {
                await using var inside = SqlDatabase.Command(joined.Connection, joined.Transaction, _deliver, values);
                await inside.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                return;
            }
            await using var connection = await Database!.OpenAsync(ct).ConfigureAwait(false);
            await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
            await using (var command = SqlDatabase.Command(connection, transaction, _deliver, values))
            {
                await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
            await transaction.CommitAsync(ct).ConfigureAwait(false);
        }
        catch (DbException e)
        {
            throw new ConnectorException($"The delivery query on database {Database!.Name} failed for draw {delivery.DrawId.ToString(CultureInfo.InvariantCulture)}: {e.Message}", e);
        }
    }
}

internal sealed class TransactionSpend : ICouponSpend
{
    private readonly SqlUnit _unit;
    private bool _committed;

    public TransactionSpend(SqlUnit unit, int left)
    {
        _unit = unit ?? throw new ArgumentNullException(nameof(unit));
        CouponsLeft = left;
    }

    public int CouponsLeft { get; }

    public SqlUnit? Unit => _unit;

    public async Task CommitAsync(CancellationToken ct)
    {
        await _unit.Transaction.CommitAsync(ct).ConfigureAwait(false);
        _committed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_committed) await _unit.Transaction.RollbackAsync().ConfigureAwait(false);
        await _unit.Transaction.DisposeAsync().ConfigureAwait(false);
        await _unit.Connection.DisposeAsync().ConfigureAwait(false);
    }
}
