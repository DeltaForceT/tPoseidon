using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresTradeStore : ITradeStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresTradeStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public TradeResult Trade(TradeOffer first, TradeOffer second, int bagCapacity)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        if (first.Slots.Select(slot => slot.ItemUid).Distinct().Count() != first.Slots.Count
            || second.Slots.Select(slot => slot.ItemUid).Distinct().Count() != second.Slots.Count)
        {
            return new TradeResult(TradeRules.DuplicateItem, null, null);
        }
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        var firstMoney = LockMoney(conn, tx, first.CharacterId);
        var secondMoney = LockMoney(conn, tx, second.CharacterId);
        if (firstMoney is not { } firstOwn || secondMoney is not { } secondOwn) return new TradeResult(TradeRules.ItemMismatch, null, null);
        if (first.Money > firstOwn || second.Money > secondOwn
            || !TradeRules.MoneySettles(firstOwn, first.Money, second.Money) || !TradeRules.MoneySettles(secondOwn, second.Money, first.Money))
        {
            return new TradeResult(TradeRules.MoneyOutOfRange, null, null);
        }
        var firstChanges = new List<TradeItemChange>();
        var secondChanges = new List<TradeItemChange>();
        foreach (var slot in first.Slots)
        {
            if (!Give(conn, tx, first.CharacterId, second.CharacterId, slot, firstChanges, secondChanges)) return new TradeResult(TradeRules.ItemMismatch, null, null);
        }
        foreach (var slot in second.Slots)
        {
            if (!Give(conn, tx, second.CharacterId, first.CharacterId, slot, secondChanges, firstChanges)) return new TradeResult(TradeRules.ItemMismatch, null, null);
        }
        if (BagCount(conn, tx, first.CharacterId) > bagCapacity || BagCount(conn, tx, second.CharacterId) > bagCapacity)
        {
            return new TradeResult(TradeRules.BagFull, null, null);
        }
        var firstSettled = (uint)(firstOwn - first.Money + second.Money);
        var secondSettled = (uint)(secondOwn - second.Money + first.Money);
        SetMoney(conn, tx, first.CharacterId, firstSettled);
        SetMoney(conn, tx, second.CharacterId, secondSettled);
        tx.Commit();
        return new TradeResult(
            TradeRules.Ok,
            new TradeSideResult(first.CharacterId, firstSettled, firstChanges),
            new TradeSideResult(second.CharacterId, secondSettled, secondChanges));
    }

    private static bool Give(
        NpgsqlConnection conn, NpgsqlTransaction tx, ulong giver, ulong receiver, TradeSlot slot,
        List<TradeItemChange> giverChanges, List<TradeItemChange> receiverChanges)
    {
        if (slot.Count == 0) return false;
        int itemIndex;
        byte itemClass;
        int stack;
        byte mixSlot;
        using (var held = new NpgsqlCommand("""
            SELECT i.item_index, i.item_class, i.item_stack, i.item_slot
            FROM items i JOIN item_char c ON c.item_uid = i.item_uid
            WHERE i.item_uid = @u AND c.char_uid = @ch AND c.item_where = @w
            FOR UPDATE
            """, conn, tx))
        {
            held.Parameters.AddWithValue("u", unchecked((long)slot.ItemUid));
            held.Parameters.AddWithValue("ch", unchecked((long)giver));
            held.Parameters.AddWithValue("w", (short)SlotKind.Inventory);
            using var reader = held.ExecuteReader();
            if (!reader.Read()) return false;
            itemIndex = reader.GetInt32(0);
            itemClass = (byte)reader.GetInt16(1);
            stack = reader.GetInt32(2);
            mixSlot = (byte)reader.GetInt16(3);
        }
        var common = itemClass == TradeRules.CommonClass;
        if (itemIndex != slot.ItemIndex || (common ? slot.Count > stack : slot.Count != 1)) return false;
        if (common && slot.Count < stack)
        {
            Execute(conn, tx, "UPDATE items SET item_stack = item_stack - @n WHERE item_uid = @u", ("n", (int)slot.Count), ("u", unchecked((long)slot.ItemUid)));
            giverChanges.Add(new TradeItemChange(TradeChangeKind.StackChanged, itemClass, itemIndex, slot.ItemUid, (ushort)(stack - slot.Count), mixSlot));
            receiverChanges.Add(common && JoinStack(conn, tx, receiver, itemIndex, slot.Count) is { } joined
                ? new TradeItemChange(TradeChangeKind.StackChanged, itemClass, itemIndex, joined.Uid, joined.Total, 0)
                : new TradeItemChange(TradeChangeKind.Created, itemClass, itemIndex, NewStack(conn, tx, receiver, itemIndex, slot.Count), slot.Count, 0));
            return true;
        }
        if (common && JoinStack(conn, tx, receiver, itemIndex, slot.Count) is { } stacked)
        {
            Execute(conn, tx, "DELETE FROM item_char WHERE item_uid = @u", ("u", unchecked((long)slot.ItemUid)));
            Execute(conn, tx, "DELETE FROM items WHERE item_uid = @u", ("u", unchecked((long)slot.ItemUid)));
            giverChanges.Add(new TradeItemChange(TradeChangeKind.Removed, itemClass, itemIndex, slot.ItemUid, slot.Count, mixSlot));
            receiverChanges.Add(new TradeItemChange(TradeChangeKind.StackChanged, itemClass, itemIndex, stacked.Uid, stacked.Total, 0));
            return true;
        }
        using (var move = new NpgsqlCommand("""
            UPDATE item_char SET char_uid = @to, item_pos = (SELECT COALESCE(MIN(p.pos), 0) FROM generate_series(0, 32767) AS p(pos)
                WHERE NOT EXISTS (SELECT 1 FROM item_char o WHERE o.char_uid = @to AND o.item_where = @w AND o.item_pos = p.pos))
            WHERE item_uid = @u AND char_uid = @from AND item_where = @w
            """, conn, tx))
        {
            move.Parameters.AddWithValue("to", unchecked((long)receiver));
            move.Parameters.AddWithValue("from", unchecked((long)giver));
            move.Parameters.AddWithValue("w", (short)SlotKind.Inventory);
            move.Parameters.AddWithValue("u", unchecked((long)slot.ItemUid));
            if (move.ExecuteNonQuery() != 1) return false;
        }
        giverChanges.Add(new TradeItemChange(TradeChangeKind.Removed, itemClass, itemIndex, slot.ItemUid, slot.Count, mixSlot));
        receiverChanges.Add(new TradeItemChange(TradeChangeKind.Created, itemClass, itemIndex, slot.ItemUid, slot.Count, mixSlot));
        return true;
    }

    private static (ulong Uid, ushort Total)? JoinStack(NpgsqlConnection conn, NpgsqlTransaction tx, ulong characterId, int itemIndex, ushort count)
    {
        using var cmd = new NpgsqlCommand("""
            UPDATE items SET item_stack = item_stack + @n
            WHERE item_uid = (
                SELECT i.item_uid FROM items i JOIN item_char c ON c.item_uid = i.item_uid
                WHERE c.char_uid = @ch AND c.item_where = @w AND i.item_index = @i AND i.item_class = @c
                ORDER BY c.item_pos LIMIT 1)
            RETURNING item_uid, item_stack
            """, conn, tx);
        cmd.Parameters.AddWithValue("n", (int)count);
        cmd.Parameters.AddWithValue("ch", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("w", (short)SlotKind.Inventory);
        cmd.Parameters.AddWithValue("i", itemIndex);
        cmd.Parameters.AddWithValue("c", (short)TradeRules.CommonClass);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return ((ulong)reader.GetInt64(0), (ushort)Math.Clamp(reader.GetInt32(1), 0, ushort.MaxValue));
    }

    private static ulong NewStack(NpgsqlConnection conn, NpgsqlTransaction tx, ulong characterId, int itemIndex, ushort count)
    {
        long uid;
        using (var insert = new NpgsqlCommand("""
            INSERT INTO items (item_index, item_class, item_attr, item_charged, item_breaked, refine_type, refine_level, refine_state, item_slot, item_stack)
            VALUES (@i, @c, 0, 0, @b, 0, 0, 0, 0, @n)
            RETURNING item_uid
            """, conn, tx))
        {
            insert.Parameters.AddWithValue("i", itemIndex);
            insert.Parameters.AddWithValue("c", (short)TradeRules.CommonClass);
            insert.Parameters.AddWithValue("b", Item.NeverBreaks);
            insert.Parameters.AddWithValue("n", (int)count);
            uid = (long)insert.ExecuteScalar()!;
        }
        using (var own = new NpgsqlCommand("""
            INSERT INTO item_char (char_uid, item_uid, item_where, item_pos)
            VALUES (@ch, @u, @w, (SELECT COALESCE(MIN(p.pos), 0) FROM generate_series(0, 32767) AS p(pos)
                WHERE NOT EXISTS (SELECT 1 FROM item_char o WHERE o.char_uid = @ch AND o.item_where = @w AND o.item_pos = p.pos)))
            """, conn, tx))
        {
            own.Parameters.AddWithValue("ch", unchecked((long)characterId));
            own.Parameters.AddWithValue("u", uid);
            own.Parameters.AddWithValue("w", (short)SlotKind.Inventory);
            own.ExecuteNonQuery();
        }
        return (ulong)uid;
    }

    private static long? LockMoney(NpgsqlConnection conn, NpgsqlTransaction tx, ulong characterId)
    {
        using var cmd = new NpgsqlCommand("SELECT money FROM characters WHERE character_id = @ch FOR UPDATE", conn, tx);
        cmd.Parameters.AddWithValue("ch", unchecked((long)characterId));
        return cmd.ExecuteScalar() is { } money and not DBNull ? Math.Max(0L, Convert.ToInt64(money, System.Globalization.CultureInfo.InvariantCulture)) : null;
    }

    private static void SetMoney(NpgsqlConnection conn, NpgsqlTransaction tx, ulong characterId, uint money) =>
        Execute(conn, tx, "UPDATE characters SET money = @m WHERE character_id = @ch", ("m", (long)money), ("ch", unchecked((long)characterId)));

    private static int BagCount(NpgsqlConnection conn, NpgsqlTransaction tx, ulong characterId)
    {
        using var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM item_char WHERE char_uid = @ch AND item_where = @w", conn, tx);
        cmd.Parameters.AddWithValue("ch", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("w", (short)SlotKind.Inventory);
        return Convert.ToInt32(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void Execute(NpgsqlConnection conn, NpgsqlTransaction tx, string sql, params (string Name, object Value)[] parameters)
    {
        using var cmd = new NpgsqlCommand(sql, conn, tx);
        foreach (var (name, value) in parameters) cmd.Parameters.AddWithValue(name, value);
        cmd.ExecuteNonQuery();
    }
}
