using Npgsql;
using tPoseidon.Core.Data;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresGachaStore : IGachaStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresGachaStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public int CouponCount(uint accountId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        return Unused(conn, null, accountId);
    }

    public void GrantCoupons(uint accountId, int count, GachaCouponSource source)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "INSERT INTO gacha_coupon (account_id, source) SELECT @a, @s FROM generate_series(1, @n)", conn);
        cmd.Parameters.AddWithValue("a", (long)accountId);
        cmd.Parameters.AddWithValue("s", (short)source);
        cmd.Parameters.AddWithValue("n", count);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<GachaPrize> Pool(short area, bool deep)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        return Pool(conn, null, area, deep);
    }

    public GachaDrawResult Draw(GachaDrawRequest request, Func<IReadOnlyList<GachaPrize>, GachaPrize> pick)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pick);
        var cost = GachaRules.CouponsFor(request.Deep);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        var coupons = new List<long>(cost);
        using (var take = new NpgsqlCommand(
            "SELECT coupon_id FROM gacha_coupon WHERE account_id = @a AND spent_at IS NULL ORDER BY coupon_id LIMIT @n FOR UPDATE", conn, tx))
        {
            take.Parameters.AddWithValue("a", (long)request.AccountId);
            take.Parameters.AddWithValue("n", cost);
            using var reader = take.ExecuteReader();
            while (reader.Read()) coupons.Add(reader.GetInt64(0));
        }
        if (coupons.Count < cost) return new GachaDrawResult(GachaDrawOutcome.NotEnoughCoupons, null, 0, Unused(conn, tx, request.AccountId));
        var pool = Pool(conn, tx, request.Area, request.Deep);
        if (pool.Count == 0) return new GachaDrawResult(GachaDrawOutcome.EmptyPool, null, 0, Unused(conn, tx, request.AccountId));
        var prize = pick(pool);
        var poolWeight = pool.Sum(entry => (long)entry.Weight);
        var chargedUid = PostgresChargedItemStore.Insert(conn, tx, new ChargedItemGrant(
            request.AccountId, prize.ItemClass, prize.ItemIndex, prize.Count, prize.UseTimeHours, prize.ItemFlag,
            checked((int)(coupons[0] + ChargedItemRules.GachaSellCodeBase)), ChargedItemRules.PurchaseSellType));
        long drawId;
        using (var log = new NpgsqlCommand("""
            INSERT INTO gacha_draw (account_id, area, deep, item_index, charged_item_uid, coupons, pool_weight, remote_address)
            VALUES (@a, @area, @deep, @i, @u, @c, @w, @r) RETURNING draw_id
            """, conn, tx))
        {
            log.Parameters.AddWithValue("a", (long)request.AccountId);
            log.Parameters.AddWithValue("area", request.Area);
            log.Parameters.AddWithValue("deep", request.Deep);
            log.Parameters.AddWithValue("i", prize.ItemIndex);
            log.Parameters.AddWithValue("u", (long)chargedUid);
            log.Parameters.AddWithValue("c", (short)cost);
            log.Parameters.AddWithValue("w", checked((int)poolWeight));
            log.Parameters.AddWithValue("r", request.RemoteAddress);
            drawId = (long)log.ExecuteScalar()!;
        }
        using (var spend = new NpgsqlCommand(
            "UPDATE gacha_coupon SET spent_at = NOW(), draw_id = @d WHERE coupon_id = ANY(@ids) AND spent_at IS NULL", conn, tx))
        {
            spend.Parameters.AddWithValue("d", drawId);
            spend.Parameters.AddWithValue("ids", coupons.ToArray());
            if (spend.ExecuteNonQuery() != cost) throw new InvalidOperationException($"Draw {drawId} could not spend its {cost} coupon(s).");
        }
        var left = Unused(conn, tx, request.AccountId);
        tx.Commit();
        return new GachaDrawResult(GachaDrawOutcome.Won, prize, chargedUid, left);
    }

    private static int Unused(NpgsqlConnection conn, NpgsqlTransaction? tx, uint accountId)
    {
        using var cmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM gacha_coupon WHERE account_id = @a AND spent_at IS NULL", conn, tx);
        cmd.Parameters.AddWithValue("a", (long)accountId);
        return (int)(long)cmd.ExecuteScalar()!;
    }

    private static List<GachaPrize> Pool(NpgsqlConnection conn, NpgsqlTransaction? tx, short area, bool deep)
    {
        using var cmd = new NpgsqlCommand(
            "SELECT item_index, item_class, item_count, use_time_hours, item_flag, weight FROM gacha_prize WHERE area = @area AND deep = @deep ORDER BY item_index",
            conn, tx);
        cmd.Parameters.AddWithValue("area", area);
        cmd.Parameters.AddWithValue("deep", deep);
        using var reader = cmd.ExecuteReader();
        var pool = new List<GachaPrize>();
        while (reader.Read())
        {
            pool.Add(new GachaPrize(
                reader.GetInt32(0), (byte)reader.GetInt16(1), reader.GetInt16(2), reader.GetInt16(3), (byte)reader.GetInt16(4), reader.GetInt32(5)));
        }
        return pool;
    }
}
