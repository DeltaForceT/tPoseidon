using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresGuildMatchStore : IGuildMatchStore
{
    public const short MakeJoinType = 0;
    public const short ChallengeJoinType = 1;
    public const short MatchPointHistoryType = 3;
    public const long MatchPointCharacter = 0;

    private readonly NpgsqlDataSource _dataSource;

    public PostgresGuildMatchStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public GuildLadderStanding? Standing(uint guildCode)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("""
            SELECT guild_ladder, match_count, win_count, lose_count, draw_count, disc_count
            FROM guild_ladder WHERE guild_code = @g
            """, conn);
        cmd.Parameters.AddWithValue("g", (long)guildCode);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new GuildLadderStanding(guildCode, reader.GetInt32(0), reader.GetInt16(1), reader.GetInt16(2), reader.GetInt16(3), reader.GetInt16(4), reader.GetInt16(5));
    }

    public IReadOnlyList<GuildLadderRankRow> TopLadder(int count)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("""
            SELECT l.guild_code, g.guild_name, l.guild_ladder, l.match_count, l.win_count, l.lose_count, l.draw_count, l.disc_count
            FROM guild_ladder AS l JOIN guild AS g ON g.guild_code = l.guild_code
            WHERE l.guild_ladder > 0
            ORDER BY l.guild_ladder DESC, l.guild_code
            LIMIT @n
            """, conn);
        cmd.Parameters.AddWithValue("n", Math.Max(0, count));
        using var reader = cmd.ExecuteReader();
        var rows = new List<GuildLadderRankRow>();
        while (reader.Read())
        {
            var guildCode = (uint)reader.GetInt64(0);
            rows.Add(new GuildLadderRankRow(reader.GetString(1), new GuildLadderStanding(
                guildCode, reader.GetInt32(2), reader.GetInt16(3), reader.GetInt16(4), reader.GetInt16(5), reader.GetInt16(6), reader.GetInt16(7))));
        }
        return rows;
    }

    public int LadderPosition(uint guildCode)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("""
            SELECT CASE WHEN own.guild_ladder > 0
                        THEN (SELECT COUNT(*) FROM guild_ladder AS other WHERE other.guild_ladder > own.guild_ladder) + 1
                        ELSE 0 END
            FROM guild_ladder AS own WHERE own.guild_code = @g
            """, conn);
        cmd.Parameters.AddWithValue("g", (long)guildCode);
        return cmd.ExecuteScalar() is { } position and not DBNull ? Convert.ToInt32(position, System.Globalization.CultureInfo.InvariantCulture) : GuildLadderRules.NotRanked;
    }

    public int MatchesBetween(uint guildA, uint guildB, DateTimeOffset dayStart, DateTimeOffset dayEnd)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("""
            SELECT COUNT(*) FROM (
                SELECT b.match_code
                FROM guild_game_match AS a
                INNER JOIN guild_match AS b ON a.match_code = b.match_code
                WHERE a.match_begin BETWEEN @start AND @end AND b.guild_code IN (@a, @b)
                GROUP BY b.match_code
                HAVING COUNT(*) = 2
            ) AS g
            """, conn);
        cmd.Parameters.AddWithValue("start", dayStart.ToUniversalTime());
        cmd.Parameters.AddWithValue("end", dayEnd.ToUniversalTime());
        cmd.Parameters.AddWithValue("a", (long)guildA);
        cmd.Parameters.AddWithValue("b", (long)guildB);
        return (int)(long)cmd.ExecuteScalar()!;
    }

    public int Insert(GuildMatchRecord match)
    {
        ArgumentNullException.ThrowIfNull(match);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        long code;
        using (var cmd = new NpgsqlCommand("""
            INSERT INTO guild_game_match (match_begin, match_end, match_type, match_map, match_timeout, winning_count, etc_num)
            VALUES (@begin, @end, @type, @map, @timeout, @winning, @etc)
            RETURNING match_code
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue("begin", match.Begin.ToUniversalTime());
            cmd.Parameters.AddWithValue("end", match.End.ToUniversalTime());
            cmd.Parameters.AddWithValue("type", (short)match.MatchType);
            cmd.Parameters.AddWithValue("map", (int)match.MatchMap);
            cmd.Parameters.AddWithValue("timeout", (short)match.TimeoutMinutes);
            cmd.Parameters.AddWithValue("winning", (int)match.WinningCount);
            cmd.Parameters.AddWithValue("etc", match.EtcNum);
            code = (long)cmd.ExecuteScalar()!;
        }
        InsertSide(conn, tx, code, MakeJoinType, match.Make);
        InsertSide(conn, tx, code, ChallengeJoinType, match.Challenge);
        if (!Settle(conn, tx, match.Make))
        {
            tx.Rollback();
            return GuildMatchStoreErrors.MakeLadderMissing;
        }
        if (!Settle(conn, tx, match.Challenge))
        {
            tx.Rollback();
            return GuildMatchStoreErrors.ChallengeLadderMissing;
        }
        tx.Commit();
        return GuildMatchStoreErrors.Ok;
    }

    private static void InsertSide(NpgsqlConnection conn, NpgsqlTransaction tx, long code, short joinType, GuildMatchSideRecord side)
    {
        using var cmd = new NpgsqlCommand("""
            INSERT INTO guild_match (match_code, guild_code, join_type, join_users, kill_count, death_count, get_ladder, total_ladder, guild_value)
            VALUES (@m, @g, @jt, @ju, @kc, @dc, @gl, @tl, @gv)
            """, conn, tx);
        cmd.Parameters.AddWithValue("m", code);
        cmd.Parameters.AddWithValue("g", (long)side.GuildCode);
        cmd.Parameters.AddWithValue("jt", joinType);
        cmd.Parameters.AddWithValue("ju", (short)side.JoinUsers);
        cmd.Parameters.AddWithValue("kc", (short)side.Kills);
        cmd.Parameters.AddWithValue("dc", (short)side.Deaths);
        cmd.Parameters.AddWithValue("gl", side.GetLadder);
        cmd.Parameters.AddWithValue("tl", side.TotalLadder);
        cmd.Parameters.AddWithValue("gv", side.GuildValue);
        cmd.ExecuteNonQuery();
    }

    private static bool Settle(NpgsqlConnection conn, NpgsqlTransaction tx, GuildMatchSideRecord side)
    {
        var counted = side.Outcome is GuildMatchOutcome.Win or GuildMatchOutcome.Lose or GuildMatchOutcome.Draw or GuildMatchOutcome.Disconnect;
        var point = side.Outcome == GuildMatchOutcome.Win ? side.GetLadder : 0;
        using (var cmd = new NpgsqlCommand("""
            UPDATE guild_ladder
            SET guild_ladder = @total,
                match_count = match_count + @match,
                win_count = win_count + @win,
                lose_count = lose_count + @lose,
                draw_count = draw_count + @draw,
                disc_count = disc_count + @disc
            WHERE guild_code = @g
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue("total", side.TotalLadder);
            cmd.Parameters.AddWithValue("match", (short)(counted ? 1 : 0));
            cmd.Parameters.AddWithValue("win", (short)(side.Outcome == GuildMatchOutcome.Win ? 1 : 0));
            cmd.Parameters.AddWithValue("lose", (short)(side.Outcome == GuildMatchOutcome.Lose ? 1 : 0));
            cmd.Parameters.AddWithValue("draw", (short)(side.Outcome == GuildMatchOutcome.Draw ? 1 : 0));
            cmd.Parameters.AddWithValue("disc", (short)(side.Outcome == GuildMatchOutcome.Disconnect ? 1 : 0));
            cmd.Parameters.AddWithValue("g", (long)side.GuildCode);
            if (cmd.ExecuteNonQuery() != 1) return false;
        }
        using (var cmd = new NpgsqlCommand("""
            UPDATE guild SET guild_point = guild_point + @p, guild_point_increase = guild_point_increase + @p WHERE guild_code = @g
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue("p", point);
            cmd.Parameters.AddWithValue("g", (long)side.GuildCode);
            cmd.ExecuteNonQuery();
        }
        using (var cmd = new NpgsqlCommand("""
            INSERT INTO guild_point_history (char_uid, add_guild_point, created_at, history_type, guild_code)
            VALUES (@c, @p, NOW(), @t, @g)
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue("c", MatchPointCharacter);
            cmd.Parameters.AddWithValue("p", point);
            cmd.Parameters.AddWithValue("t", MatchPointHistoryType);
            cmd.Parameters.AddWithValue("g", (long)side.GuildCode);
            cmd.ExecuteNonQuery();
        }
        return true;
    }
}
