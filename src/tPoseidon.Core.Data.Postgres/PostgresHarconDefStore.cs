using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresHarconDefStore : IHarconDefStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresHarconDefStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public HarconDefRecord Load(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        var coins = 0;
        var count = 0;
        DateOnly? day = null;
        using (var cmd = new NpgsqlCommand("SELECT coins, entry_count, entry_day FROM harcon_player WHERE char_uid = @c", conn))
        {
            cmd.Parameters.AddWithValue("c", (long)characterId);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                coins = reader.GetInt32(0);
                count = reader.GetInt16(1);
                day = reader.IsDBNull(2) ? null : DateOnly.FromDateTime(reader.GetDateTime(2));
            }
        }
        var cleared = new HashSet<int>();
        using (var cmd = new NpgsqlCommand("SELECT stage_id FROM harcon_clear WHERE char_uid = @c", conn))
        {
            cmd.Parameters.AddWithValue("c", (long)characterId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) cleared.Add(reader.GetInt16(0));
        }
        return new HarconDefRecord(coins, count, day, cleared);
    }

    public void SaveEntries(ulong characterId, int count, DateOnly day)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("""
            INSERT INTO harcon_player (char_uid, entry_count, entry_day) VALUES (@c, @n, @d)
            ON CONFLICT (char_uid) DO UPDATE SET entry_count = EXCLUDED.entry_count, entry_day = EXCLUDED.entry_day
            """, conn);
        cmd.Parameters.AddWithValue("c", (long)characterId);
        cmd.Parameters.AddWithValue("n", (short)count);
        cmd.Parameters.Add(new NpgsqlParameter("d", NpgsqlTypes.NpgsqlDbType.Date) { Value = day });
        cmd.ExecuteNonQuery();
    }

    public int AddCoins(ulong characterId, int delta)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("""
            INSERT INTO harcon_player (char_uid, coins) VALUES (@c, GREATEST(@n, 0))
            ON CONFLICT (char_uid) DO UPDATE SET coins = GREATEST(harcon_player.coins + @n, 0)
            RETURNING coins
            """, conn);
        cmd.Parameters.AddWithValue("c", (long)characterId);
        cmd.Parameters.AddWithValue("n", delta);
        return Convert.ToInt32(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    public HarconDefTotals Totals(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        int plays = 0, wins = 0, losses = 0;
        using (var cmd = new NpgsqlCommand("SELECT plays, wins, losses FROM harcon_player WHERE char_uid = @c", conn))
        {
            cmd.Parameters.AddWithValue("c", (long)characterId);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                plays = reader.GetInt32(0);
                wins = reader.GetInt32(1);
                losses = reader.GetInt32(2);
            }
        }
        var stages = new List<(int StageId, int Clears)>(HarconDefTotals.ShownStages);
        using (var cmd = new NpgsqlCommand("SELECT stage_id, clear_count FROM harcon_clear WHERE char_uid = @c ORDER BY clear_count DESC, stage_id LIMIT @n", conn))
        {
            cmd.Parameters.AddWithValue("c", (long)characterId);
            cmd.Parameters.AddWithValue("n", HarconDefTotals.ShownStages);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) stages.Add((reader.GetInt16(0), reader.GetInt32(1)));
        }
        return new HarconDefTotals(plays, wins, losses, stages);
    }

    public void AddResult(ulong characterId, bool won)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("""
            INSERT INTO harcon_player (char_uid, plays, wins, losses) VALUES (@c, 1, @w, @l)
            ON CONFLICT (char_uid) DO UPDATE SET plays = harcon_player.plays + 1, wins = harcon_player.wins + @w, losses = harcon_player.losses + @l
            """, conn);
        cmd.Parameters.AddWithValue("c", (long)characterId);
        cmd.Parameters.AddWithValue("w", won ? 1 : 0);
        cmd.Parameters.AddWithValue("l", won ? 0 : 1);
        cmd.ExecuteNonQuery();
    }

    public HarconStageRecord StageRecord(ulong characterId, int stageId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("""
            SELECT plays, wins, losses, total_score, contribution_sum, best_score, best_rank, best_contribution,
                   best_deaths, best_tower_score, best_towers_built
            FROM harcon_stage_record WHERE char_uid = @c AND stage_id = @s
            """, conn);
        cmd.Parameters.AddWithValue("c", (long)characterId);
        cmd.Parameters.AddWithValue("s", (short)stageId);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return HarconStageRecord.Empty(stageId);
        return new HarconStageRecord(
            stageId, reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt64(3), reader.GetFloat(4),
            reader.GetInt32(5), reader.GetInt16(6), reader.GetFloat(7), reader.GetInt16(8), reader.GetInt32(9), reader.GetInt16(10));
    }

    public void AddStageResult(ulong characterId, HarconStageResult result)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("""
            INSERT INTO harcon_stage_record (char_uid, stage_id, plays, wins, losses, total_score, contribution_sum,
                best_score, best_rank, best_contribution, best_deaths, best_tower_score, best_towers_built)
            VALUES (@c, @s, 1, @w, @l, @score, @contribution, @score, @rank, @contribution, @deaths, @towerScore, @towers)
            ON CONFLICT (char_uid, stage_id) DO UPDATE SET
                plays = harcon_stage_record.plays + 1,
                wins = harcon_stage_record.wins + @w,
                losses = harcon_stage_record.losses + @l,
                total_score = harcon_stage_record.total_score + @score,
                contribution_sum = harcon_stage_record.contribution_sum + @contribution,
                best_rank = CASE WHEN @score > harcon_stage_record.best_score THEN @rank ELSE harcon_stage_record.best_rank END,
                best_contribution = CASE WHEN @score > harcon_stage_record.best_score THEN @contribution ELSE harcon_stage_record.best_contribution END,
                best_deaths = CASE WHEN @score > harcon_stage_record.best_score THEN @deaths ELSE harcon_stage_record.best_deaths END,
                best_tower_score = CASE WHEN @score > harcon_stage_record.best_score THEN @towerScore ELSE harcon_stage_record.best_tower_score END,
                best_towers_built = CASE WHEN @score > harcon_stage_record.best_score THEN @towers ELSE harcon_stage_record.best_towers_built END,
                best_score = GREATEST(harcon_stage_record.best_score, @score)
            """, conn);
        cmd.Parameters.AddWithValue("c", (long)characterId);
        cmd.Parameters.AddWithValue("s", (short)result.StageId);
        cmd.Parameters.AddWithValue("w", result.Won ? 1 : 0);
        cmd.Parameters.AddWithValue("l", result.Won ? 0 : 1);
        cmd.Parameters.AddWithValue("score", result.Score);
        cmd.Parameters.AddWithValue("contribution", result.Contribution);
        cmd.Parameters.AddWithValue("rank", (short)Math.Clamp(result.Rank, 0, short.MaxValue));
        cmd.Parameters.AddWithValue("deaths", (short)Math.Clamp(result.Deaths, 0, short.MaxValue));
        cmd.Parameters.AddWithValue("towerScore", result.TowerScore);
        cmd.Parameters.AddWithValue("towers", (short)Math.Clamp(result.TowersBuilt, 0, short.MaxValue));
        cmd.ExecuteNonQuery();
    }

    public void AddClear(ulong characterId, int stageId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("""
            INSERT INTO harcon_clear (char_uid, stage_id) VALUES (@c, @s)
            ON CONFLICT (char_uid, stage_id) DO UPDATE SET clear_count = harcon_clear.clear_count + 1
            """, conn);
        cmd.Parameters.AddWithValue("c", (long)characterId);
        cmd.Parameters.AddWithValue("s", (short)stageId);
        cmd.ExecuteNonQuery();
    }
}
