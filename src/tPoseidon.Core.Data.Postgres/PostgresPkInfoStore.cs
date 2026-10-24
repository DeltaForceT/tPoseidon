using tPoseidon.Core.Data;
using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresPkInfoStore : IPkInfoStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresPkInfoStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public PkInfo GetOrCreate(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO pkinfo (char_uid)
            VALUES (@char_uid)
            ON CONFLICT (char_uid) DO UPDATE
                SET char_uid = EXCLUDED.char_uid
            RETURNING char_uid, kill_today_count, kill_total_count,
                      death_today_count, death_total_count, latest_update_at
            """,
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        using var reader = cmd.ExecuteReader();
        reader.Read();
        return Read(reader);
    }

    public PkInfo IncrementKill(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO pkinfo (char_uid, kill_today_count, kill_total_count, latest_update_at)
            VALUES (@char_uid, 1, 1, NOW())
            ON CONFLICT (char_uid) DO UPDATE
                SET kill_today_count  = pkinfo.kill_today_count + 1,
                    kill_total_count  = pkinfo.kill_total_count + 1,
                    latest_update_at  = NOW()
            RETURNING char_uid, kill_today_count, kill_total_count,
                      death_today_count, death_total_count, latest_update_at
            """,
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        using var reader = cmd.ExecuteReader();
        reader.Read();
        return Read(reader);
    }

    public PkInfo IncrementDeath(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO pkinfo (char_uid, death_today_count, death_total_count, latest_update_at)
            VALUES (@char_uid, 1, 1, NOW())
            ON CONFLICT (char_uid) DO UPDATE
                SET death_today_count = pkinfo.death_today_count + 1,
                    death_total_count = pkinfo.death_total_count + 1,
                    latest_update_at  = NOW()
            RETURNING char_uid, kill_today_count, kill_total_count,
                      death_today_count, death_total_count, latest_update_at
            """,
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        using var reader = cmd.ExecuteReader();
        reader.Read();
        return Read(reader);
    }

    public PkInfo Save(PkInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO pkinfo (char_uid, kill_today_count, kill_total_count, death_today_count, death_total_count, latest_update_at)
            VALUES (@char_uid, @kill_today, @kill_total, @death_today, @death_total, NOW())
            ON CONFLICT (char_uid) DO UPDATE
                SET kill_today_count  = EXCLUDED.kill_today_count,
                    kill_total_count  = EXCLUDED.kill_total_count,
                    death_today_count = EXCLUDED.death_today_count,
                    death_total_count = EXCLUDED.death_total_count,
                    latest_update_at  = NOW()
            RETURNING char_uid, kill_today_count, kill_total_count,
                      death_today_count, death_total_count, latest_update_at
            """,
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)info.CharacterId));
        cmd.Parameters.AddWithValue("kill_today", info.KillTodayCount);
        cmd.Parameters.AddWithValue("kill_total", info.KillTotalCount);
        cmd.Parameters.AddWithValue("death_today", info.DeathTodayCount);
        cmd.Parameters.AddWithValue("death_total", info.DeathTotalCount);
        using var reader = cmd.ExecuteReader();
        reader.Read();
        return Read(reader);
    }

    public bool ResetTodayCounts(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            UPDATE pkinfo
            SET kill_today_count  = 0,
                death_today_count = 0,
                latest_update_at  = NOW()
            WHERE char_uid = @char_uid
            """,
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        return cmd.ExecuteNonQuery() == 1;
    }

    public int ResetAllTodayCounts()
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "UPDATE pkinfo SET kill_today_count = 0, death_today_count = 0, latest_update_at = NOW()",
            conn);
        return cmd.ExecuteNonQuery();
    }

    public void AppendLog(PkInfo snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO pkinfo_log
                (char_uid, kill_today_count, kill_total_count,
                 death_today_count, death_total_count, log_at)
            VALUES
                (@char_uid, @kt, @ktot, @dt, @dtot, NOW())
            """,
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)snapshot.CharacterId));
        cmd.Parameters.AddWithValue("kt", snapshot.KillTodayCount);
        cmd.Parameters.AddWithValue("ktot", snapshot.KillTotalCount);
        cmd.Parameters.AddWithValue("dt", snapshot.DeathTodayCount);
        cmd.Parameters.AddWithValue("dtot", snapshot.DeathTotalCount);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<PkInfo> GetLog(ulong characterId, int limit = 50)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            SELECT char_uid, kill_today_count, kill_total_count,
                   death_today_count, death_total_count, log_at
            FROM pkinfo_log
            WHERE char_uid = @char_uid
            ORDER BY log_seq DESC
            LIMIT @limit
            """,
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("limit", limit);
        using var reader = cmd.ExecuteReader();
        var result = new List<PkInfo>();
        while (reader.Read())
        {
            result.Add(new PkInfo
            {
                CharacterId = unchecked((ulong)reader.GetInt64(0)),
                KillTodayCount = reader.GetInt32(1),
                KillTotalCount = reader.GetInt32(2),
                DeathTodayCount = reader.GetInt32(3),
                DeathTotalCount = reader.GetInt32(4),
                LatestUpdateAt = new DateTimeOffset(reader.GetDateTime(5), TimeSpan.Zero),
            });
        }
        return result;
    }

    private static PkInfo Read(NpgsqlDataReader reader) => new()
    {
        CharacterId = unchecked((ulong)reader.GetInt64(0)),
        KillTodayCount = reader.GetInt32(1),
        KillTotalCount = reader.GetInt32(2),
        DeathTodayCount = reader.GetInt32(3),
        DeathTotalCount = reader.GetInt32(4),
        LatestUpdateAt = new DateTimeOffset(reader.GetDateTime(5), TimeSpan.Zero),
    };
}
