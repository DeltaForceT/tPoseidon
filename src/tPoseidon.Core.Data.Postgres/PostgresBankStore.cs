using tPoseidon.Core.Data;
using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresBankStore : IBankStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresBankStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public long GetMoney(uint accountId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "SELECT account_bank_money FROM accounts WHERE account_id = @account_id",
            conn);
        cmd.Parameters.AddWithValue("account_id", (long)accountId);
        var result = cmd.ExecuteScalar();
        return result is long l ? l : 0L;
    }

    public long AdjustMoney(uint accountId, long delta)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            UPDATE accounts
            SET account_bank_money = GREATEST(0, account_bank_money + @delta)
            WHERE account_id = @account_id
            RETURNING account_bank_money
            """,
            conn);
        cmd.Parameters.AddWithValue("account_id", (long)accountId);
        cmd.Parameters.AddWithValue("delta", delta);
        var result = cmd.ExecuteScalar();
        return result is long l ? l : 0L;
    }

    public IReadOnlyList<Item> GetItems(uint accountId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            BaseSelectSql + " WHERE u.account_id = @account_id ORDER BY u.item_pos ASC",
            conn);
        cmd.Parameters.AddWithValue("account_id", (long)accountId);
        using var reader = cmd.ExecuteReader();
        var result = new List<Item>();
        while (reader.Read()) result.Add(ReadBankItem(reader));
        return result;
    }

    public Item Deposit(uint accountId, ulong itemUid, byte slotPosition)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();

        using (var detachCmd = new NpgsqlCommand(
            "DELETE FROM item_char WHERE item_uid = @item_uid",
            conn, tx))
        {
            detachCmd.Parameters.AddWithValue("item_uid", unchecked((long)itemUid));
            detachCmd.ExecuteNonQuery();
        }

        using (var insertCmd = new NpgsqlCommand(
            """
            INSERT INTO item_user (account_id, item_uid, item_pos)
            VALUES (@account_id, @item_uid, @item_pos)
            ON CONFLICT (account_id, item_uid) DO UPDATE
                SET item_pos = EXCLUDED.item_pos
            """,
            conn, tx))
        {
            insertCmd.Parameters.AddWithValue("account_id", (long)accountId);
            insertCmd.Parameters.AddWithValue("item_uid", unchecked((long)itemUid));
            insertCmd.Parameters.AddWithValue("item_pos", (short)slotPosition);
            insertCmd.ExecuteNonQuery();
        }

        tx.Commit();

        return FindItem(accountId, itemUid)
            ?? throw new InvalidOperationException($"Item {itemUid} not found after deposit.");
    }

    public bool Withdraw(uint accountId, ulong itemUid)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "DELETE FROM item_user WHERE account_id = @account_id AND item_uid = @item_uid",
            conn);
        cmd.Parameters.AddWithValue("account_id", (long)accountId);
        cmd.Parameters.AddWithValue("item_uid", unchecked((long)itemUid));
        return cmd.ExecuteNonQuery() == 1;
    }

    public Item? FindItem(uint accountId, ulong itemUid)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            BaseSelectSql + " WHERE u.account_id = @account_id AND u.item_uid = @item_uid",
            conn);
        cmd.Parameters.AddWithValue("account_id", (long)accountId);
        cmd.Parameters.AddWithValue("item_uid", unchecked((long)itemUid));
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadBankItem(reader) : null;
    }

    private const string BaseSelectSql = """
        SELECT i.item_uid, i.item_index, i.item_class, i.item_attr,
               i.item_charged, i.item_breaked, i.refine_type, i.refine_level,
               i.refine_state, i.item_slot, i.item_stack,
               u.item_pos
        FROM items i
        JOIN item_user u ON u.item_uid = i.item_uid
        """;

    private static Item ReadBankItem(NpgsqlDataReader reader) => new()
    {
        ItemUid = unchecked((ulong)reader.GetInt64(0)),
        ItemIndex = reader.GetInt32(1),
        ItemClass = (byte)reader.GetInt16(2),
        ItemAttr = reader.GetInt16(3),
        ItemCharged = (byte)reader.GetInt16(4),
        BreakedAt = new DateTimeOffset(reader.GetDateTime(5), TimeSpan.Zero),
        RefineType = reader.GetInt16(6),
        RefineLevel = (byte)reader.GetInt16(7),
        RefineState = (byte)reader.GetInt16(8),
        ItemSlot = (byte)reader.GetInt16(9),
        Stack = (ushort)reader.GetInt32(10),
        CharacterId = 0,
        SlotKind = SlotKind.Warehouse,
        SlotPosition = (byte)reader.GetInt16(11),
    };
}
