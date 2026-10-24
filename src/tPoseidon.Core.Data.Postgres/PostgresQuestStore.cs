using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresQuestStore : IQuestStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresQuestStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public void AddActive(Quest quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO quests
                (char_uid, quest_type, quest_id, com_count, monster_id,
                 kill_count, record_time, accepted_at)
            VALUES
                (@char_uid, @quest_type, @quest_id, @com_count, @monster_id,
                 @kill_count, @record_time, @accepted_at)
            """,
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)quest.CharacterId));
        cmd.Parameters.AddWithValue("quest_type", (short)quest.QuestType);
        cmd.Parameters.AddWithValue("quest_id", quest.QuestId);
        cmd.Parameters.AddWithValue("com_count", (short)quest.CompletionCount);
        cmd.Parameters.AddWithValue("monster_id", quest.MonsterId);
        cmd.Parameters.AddWithValue("kill_count", quest.KillCount);
        cmd.Parameters.AddWithValue("record_time", quest.RecordTime);
        cmd.Parameters.AddWithValue("accepted_at", quest.AcceptedAt.UtcDateTime);

        try
        {
            cmd.ExecuteNonQuery();
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            throw new InvalidOperationException(
                $"Quest already active for character {quest.CharacterId}: " +
                $"type={quest.QuestType}, id={quest.QuestId}, monster={quest.MonsterId}", ex);
        }
    }

    public IReadOnlyList<Quest> GetActiveForCharacter(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            SELECT char_uid, quest_type, quest_id, com_count, monster_id,
                   kill_count, record_time, accepted_at
            FROM quests
            WHERE char_uid = @char_uid
            """,
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        using var reader = cmd.ExecuteReader();
        var result = new List<Quest>();
        while (reader.Read()) result.Add(ReadQuest(reader));
        return result;
    }

    public Quest? FindActive(ulong characterId, byte questType, int questId, int monsterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            SELECT char_uid, quest_type, quest_id, com_count, monster_id,
                   kill_count, record_time, accepted_at
            FROM quests
            WHERE char_uid = @char_uid
              AND quest_type = @quest_type
              AND quest_id = @quest_id
              AND monster_id = @monster_id
            """,
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("quest_type", (short)questType);
        cmd.Parameters.AddWithValue("quest_id", questId);
        cmd.Parameters.AddWithValue("monster_id", monsterId);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadQuest(reader) : null;
    }

    public int CountActiveOfType(ulong characterId, byte questType)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM quests WHERE char_uid = @char_uid AND quest_type = @quest_type",
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("quest_type", (short)questType);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public bool UpdateMonsterProgress(ulong characterId, int questId, int monsterId, short killCount, int recordTime)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            UPDATE quests
            SET kill_count = @kill_count, record_time = @record_time
            WHERE char_uid = @char_uid
              AND quest_type IN (2, 5)
              AND quest_id = @quest_id
              AND monster_id = @monster_id
            """,
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("quest_id", questId);
        cmd.Parameters.AddWithValue("monster_id", monsterId);
        cmd.Parameters.AddWithValue("kill_count", killCount);
        cmd.Parameters.AddWithValue("record_time", recordTime);
        return cmd.ExecuteNonQuery() == 1;
    }

    public bool RemoveActive(ulong characterId, byte questType, int questId, int monsterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            DELETE FROM quests
            WHERE char_uid = @char_uid
              AND quest_type = @quest_type
              AND quest_id = @quest_id
              AND monster_id = @monster_id
            """,
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("quest_type", (short)questType);
        cmd.Parameters.AddWithValue("quest_id", questId);
        cmd.Parameters.AddWithValue("monster_id", monsterId);
        return cmd.ExecuteNonQuery() == 1;
    }

    private static Quest ReadQuest(NpgsqlDataReader reader) => new()
    {
        CharacterId = unchecked((ulong)reader.GetInt64(0)),
        QuestType = (byte)reader.GetInt16(1),
        QuestId = reader.GetInt32(2),
        CompletionCount = (byte)reader.GetInt16(3),
        MonsterId = reader.GetInt32(4),
        KillCount = reader.GetInt16(5),
        RecordTime = reader.GetInt32(6),
        AcceptedAt = new DateTimeOffset(reader.GetDateTime(7), TimeSpan.Zero),
    };
}
