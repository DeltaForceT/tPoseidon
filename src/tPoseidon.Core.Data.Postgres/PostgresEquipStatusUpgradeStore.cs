using System.Globalization;
using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresEquipStatusUpgradeStore : IEquipStatusUpgradeStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresEquipStatusUpgradeStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public EquipStatusUpgradeResult Apply(EquipStatusUpgrade upgrade)
    {
        ArgumentNullException.ThrowIfNull(upgrade);
        var touched = upgrade.Items.Where(item => item.Action != EquipUpgradeAction.Insert).Select(item => item.ItemUid).ToList();
        if (touched.Distinct().Count() != touched.Count) return EquipStatusUpgradeResult.Refused(EquipStatusUpgradeRules.ItemMismatch);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        if (LockMoney(conn, tx, upgrade.CharacterId) is not { } money) return EquipStatusUpgradeResult.Refused(EquipStatusUpgradeRules.ItemMismatch);
        if (money < upgrade.Fee) return EquipStatusUpgradeResult.Refused(EquipStatusUpgradeRules.NotEnoughMoney);
        var applied = new List<EquipUpgradeItem>(upgrade.Items.Count);
        foreach (var item in upgrade.Items)
        {
            switch (item.Action)
            {
                case EquipUpgradeAction.Delete:
                    if (!Held(conn, tx, upgrade.CharacterId, item)) return EquipStatusUpgradeResult.Refused(EquipStatusUpgradeRules.ItemMismatch);
                    Execute(conn, tx, "DELETE FROM item_char WHERE item_uid = @u", ("u", unchecked((long)item.ItemUid)));
                    Execute(conn, tx, "DELETE FROM items WHERE item_uid = @u", ("u", unchecked((long)item.ItemUid)));
                    applied.Add(item);
                    break;
                case EquipUpgradeAction.Update:
                    if (!Held(conn, tx, upgrade.CharacterId, item)) return EquipStatusUpgradeResult.Refused(EquipStatusUpgradeRules.ItemMismatch);
                    Execute(conn, tx, """
                        UPDATE items SET item_stack = @n, item_slot = @s, refine_type = @rt, refine_level = @rl, refine_state = @rs
                        WHERE item_uid = @u
                        """,
                        ("n", (int)item.Count), ("s", (short)item.Slot), ("rt", item.RefineType), ("rl", (short)item.RefineLevel),
                        ("rs", (short)item.RefineState), ("u", unchecked((long)item.ItemUid)));
                    applied.Add(item);
                    break;
                default:
                    var breakedAt = Item.BreakedAfter(item.ItemClass, item.PeriodMinutes, DateTimeOffset.UtcNow);
                    applied.Add(item with { ItemUid = Insert(conn, tx, upgrade.CharacterId, item, breakedAt), BreakedAt = breakedAt > Item.NeverBreaks ? breakedAt : null });
                    break;
            }
        }
        if (BagCount(conn, tx, upgrade.CharacterId) > upgrade.BagCapacity) return EquipStatusUpgradeResult.Refused(EquipStatusUpgradeRules.BagFull);
        var remain = money - upgrade.Fee;
        Execute(conn, tx, "UPDATE characters SET money = @m WHERE character_id = @ch", ("m", remain), ("ch", unchecked((long)upgrade.CharacterId)));
        tx.Commit();
        return new EquipStatusUpgradeResult(EquipStatusUpgradeRules.Ok, remain, applied);
    }

    private static bool Held(NpgsqlConnection conn, NpgsqlTransaction tx, ulong characterId, EquipUpgradeItem item)
    {
        using var cmd = new NpgsqlCommand("""
            SELECT i.item_index FROM items i JOIN item_char c ON c.item_uid = i.item_uid
            WHERE i.item_uid = @u AND c.char_uid = @ch AND c.item_where = @w
            FOR UPDATE
            """, conn, tx);
        cmd.Parameters.AddWithValue("u", unchecked((long)item.ItemUid));
        cmd.Parameters.AddWithValue("ch", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("w", (short)SlotKind.Inventory);
        return cmd.ExecuteScalar() is int index && index == item.ItemIndex;
    }

    private static ulong Insert(NpgsqlConnection conn, NpgsqlTransaction tx, ulong characterId, EquipUpgradeItem item, DateTimeOffset breakedAt)
    {
        long uid;
        using (var insert = new NpgsqlCommand("""
            INSERT INTO items (item_index, item_class, item_attr, item_charged, item_breaked, refine_type, refine_level, refine_state, item_slot, item_stack)
            VALUES (@i, @c, 0, 0, @b, @rt, @rl, @rs, @s, @n)
            RETURNING item_uid
            """, conn, tx))
        {
            insert.Parameters.AddWithValue("i", item.ItemIndex);
            insert.Parameters.AddWithValue("c", (short)item.ItemClass);
            insert.Parameters.AddWithValue("b", breakedAt.UtcDateTime);
            insert.Parameters.AddWithValue("rt", item.RefineType);
            insert.Parameters.AddWithValue("rl", (short)item.RefineLevel);
            insert.Parameters.AddWithValue("rs", (short)item.RefineState);
            insert.Parameters.AddWithValue("s", (short)item.Slot);
            insert.Parameters.AddWithValue("n", (int)item.Count);
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
        return cmd.ExecuteScalar() is { } money and not DBNull ? Math.Max(0L, Convert.ToInt64(money, CultureInfo.InvariantCulture)) : null;
    }

    private static int BagCount(NpgsqlConnection conn, NpgsqlTransaction tx, ulong characterId)
    {
        using var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM item_char WHERE char_uid = @ch AND item_where = @w", conn, tx);
        cmd.Parameters.AddWithValue("ch", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("w", (short)SlotKind.Inventory);
        return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static void Execute(NpgsqlConnection conn, NpgsqlTransaction tx, string sql, params (string Name, object Value)[] parameters)
    {
        using var cmd = new NpgsqlCommand(sql, conn, tx);
        foreach (var (name, value) in parameters) cmd.Parameters.AddWithValue(name, value);
        cmd.ExecuteNonQuery();
    }
}
