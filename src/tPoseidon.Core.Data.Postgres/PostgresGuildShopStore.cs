using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresGuildShopStore : IGuildShopStore
{
    private const int PurchaseLockClass = 0x47534850;

    private readonly NpgsqlDataSource _dataSource;

    public PostgresGuildShopStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public GuildShopState? StateOf(ulong guildCode)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("SELECT guild_coin, guild_max_members, depot_tabs FROM guild WHERE guild_code = @g", conn);
        cmd.Parameters.AddWithValue("g", unchecked((long)guildCode));
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadState(reader) : null;
    }

    public GuildShopReceipt Purchase(GuildShopOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        using (var gate = new NpgsqlCommand("SELECT pg_try_advisory_xact_lock(@class, @guild)", conn, tx))
        {
            gate.Parameters.AddWithValue("class", PurchaseLockClass);
            gate.Parameters.AddWithValue("guild", unchecked((int)order.GuildCode));
            if (gate.ExecuteScalar() is not true)
            {
                return new GuildShopReceipt(GuildShopErrors.PurchasePending, default);
            }
        }

        GuildShopState current;
        using (var lockRow = new NpgsqlCommand(
            "SELECT guild_coin, guild_max_members, depot_tabs FROM guild WHERE guild_code = @g FOR UPDATE", conn, tx))
        {
            lockRow.Parameters.AddWithValue("g", unchecked((long)order.GuildCode));
            using var reader = lockRow.ExecuteReader();
            if (!reader.Read()) return new GuildShopReceipt(GuildShopErrors.NotAuthority, default);
            current = ReadState(reader);
        }

        var decision = GuildShopRules.Decide(order.Type, order.Price, current);
        if (decision.Error != GuildShopErrors.None) return new GuildShopReceipt(decision.Error, current);

        using (var update = new NpgsqlCommand(
            "UPDATE guild SET guild_coin = @coin, guild_max_members = @max, depot_tabs = @tabs WHERE guild_code = @g", conn, tx))
        {
            update.Parameters.AddWithValue("coin", decision.After.Coins);
            update.Parameters.AddWithValue("max", (short)decision.After.MaxMembers);
            update.Parameters.AddWithValue("tabs", (short)decision.After.DepotTabs);
            update.Parameters.AddWithValue("g", unchecked((long)order.GuildCode));
            update.ExecuteNonQuery();
        }

        using (var history = new NpgsqlCommand(
            "INSERT INTO guild_shop_history (guild_code, char_name, goods_index, coin_cost) VALUES (@g, @n, @i, @c)", conn, tx))
        {
            history.Parameters.AddWithValue("g", unchecked((long)order.GuildCode));
            history.Parameters.AddWithValue("n", order.CharacterName);
            history.Parameters.AddWithValue("i", order.GoodsIndex);
            history.Parameters.AddWithValue("c", order.Price);
            history.ExecuteNonQuery();
        }

        tx.Commit();
        return new GuildShopReceipt(GuildShopErrors.None, decision.After);
    }

    public IReadOnlyList<GuildShopHistoryEntry> History(ulong guildCode)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            SELECT created_at, char_name, goods_index, coin_cost
            FROM guild_shop_history
            WHERE guild_code = @g
            ORDER BY history_seq DESC
            LIMIT @n
            """,
            conn);
        cmd.Parameters.AddWithValue("g", unchecked((long)guildCode));
        cmd.Parameters.AddWithValue("n", GuildShopRules.HistoryShown);
        using var reader = cmd.ExecuteReader();
        var result = new List<GuildShopHistoryEntry>();
        while (reader.Read())
        {
            result.Add(new GuildShopHistoryEntry(reader.GetFieldValue<DateTimeOffset>(0), reader.GetString(1), reader.GetInt32(2), reader.GetInt32(3)));
        }
        return result;
    }

    public int? AddCoins(ulong guildCode, int amount)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            UPDATE guild SET guild_coin = guild_coin + @amount
            WHERE guild_code = @g AND guild_coin::bigint + @amount BETWEEN 0 AND 2147483647
            RETURNING guild_coin
            """,
            conn);
        cmd.Parameters.AddWithValue("amount", (long)amount);
        cmd.Parameters.AddWithValue("g", unchecked((long)guildCode));
        return cmd.ExecuteScalar() is int coins ? coins : null;
    }

    private static GuildShopState ReadState(NpgsqlDataReader reader) =>
        new(reader.GetInt32(0), reader.GetInt16(1), reader.GetInt16(2));
}
