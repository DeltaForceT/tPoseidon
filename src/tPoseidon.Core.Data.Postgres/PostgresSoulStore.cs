using System.Globalization;
using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresSoulStore : ISoulStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresSoulStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public SoulAccount Load(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        uint soul = 0;
        using (var cmd = new NpgsqlCommand("SELECT soul FROM char_soul WHERE char_uid = @c", conn))
        {
            cmd.Parameters.AddWithValue("c", unchecked((long)characterId));
            if (cmd.ExecuteScalar() is { } stored and not DBNull) soul = (uint)Math.Clamp(Convert.ToInt64(stored, CultureInfo.InvariantCulture), 0L, uint.MaxValue);
        }
        var guardians = new List<GuardianSoulRow>();
        using (var cmd = new NpgsqlCommand("""
            SELECT g.guardian_uid, COALESCE(s.guardian_gauge, 0), COALESCE(o.source_item_uid, 0), COALESCE(o.source_count, 0),
                   COALESCE(o.potions_ready, 0), o.last_tick
            FROM char_soulguardian g
            LEFT JOIN char_soulguardian_status s ON s.guardian_uid = g.guardian_uid
            LEFT JOIN char_soulguardian_source o ON o.guardian_uid = g.guardian_uid
            WHERE g.char_uid = @c
            ORDER BY g.slot_num
            """, conn))
        {
            cmd.Parameters.AddWithValue("c", unchecked((long)characterId));
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var lastTick = reader.IsDBNull(5) ? DateTimeOffset.UtcNow : reader.GetFieldValue<DateTimeOffset>(5);
                guardians.Add(new GuardianSoulRow(
                    unchecked((ulong)reader.GetInt64(0)),
                    reader.GetInt32(1),
                    unchecked((ulong)reader.GetInt64(2)),
                    reader.GetInt32(3),
                    reader.GetInt32(4),
                    lastTick));
            }
        }
        return new SoulAccount(soul, guardians);
    }

    public void SaveSoul(ulong characterId, uint soul)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("""
            INSERT INTO char_soul (char_uid, soul, updated_at) VALUES (@c, @s, NOW())
            ON CONFLICT (char_uid) DO UPDATE SET soul = EXCLUDED.soul, updated_at = NOW()
            """, conn);
        cmd.Parameters.AddWithValue("c", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("s", (long)soul);
        cmd.ExecuteNonQuery();
    }

    public bool SaveGuardian(ulong characterId, GuardianSoulRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        using (var owned = new NpgsqlCommand("SELECT 1 FROM char_soulguardian WHERE guardian_uid = @g AND char_uid = @c", conn, tx))
        {
            owned.Parameters.AddWithValue("g", unchecked((long)row.GuardianUid));
            owned.Parameters.AddWithValue("c", unchecked((long)characterId));
            if (owned.ExecuteScalar() is null) return false;
        }
        using (var gauge = new NpgsqlCommand("UPDATE char_soulguardian_status SET guardian_gauge = @v WHERE guardian_uid = @g", conn, tx))
        {
            gauge.Parameters.AddWithValue("v", row.Gauge);
            gauge.Parameters.AddWithValue("g", unchecked((long)row.GuardianUid));
            gauge.ExecuteNonQuery();
        }
        using (var source = new NpgsqlCommand("""
            INSERT INTO char_soulguardian_source (guardian_uid, source_item_uid, source_count, potions_ready, last_tick)
            VALUES (@g, @u, @n, @p, @t)
            ON CONFLICT (guardian_uid) DO UPDATE SET source_item_uid = EXCLUDED.source_item_uid, source_count = EXCLUDED.source_count,
                potions_ready = EXCLUDED.potions_ready, last_tick = EXCLUDED.last_tick
            """, conn, tx))
        {
            source.Parameters.AddWithValue("g", unchecked((long)row.GuardianUid));
            source.Parameters.AddWithValue("u", unchecked((long)row.SourceItemUid));
            source.Parameters.AddWithValue("n", row.SourceCount);
            source.Parameters.AddWithValue("p", row.PotionsReady);
            source.Parameters.AddWithValue("t", row.LastTick.ToUniversalTime());
            source.ExecuteNonQuery();
        }
        tx.Commit();
        return true;
    }
}
