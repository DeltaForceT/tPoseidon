using Npgsql;
using tPoseidon.Core.Data;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresChargedItemStore : IChargedItemStore
{
    private const string Columns = "item_uid, item_class, item_index, item_attr, item_use_time, item_flag";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresChargedItemStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public IReadOnlyList<ChargedItem> ListWaiting(uint accountId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            $"SELECT {Columns} FROM charged_item WHERE account_id = @a AND item_use_date IS NULL ORDER BY item_uid", conn);
        cmd.Parameters.AddWithValue("a", (long)accountId);
        using var reader = cmd.ExecuteReader();
        var items = new List<ChargedItem>();
        while (reader.Read()) items.Add(Read(reader));
        return items;
    }

    public ulong Grant(ChargedItemGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        return Insert(conn, null, grant);
    }

    internal static ulong Insert(NpgsqlConnection conn, NpgsqlTransaction? tx, ChargedItemGrant grant)
    {
        using var cmd = new NpgsqlCommand(
            "INSERT INTO charged_item (item_class, account_id, item_index, item_attr, item_use_time, item_flag, sell_code, sell_type) " +
            "VALUES (@c, @a, @i, @attr, @t, @f, @sc, @st) RETURNING item_uid", conn, tx);
        cmd.Parameters.AddWithValue("c", (short)grant.ItemClass);
        cmd.Parameters.AddWithValue("a", (long)grant.AccountId);
        cmd.Parameters.AddWithValue("i", grant.ItemIndex);
        cmd.Parameters.AddWithValue("attr", grant.ItemAttr);
        cmd.Parameters.AddWithValue("t", grant.UseTimeHours);
        cmd.Parameters.AddWithValue("f", (short)grant.ItemFlag);
        cmd.Parameters.AddWithValue("sc", grant.SellCode);
        cmd.Parameters.AddWithValue("st", (short)grant.SellType);
        return (ulong)(long)cmd.ExecuteScalar()!;
    }

    public ChargedItemMoveResult MoveToInventory(ChargedItemMove move)
    {
        ArgumentNullException.ThrowIfNull(move);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        ChargedItem? waiting;
        using (var cmd = new NpgsqlCommand(
            $"SELECT {Columns} FROM charged_item WHERE item_uid = @u AND account_id = @a AND item_use_date IS NULL FOR UPDATE", conn, tx))
        {
            cmd.Parameters.AddWithValue("u", (long)move.ItemUid);
            cmd.Parameters.AddWithValue("a", (long)move.AccountId);
            using var reader = cmd.ExecuteReader();
            waiting = reader.Read() ? Read(reader) : null;
        }
        if (waiting is null) return new ChargedItemMoveResult(null, ChargedItemRules.NotWaiting);
        using (var stamp = new NpgsqlCommand(
            "UPDATE charged_item SET item_use_date = NOW(), take_char = @ch WHERE item_uid = @u AND item_use_date IS NULL", conn, tx))
        {
            stamp.Parameters.AddWithValue("ch", (long)move.CharacterId);
            stamp.Parameters.AddWithValue("u", (long)move.ItemUid);
            if (stamp.ExecuteNonQuery() != 1) return new ChargedItemMoveResult(null, ChargedItemRules.StampFailed);
        }
        var common = waiting.ItemClass == ChargedItemRules.CommonClass;
        var count = (ushort)Math.Clamp((int)waiting.ItemAttr, 1, ushort.MaxValue);
        ChargedItemMoved moved;
        if (common && move.JoinStack && JoinStack(conn, tx, move.CharacterId, waiting.ItemIndex, count) is { } stacked)
        {
            moved = new ChargedItemMoved(stacked.Uid, true, ChargedItemRules.CommonClass, waiting.ItemIndex, count, stacked.Total, Item.NeverBreaks);
        }
        else
        {
            var breaked = !common && waiting.ItemFlag == ChargedItemRules.TimedFlag
                ? DateTimeOffset.UtcNow.AddHours(waiting.UseTimeHours)
                : Item.NeverBreaks;
            var itemClass = common ? ChargedItemRules.CommonClass : ChargedItemRules.RareClass;
            using (var insert = new NpgsqlCommand("""
                INSERT INTO items (item_uid, item_index, item_class, item_attr, item_charged, item_breaked, refine_type, refine_level, refine_state, item_slot, item_stack)
                VALUES (@u, @i, @c, 0, 1, @b, 0, 0, 0, 0, @n)
                """, conn, tx))
            {
                insert.Parameters.AddWithValue("u", (long)move.ItemUid);
                insert.Parameters.AddWithValue("i", waiting.ItemIndex);
                insert.Parameters.AddWithValue("c", (short)itemClass);
                insert.Parameters.AddWithValue("b", breaked);
                insert.Parameters.AddWithValue("n", (int)count);
                if (insert.ExecuteNonQuery() != 1) return new ChargedItemMoveResult(null, ChargedItemRules.MoveFailed);
            }
            using (var own = new NpgsqlCommand("""
                INSERT INTO item_char (char_uid, item_uid, item_where, item_pos)
                VALUES (@ch, @u, @w, (SELECT COALESCE(MIN(p.pos), 0) FROM generate_series(0, 32767) AS p(pos)
                    WHERE NOT EXISTS (SELECT 1 FROM item_char o WHERE o.char_uid = @ch AND o.item_where = @w AND o.item_pos = p.pos)))
                """, conn, tx))
            {
                own.Parameters.AddWithValue("ch", (long)move.CharacterId);
                own.Parameters.AddWithValue("u", (long)move.ItemUid);
                own.Parameters.AddWithValue("w", (short)SlotKind.Inventory);
                if (own.ExecuteNonQuery() != 1) return new ChargedItemMoveResult(null, ChargedItemRules.MoveFailed);
            }
            moved = new ChargedItemMoved(move.ItemUid, false, itemClass, waiting.ItemIndex, count, count, breaked);
        }
        tx.Commit();
        return new ChargedItemMoveResult(moved, 0);
    }

    private static (ulong Uid, ushort Total)? JoinStack(NpgsqlConnection conn, NpgsqlTransaction tx, ulong characterId, int itemIndex, ushort count)
    {
        using var cmd = new NpgsqlCommand("""
            UPDATE items SET item_stack = item_stack + @n
            WHERE item_uid = (
                SELECT i.item_uid FROM items i JOIN item_char c ON c.item_uid = i.item_uid
                WHERE c.char_uid = @ch AND c.item_where = @w AND i.item_index = @i AND i.item_class = 1
                ORDER BY c.item_pos LIMIT 1)
            RETURNING item_uid, item_stack
            """, conn, tx);
        cmd.Parameters.AddWithValue("n", (int)count);
        cmd.Parameters.AddWithValue("ch", (long)characterId);
        cmd.Parameters.AddWithValue("w", (short)SlotKind.Inventory);
        cmd.Parameters.AddWithValue("i", itemIndex);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return ((ulong)reader.GetInt64(0), (ushort)Math.Clamp(reader.GetInt32(1), 0, ushort.MaxValue));
    }

    private static ChargedItem Read(NpgsqlDataReader reader) => new(
        (ulong)reader.GetInt64(0),
        (byte)reader.GetInt16(1),
        reader.GetInt32(2),
        reader.GetInt16(3),
        reader.GetInt16(4),
        (byte)reader.GetInt16(5));
}
