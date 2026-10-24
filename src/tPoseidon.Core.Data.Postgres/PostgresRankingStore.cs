using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresRankingStore : IRankingStore
{
    private const int KillsFirstType = 5;
    private const int TimeFirstType = 6;

    private readonly NpgsqlDataSource _dataSource;

    public PostgresRankingStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public int RebuildMonsterRanks(DateTime weekStart, DateTime weekEnd, int topCount)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        using (var clear = new NpgsqlCommand("DELETE FROM ranking", conn, tx)) clear.ExecuteNonQuery();
        var inserted = 0;
        foreach (var (type, order) in new[] { (KillsFirstType, "h.kill_count DESC, h.record_time"), (TimeFirstType, "h.record_time, h.kill_count DESC") })
        {
            using var fill = new NpgsqlCommand($"""
                INSERT INTO ranking (ranking_type, monster_id, ranking_order, char_uid, char_type, char_name, value1, value2, value3)
                SELECT @type, monster_id, ranked, char_uid, species, name, kill_count, record_time, 0
                FROM (
                    SELECT h.monster_id, h.char_uid, c.species, c.name, h.kill_count, h.record_time,
                           ROW_NUMBER() OVER (PARTITION BY h.monster_id ORDER BY {order}) AS ranked
                    FROM quest_history h JOIN characters c ON c.character_id = h.char_uid
                    WHERE h.quest_completed_date >= @start AND h.quest_completed_date <= @end
                      AND h.quest_id > 0 AND h.monster_id > 0
                ) ordered
                WHERE ranked <= @top
                """, conn, tx);
            fill.Parameters.AddWithValue("type", (short)type);
            fill.Parameters.Add(new NpgsqlParameter("start", NpgsqlTypes.NpgsqlDbType.TimestampTz) { Value = new DateTimeOffset(weekStart).UtcDateTime });
            fill.Parameters.Add(new NpgsqlParameter("end", NpgsqlTypes.NpgsqlDbType.TimestampTz) { Value = new DateTimeOffset(weekEnd).UtcDateTime });
            fill.Parameters.AddWithValue("top", topCount);
            inserted += fill.ExecuteNonQuery();
        }
        tx.Commit();
        return inserted;
    }

    public IReadOnlyList<RankingRow> Top(int rankingType, int monsterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("""
            SELECT ranking_order, char_uid, char_type, char_name, value1, value2, value3 FROM ranking
            WHERE ranking_type = @type AND monster_id = @monster ORDER BY ranking_order
            """, conn);
        cmd.Parameters.AddWithValue("type", (short)rankingType);
        cmd.Parameters.AddWithValue("monster", monsterId);
        using var reader = cmd.ExecuteReader();
        var rows = new List<RankingRow>();
        while (reader.Read())
        {
            rows.Add(new RankingRow(reader.GetInt16(0), (ulong)reader.GetInt64(1), reader.GetInt16(2), reader.GetString(3),
                reader.GetInt32(4), reader.GetInt32(5), reader.GetInt32(6)));
        }
        return rows;
    }
}
