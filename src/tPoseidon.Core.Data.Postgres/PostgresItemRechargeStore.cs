using System.Globalization;
using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresItemRechargeStore : IItemRechargeStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresItemRechargeStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public ItemRechargeResult Recharge(ItemRecharge recharge, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(recharge);
        if (recharge.ItemUid == recharge.CouponUid || recharge.CouponCount < 1) return ItemRechargeResult.Refused(ItemRechargeErrors.CouponNotHeld);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        if (HeldBreaked(conn, tx, recharge.CharacterId, recharge.ItemUid) is not { } breaked) return ItemRechargeResult.Refused(ItemRechargeErrors.ItemNotHeld);
        if (HeldStack(conn, tx, recharge.CharacterId, recharge.CouponUid) is not { } stack) return ItemRechargeResult.Refused(ItemRechargeErrors.CouponNotHeld);
        if (stack < recharge.CouponCount) return ItemRechargeResult.Refused(ItemRechargeErrors.NotEnoughCoupons);
        var expiry = (breaked > now ? breaked : now).AddHours(recharge.Hours);
        Execute(conn, tx, "UPDATE items SET item_breaked = @b WHERE item_uid = @u", ("b", expiry), ("u", unchecked((long)recharge.ItemUid)));
        var left = stack - recharge.CouponCount;
        if (left > 0)
        {
            Execute(conn, tx, "UPDATE items SET item_stack = @n WHERE item_uid = @u", ("n", left), ("u", unchecked((long)recharge.CouponUid)));
        }
        else
        {
            Execute(conn, tx, "DELETE FROM item_char WHERE item_uid = @u", ("u", unchecked((long)recharge.CouponUid)));
            Execute(conn, tx, "DELETE FROM items WHERE item_uid = @u", ("u", unchecked((long)recharge.CouponUid)));
        }
        tx.Commit();
        return new ItemRechargeResult(ItemRechargeErrors.Ok, expiry, left);
    }

    private static DateTimeOffset? HeldBreaked(NpgsqlConnection conn, NpgsqlTransaction tx, ulong characterId, ulong itemUid)
    {
        using var cmd = Held("i.item_breaked", conn, tx, characterId, itemUid);
        return cmd.ExecuteScalar() is DateTime breaked ? new DateTimeOffset(DateTime.SpecifyKind(breaked, DateTimeKind.Utc)) : null;
    }

    private static int? HeldStack(NpgsqlConnection conn, NpgsqlTransaction tx, ulong characterId, ulong itemUid)
    {
        using var cmd = Held("i.item_stack", conn, tx, characterId, itemUid);
        return cmd.ExecuteScalar() is { } stack and not DBNull ? Convert.ToInt32(stack, CultureInfo.InvariantCulture) : null;
    }

    private static NpgsqlCommand Held(string column, NpgsqlConnection conn, NpgsqlTransaction tx, ulong characterId, ulong itemUid)
    {
        var cmd = new NpgsqlCommand($"""
            SELECT {column} FROM items i JOIN item_char c ON c.item_uid = i.item_uid
            WHERE i.item_uid = @u AND c.char_uid = @ch AND c.item_where = @w
            FOR UPDATE
            """, conn, tx);
        cmd.Parameters.AddWithValue("u", unchecked((long)itemUid));
        cmd.Parameters.AddWithValue("ch", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("w", (short)SlotKind.Inventory);
        return cmd;
    }

    private static void Execute(NpgsqlConnection conn, NpgsqlTransaction tx, string sql, params (string Name, object Value)[] parameters)
    {
        using var cmd = new NpgsqlCommand(sql, conn, tx);
        foreach (var (name, value) in parameters) cmd.Parameters.AddWithValue(name, value);
        cmd.ExecuteNonQuery();
    }
}
