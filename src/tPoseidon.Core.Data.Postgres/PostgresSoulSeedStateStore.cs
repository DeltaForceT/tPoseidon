using Npgsql;
using tPoseidon.Core.Data;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresSoulSeedStateStore : ISoulSeedStateStore
{
    private const string PointColumns =
        "power_point, magic_point, sense_point, charm_point, special_power_point, special_magic_point, special_sense_point, special_charm_point";

    private const short ActiveFlag = 1;
    private const short NotAwakened = 0;
    private const short DefaultSlots = 2;
    private const int PointCount = 8;

    private readonly NpgsqlDataSource _dataSource;

    public PostgresSoulSeedStateStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public SoulSeedUseError Use(ulong characterId, int seedItemIndex)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        short? seedFlag = null;
        short slotMax = 0;
        using (var awakening = new NpgsqlCommand("SELECT soulseed_flag, slot_max_count FROM char_awakening WHERE char_uid = @c", conn, tx))
        {
            awakening.Parameters.AddWithValue("c", (long)characterId);
            using var row = awakening.ExecuteReader();
            if (row.Read())
            {
                seedFlag = row.GetInt16(0);
                slotMax = row.GetInt16(1);
            }
        }
        if (seedFlag is { } flag)
        {
            long owned;
            using (var count = new NpgsqlCommand("SELECT COUNT(*) FROM char_soulguardian WHERE char_uid = @c", conn, tx))
            {
                count.Parameters.AddWithValue("c", (long)characterId);
                owned = (long)count.ExecuteScalar()!;
            }
            if (slotMax <= owned) return SoulSeedUseError.SlotsFull;
            if (flag != 0) return SoulSeedUseError.AlreadyActive;
            using (var reset = new NpgsqlCommand("""
                UPDATE char_soulseed SET seed_item_index = @s,
                    power_point = 0, magic_point = 0, sense_point = 0, charm_point = 0,
                    special_power_point = 0, special_magic_point = 0, special_sense_point = 0, special_charm_point = 0,
                    start_at = NOW(), last_update_at = NOW()
                WHERE char_uid = @c
                """, conn, tx))
            {
                reset.Parameters.AddWithValue("c", (long)characterId);
                reset.Parameters.AddWithValue("s", seedItemIndex);
                reset.ExecuteNonQuery();
            }
            using var activate = new NpgsqlCommand("UPDATE char_awakening SET soulseed_flag = @f WHERE char_uid = @c", conn, tx);
            activate.Parameters.AddWithValue("c", (long)characterId);
            activate.Parameters.AddWithValue("f", ActiveFlag);
            activate.ExecuteNonQuery();
        }
        else
        {
            using (var insertAwakening = new NpgsqlCommand(
                "INSERT INTO char_awakening (char_uid, soulseed_flag, awakening_flag, slot_max_count, created_at) VALUES (@c, @f, @a, @m, NOW())", conn, tx))
            {
                insertAwakening.Parameters.AddWithValue("c", (long)characterId);
                insertAwakening.Parameters.AddWithValue("f", ActiveFlag);
                insertAwakening.Parameters.AddWithValue("a", NotAwakened);
                insertAwakening.Parameters.AddWithValue("m", DefaultSlots);
                insertAwakening.ExecuteNonQuery();
            }
            using var insertSeed = new NpgsqlCommand($"""
                INSERT INTO char_soulseed (char_uid, seed_item_index, {PointColumns}, start_at, last_update_at)
                VALUES (@c, @s, 0, 0, 0, 0, 0, 0, 0, 0, NOW(), NOW())
                """, conn, tx);
            insertSeed.Parameters.AddWithValue("c", (long)characterId);
            insertSeed.Parameters.AddWithValue("s", seedItemIndex);
            insertSeed.ExecuteNonQuery();
        }
        Log(conn, tx, characterId);
        tx.Commit();
        return SoulSeedUseError.None;
    }

    public bool SavePoints(ulong characterId, IReadOnlyList<short> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        using (var update = new NpgsqlCommand("""
            UPDATE char_soulseed SET power_point = @p0, magic_point = @p1, sense_point = @p2, charm_point = @p3,
                special_power_point = @p4, special_magic_point = @p5, special_sense_point = @p6, special_charm_point = @p7,
                last_update_at = NOW()
            WHERE char_uid = @c
            """, conn, tx))
        {
            update.Parameters.AddWithValue("c", (long)characterId);
            for (var i = 0; i < PointCount; i++) update.Parameters.AddWithValue("p" + i, i < points.Count ? points[i] : (short)0);
            if (update.ExecuteNonQuery() == 0) return false;
        }
        Log(conn, tx, characterId);
        tx.Commit();
        return true;
    }

    public SoulSeedState? LoadActive(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand($"""
            SELECT s.seed_item_index, s.start_at, s.last_update_at, {Prefixed("s.")}
            FROM char_soulseed s
            JOIN char_awakening a ON a.char_uid = s.char_uid
            WHERE s.char_uid = @c AND a.soulseed_flag <> 0
            """, conn);
        cmd.Parameters.AddWithValue("c", (long)characterId);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        var points = new short[PointCount];
        for (var i = 0; i < points.Length; i++) points[i] = reader.GetInt16(3 + i);
        return new SoulSeedState(
            reader.GetInt32(0),
            new DateTimeOffset(reader.GetDateTime(1), TimeSpan.Zero),
            new DateTimeOffset(reader.GetDateTime(2), TimeSpan.Zero),
            points);
    }

    private static string Prefixed(string alias) =>
        string.Join(", ", PointColumns.Split(", ").Select(column => alias + column));

    private static void Log(NpgsqlConnection conn, NpgsqlTransaction tx, ulong characterId)
    {
        using var log = new NpgsqlCommand($"""
            INSERT INTO char_soulseed_log (char_uid, seed_item_index, {PointColumns}, start_at, last_update_at)
            SELECT char_uid, seed_item_index, {PointColumns}, start_at, last_update_at FROM char_soulseed WHERE char_uid = @c
            """, conn, tx);
        log.Parameters.AddWithValue("c", (long)characterId);
        log.ExecuteNonQuery();
    }
}
