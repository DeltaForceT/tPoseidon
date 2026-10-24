using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresGiftStore : IGiftStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresGiftStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public GiftAcceptResult Accept(uint giftCode, string receiveMessage, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(receiveMessage);
        var message = receiveMessage.Length > GiftItemRules.MessageLength ? receiveMessage[..GiftItemRules.MessageLength] : receiveMessage;
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        long toAccount;
        short category;
        short? count;
        ChargedItemGrant grant;
        bool taken;
        using (var cmd = new NpgsqlCommand("""
            SELECT to_account_id, goods_category, item_count, take_date IS NOT NULL,
                   item_class, item_index, item_attr, item_use_time, item_flag, sell_code
            FROM gift_item WHERE gift_code = @g FOR UPDATE
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue("g", (int)giftCode);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return new GiftAcceptResult(GiftAcceptErrors.NotFound);
            toAccount = reader.GetInt64(0);
            category = reader.GetInt16(1);
            count = reader.IsDBNull(2) ? null : reader.GetInt16(2);
            taken = reader.GetBoolean(3);
            grant = new ChargedItemGrant(
                checked((uint)toAccount), (byte)reader.GetInt16(4), reader.GetInt32(5), reader.GetInt16(6), reader.GetInt16(7),
                (byte)reader.GetInt16(8), reader.GetInt32(9), ChargedItemRules.OtherSellType);
        }
        if (taken) return new GiftAcceptResult(GiftAcceptErrors.AlreadyTaken);
        if (category != GiftItemRules.ChargedBoxCategory || count is not > 0) return new GiftAcceptResult(GiftAcceptErrors.GrantFailed);
        for (var copy = 0; copy < count; copy++) PostgresChargedItemStore.Insert(conn, tx, grant);
        using (var stamp = new NpgsqlCommand(
            "UPDATE gift_item SET take_date = @t, receive_message = @m WHERE gift_code = @g AND take_date IS NULL", conn, tx))
        {
            stamp.Parameters.AddWithValue("t", at.UtcDateTime);
            stamp.Parameters.AddWithValue("m", message);
            stamp.Parameters.AddWithValue("g", (int)giftCode);
            if (stamp.ExecuteNonQuery() != 1) return new GiftAcceptResult(GiftAcceptErrors.AlreadyTaken);
        }
        tx.Commit();
        return new GiftAcceptResult(GiftAcceptErrors.None);
    }

    private const string NameSql = "CASE WHEN c.char_authority = 2 THEN COALESCE(d.char_name, '') ELSE c.name END";

    public IReadOnlyList<GiftReceivedRow> Received(uint accountId, int limit)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "SELECT g.goods_code, g.take_date, " + NameSql + ", g.gift_message, c.species " +
            "FROM gift_item g JOIN characters c ON c.character_id = g.from_char_uid " +
            "LEFT JOIN deleted_char d ON d.char_uid = c.character_id " +
            "WHERE g.to_account_id = @u AND g.take_date IS NOT NULL ORDER BY g.take_date DESC LIMIT @n", conn);
        cmd.Parameters.AddWithValue("u", (long)accountId);
        cmd.Parameters.AddWithValue("n", limit);
        using var reader = cmd.ExecuteReader();
        var rows = new List<GiftReceivedRow>();
        while (reader.Read())
        {
            rows.Add(new GiftReceivedRow((uint)reader.GetInt32(0), reader.GetFieldValue<DateTimeOffset>(1), reader.GetString(2), reader.GetString(3), reader.GetInt16(4)));
        }
        return rows;
    }

    public IReadOnlyList<GiftPendingRow> Pending(uint accountId, int limit)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "SELECT g.gift_code, g.goods_code, " + NameSql + ", g.gift_date, g.gift_message, c.species " +
            "FROM gift_item g JOIN characters c ON c.character_id = g.from_char_uid " +
            "LEFT JOIN deleted_char d ON d.char_uid = c.character_id " +
            "WHERE g.to_account_id = @u AND g.take_date IS NULL ORDER BY g.gift_date DESC LIMIT @n", conn);
        cmd.Parameters.AddWithValue("u", (long)accountId);
        cmd.Parameters.AddWithValue("n", limit);
        using var reader = cmd.ExecuteReader();
        var rows = new List<GiftPendingRow>();
        while (reader.Read())
        {
            rows.Add(new GiftPendingRow((uint)reader.GetInt32(0), (uint)reader.GetInt32(1), reader.GetString(2), reader.GetFieldValue<DateTimeOffset>(3), reader.GetString(4), reader.GetInt16(5)));
        }
        return rows;
    }

    public IReadOnlyList<GiftSentRow> Sent(uint accountId, int limit)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "SELECT g.goods_code, g.gift_date, " + NameSql + ", g.receive_message, g.take_date IS NOT NULL, c.species " +
            "FROM gift_item g JOIN characters c ON c.character_id = g.to_char_uid " +
            "LEFT JOIN deleted_char d ON d.char_uid = c.character_id " +
            "WHERE g.from_account_id = @u ORDER BY g.take_date DESC NULLS LAST LIMIT @n", conn);
        cmd.Parameters.AddWithValue("u", (long)accountId);
        cmd.Parameters.AddWithValue("n", limit);
        using var reader = cmd.ExecuteReader();
        var rows = new List<GiftSentRow>();
        while (reader.Read())
        {
            rows.Add(new GiftSentRow((uint)reader.GetInt32(0), reader.GetFieldValue<DateTimeOffset>(1), reader.GetString(2), reader.GetString(3), reader.GetBoolean(4), reader.GetInt16(5)));
        }
        return rows;
    }

    public int RemainCount(uint accountId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM gift_item WHERE to_account_id = @u AND take_date IS NULL", conn);
        cmd.Parameters.AddWithValue("u", (long)accountId);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }
}
