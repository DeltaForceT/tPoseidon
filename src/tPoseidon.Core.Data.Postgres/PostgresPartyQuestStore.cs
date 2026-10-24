using Npgsql;
using tPoseidon.Core.Data;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresPartyQuestStore : IPartyQuestStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresPartyQuestStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public ulong Create(ushort questId, ushort monsterId, ushort targetCount)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "INSERT INTO party_quest (quest_id, monster_id, target_count) VALUES (@q, @m, @t) RETURNING party_uid", conn);
        cmd.Parameters.AddWithValue("q", (short)questId);
        cmd.Parameters.AddWithValue("m", (short)monsterId);
        cmd.Parameters.AddWithValue("t", (short)targetCount);
        return (ulong)(long)cmd.ExecuteScalar()!;
    }

    public IReadOnlyDictionary<ulong, int> CompletionCounts(ushort questId, IReadOnlyCollection<ulong> characterIds)
    {
        ArgumentNullException.ThrowIfNull(characterIds);
        var counts = new Dictionary<ulong, int>();
        if (characterIds.Count == 0) return counts;
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "SELECT char_uid, com_count FROM quest_history WHERE quest_type = @type AND quest_id = @q AND char_uid = ANY(@ids)", conn);
        cmd.Parameters.AddWithValue("type", (short)PartyQuestRules.HistoryQuestType);
        cmd.Parameters.AddWithValue("q", (int)questId);
        cmd.Parameters.AddWithValue("ids", characterIds.Select(id => (long)id).ToArray());
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) counts[(ulong)reader.GetInt64(0)] = reader.GetInt16(1);
        return counts;
    }

    public PartyQuestOutcome Complete(ulong characterId, ulong partyUid, ushort questId, ushort killCount, int recordTime, byte memberCount)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        using (var update = new NpgsqlCommand(
            "UPDATE party_quest SET record_time = @r, member_count = @n WHERE party_uid = @p", conn, tx))
        {
            update.Parameters.AddWithValue("r", recordTime);
            update.Parameters.AddWithValue("n", (short)memberCount);
            update.Parameters.AddWithValue("p", (long)partyUid);
            if (update.ExecuteNonQuery() == 0) return new PartyQuestOutcome(PartyQuestError.PartyQuestWrite, 0);
        }
        int comCount;
        using (var history = new NpgsqlCommand(
            "SELECT com_count FROM quest_history WHERE char_uid = @c AND quest_id = @q AND monster_id = 0 LIMIT 1", conn, tx))
        {
            history.Parameters.AddWithValue("c", (long)characterId);
            history.Parameters.AddWithValue("q", (int)questId);
            comCount = history.ExecuteScalar() is short previous
                ? (previous == PartyQuestRules.MostCompletions ? PartyQuestRules.MostCompletions : previous + 1)
                : 1;
        }
        using (var member = new NpgsqlCommand(
            "INSERT INTO party_quest_member (party_uid, char_uid, char_level, char_job, kill_count, com_count) " +
            $"SELECT @p, c.character_id, c.level, {CharacterSql.Job}, @k, @cc FROM characters c " +
            "WHERE c.character_id = @c AND c.char_authority <> @deleted", conn, tx))
        {
            member.Parameters.AddWithValue("p", (long)partyUid);
            member.Parameters.AddWithValue("c", (long)characterId);
            member.Parameters.AddWithValue("k", (short)killCount);
            member.Parameters.AddWithValue("cc", comCount);
            member.Parameters.AddWithValue("deleted", (short)PartyQuestRules.DeletedAuthority);
            if (member.ExecuteNonQuery() != 1) return new PartyQuestOutcome(PartyQuestError.MemberOrHistoryWrite, 0);
        }
        using (var active = new NpgsqlCommand(
            "INSERT INTO quests (char_uid, quest_type, quest_id, com_count, monster_id, kill_count, record_time) " +
            "VALUES (@c, @type, @q, @cc, 0, 0, 0) ON CONFLICT DO NOTHING", conn, tx))
        {
            active.Parameters.AddWithValue("c", (long)characterId);
            active.Parameters.AddWithValue("type", (short)PartyQuestRules.HistoryQuestType);
            active.Parameters.AddWithValue("q", (int)questId);
            active.Parameters.AddWithValue("cc", (short)comCount);
            if (active.ExecuteNonQuery() != 1) return new PartyQuestOutcome(PartyQuestError.NotFoundOrAlreadyRewarded, 0);
        }
        tx.Commit();
        return new PartyQuestOutcome(PartyQuestError.None, comCount);
    }

    public IReadOnlyList<PartyQuestRecord> ListUnrewarded(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "SELECT a.party_uid, b.char_uid, a.quest_id, a.monster_id, b.kill_count, a.target_count, a.record_time, a.member_count, b.com_count " +
            "FROM party_quest a JOIN party_quest_member b ON a.party_uid = b.party_uid " +
            "WHERE b.char_uid = @c AND b.reward_date IS NULL", conn);
        cmd.Parameters.AddWithValue("c", (long)characterId);
        using var reader = cmd.ExecuteReader();
        var records = new List<PartyQuestRecord>();
        while (reader.Read())
        {
            records.Add(new PartyQuestRecord(
                (ulong)reader.GetInt64(0), (ulong)reader.GetInt64(1), (ushort)reader.GetInt16(2), (ushort)reader.GetInt16(3),
                (ushort)reader.GetInt16(4), (ushort)reader.GetInt16(5), reader.GetInt32(6), (byte)reader.GetInt16(7), reader.GetInt32(8)));
        }
        return records;
    }

    public PartyQuestOutcome Reward(ulong characterId, ushort questId, ulong partyUid)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        short monsterId;
        int recordTime;
        int comCount;
        short killCount;
        using (var read = new NpgsqlCommand(
            "SELECT pq.monster_id, pq.record_time, pm.com_count, pm.kill_count FROM party_quest pq " +
            "JOIN party_quest_member pm ON pq.party_uid = pm.party_uid " +
            "WHERE pq.party_uid = @p AND pm.char_uid = @c AND pm.reward_date IS NULL", conn, tx))
        {
            read.Parameters.AddWithValue("p", (long)partyUid);
            read.Parameters.AddWithValue("c", (long)characterId);
            using var reader = read.ExecuteReader();
            if (!reader.Read()) return new PartyQuestOutcome(PartyQuestError.NotFoundOrAlreadyRewarded, 0);
            monsterId = reader.GetInt16(0);
            recordTime = reader.GetInt32(1);
            comCount = reader.GetInt32(2);
            killCount = reader.GetInt16(3);
        }
        if (comCount == 1)
        {
            using var insert = new NpgsqlCommand(
                "INSERT INTO quest_history (char_uid, quest_type, quest_id, com_count, monster_id, kill_count, record_time, quest_completed_date) " +
                "VALUES (@c, @type, @q, @cc, 0, 0, 0, NOW())", conn, tx);
            insert.Parameters.AddWithValue("c", (long)characterId);
            insert.Parameters.AddWithValue("type", (short)PartyQuestRules.HistoryQuestType);
            insert.Parameters.AddWithValue("q", (int)questId);
            insert.Parameters.AddWithValue("cc", (short)comCount);
            insert.ExecuteNonQuery();
        }
        else if (comCount > 1)
        {
            using var update = new NpgsqlCommand(
                "UPDATE quest_history SET com_count = @cc, quest_completed_date = NOW() " +
                "WHERE char_uid = @c AND quest_id = @q AND monster_id = 0 AND quest_type = @type", conn, tx);
            update.Parameters.AddWithValue("c", (long)characterId);
            update.Parameters.AddWithValue("type", (short)PartyQuestRules.HistoryQuestType);
            update.Parameters.AddWithValue("q", (int)questId);
            update.Parameters.AddWithValue("cc", (short)comCount);
            if (update.ExecuteNonQuery() == 0) return new PartyQuestOutcome(PartyQuestError.MemberOrHistoryWrite, 0);
        }
        else
        {
            return new PartyQuestOutcome(PartyQuestError.NotFoundOrAlreadyRewarded, 0);
        }
        using (var remove = new NpgsqlCommand(
            "DELETE FROM quests WHERE char_uid = @c AND quest_id = @q AND monster_id = 0", conn, tx))
        {
            remove.Parameters.AddWithValue("c", (long)characterId);
            remove.Parameters.AddWithValue("q", (int)questId);
            if (remove.ExecuteNonQuery() == 0) return new PartyQuestOutcome(PartyQuestError.ActiveQuestRemove, 0);
        }
        using (var log = new NpgsqlCommand(
            "INSERT INTO quest_history_log (char_uid, quest_type, quest_id, com_count, monster_id, kill_count, record_time) " +
            "VALUES (@c, @type, @q, @cc, @m, @k, @r)", conn, tx))
        {
            log.Parameters.AddWithValue("c", (long)characterId);
            log.Parameters.AddWithValue("type", (short)PartyQuestRules.HistoryQuestType);
            log.Parameters.AddWithValue("q", (int)questId);
            log.Parameters.AddWithValue("cc", (short)comCount);
            log.Parameters.AddWithValue("m", (int)monsterId);
            log.Parameters.AddWithValue("k", killCount);
            log.Parameters.AddWithValue("r", recordTime);
            log.ExecuteNonQuery();
        }
        using (var guild = new NpgsqlCommand(
            "WITH member AS (SELECT guild_code FROM guild_member WHERE char_uid = @c), " +
            "bumped AS (UPDATE guild SET guild_point = guild_point + @pt, guild_point_increase = guild_point_increase + @pt " +
            "WHERE guild_code IN (SELECT guild_code FROM member) RETURNING guild_code) " +
            "INSERT INTO guild_point_history (char_uid, add_guild_point, history_type, guild_code) " +
            "SELECT @c, @pt, @ht, guild_code FROM bumped", conn, tx))
        {
            guild.Parameters.AddWithValue("c", (long)characterId);
            guild.Parameters.AddWithValue("pt", PartyQuestRules.GuildPointPerReward);
            guild.Parameters.AddWithValue("ht", (short)PartyQuestRules.GuildPointHistoryType);
            guild.ExecuteNonQuery();
        }
        using (var stamp = new NpgsqlCommand(
            "UPDATE party_quest_member SET reward_date = NOW() WHERE party_uid = @p AND char_uid = @c AND reward_date IS NULL", conn, tx))
        {
            stamp.Parameters.AddWithValue("p", (long)partyUid);
            stamp.Parameters.AddWithValue("c", (long)characterId);
            if (stamp.ExecuteNonQuery() == 0) return new PartyQuestOutcome(PartyQuestError.RewardStamp, 0);
        }
        tx.Commit();
        return new PartyQuestOutcome(PartyQuestError.None, comCount);
    }
}
