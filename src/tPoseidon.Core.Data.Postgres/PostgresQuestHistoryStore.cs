using tPoseidon.Core.Data;
using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresQuestHistoryStore : IQuestHistoryStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresQuestHistoryStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public QuestHistory? Find(ulong characterId, byte questType, int questId, int monsterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            SELECT char_uid, quest_type, quest_id, com_count, monster_id,
                   kill_count, record_time, quest_completed_date
            FROM quest_history
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
        return reader.Read() ? ReadHistory(reader) : null;
    }

    public void Insert(QuestHistory row)
    {
        ArgumentNullException.ThrowIfNull(row);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO quest_history
                (char_uid, quest_type, quest_id, com_count, monster_id,
                 kill_count, record_time, quest_completed_date)
            VALUES
                (@char_uid, @quest_type, @quest_id, @com_count, @monster_id,
                 @kill_count, @record_time, @completed_at)
            """,
            conn);
        BindHistoryParameters(cmd, row);
        try
        {
            cmd.ExecuteNonQuery();
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            throw new InvalidOperationException(
                $"Quest history row already exists: char={row.CharacterId}, " +
                $"type={row.QuestType}, id={row.QuestId}, monster={row.MonsterId}", ex);
        }
    }

    public bool Update(QuestHistory row)
    {
        ArgumentNullException.ThrowIfNull(row);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            UPDATE quest_history
            SET com_count = @com_count,
                kill_count = @kill_count,
                record_time = @record_time,
                quest_completed_date = @completed_at
            WHERE char_uid = @char_uid
              AND quest_type = @quest_type
              AND quest_id = @quest_id
              AND monster_id = @monster_id
            """,
            conn);
        BindHistoryParameters(cmd, row);
        return cmd.ExecuteNonQuery() == 1;
    }

    public IReadOnlyList<QuestHistory> GetForCharacter(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            SELECT char_uid, quest_type, quest_id, com_count, monster_id,
                   kill_count, record_time, quest_completed_date
            FROM quest_history
            WHERE char_uid = @char_uid
            ORDER BY quest_completed_date DESC
            """,
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        using var reader = cmd.ExecuteReader();
        var result = new List<QuestHistory>();
        while (reader.Read()) result.Add(ReadHistory(reader));
        return result;
    }

    public void AppendLog(QuestHistory row)
    {
        ArgumentNullException.ThrowIfNull(row);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO quest_history_log
                (char_uid, quest_type, quest_id, com_count, monster_id,
                 kill_count, record_time, quest_completed_date)
            VALUES
                (@char_uid, @quest_type, @quest_id, @com_count, @monster_id,
                 @kill_count, @record_time, @completed_at)
            """,
            conn);
        BindHistoryParameters(cmd, row);
        cmd.ExecuteNonQuery();
    }

    public bool CompletedToday(ulong characterId, byte questType, int questId, int monsterId, DateTime referenceUtc)
    {

        

        

        
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            SELECT 1
            FROM quest_history
            WHERE char_uid = @char_uid
              AND quest_type = @quest_type
              AND quest_id = @quest_id
              AND monster_id = @monster_id
              AND DATE(quest_completed_date AT TIME ZONE 'UTC') = @ref_date
            """,
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("quest_type", (short)questType);
        cmd.Parameters.AddWithValue("quest_id", questId);
        cmd.Parameters.AddWithValue("monster_id", monsterId);
        cmd.Parameters.AddWithValue("ref_date", referenceUtc.Date);
        using var reader = cmd.ExecuteReader();
        return reader.Read();
    }

    public bool HasCompletedAtLeast(ulong characterId, byte questType, int questId, int monsterId, byte threshold)
    {

        
        if (threshold == 0) return true;
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            SELECT 1
            FROM quest_history
            WHERE char_uid = @char_uid
              AND quest_type = @quest_type
              AND quest_id = @quest_id
              AND monster_id = @monster_id
              AND com_count >= @threshold
            """,
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("quest_type", (short)questType);
        cmd.Parameters.AddWithValue("quest_id", questId);
        cmd.Parameters.AddWithValue("monster_id", monsterId);
        cmd.Parameters.AddWithValue("threshold", (short)threshold);
        using var reader = cmd.ExecuteReader();
        return reader.Read();
    }

    private static void BindHistoryParameters(NpgsqlCommand cmd, QuestHistory row)
    {
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)row.CharacterId));
        cmd.Parameters.AddWithValue("quest_type", (short)row.QuestType);
        cmd.Parameters.AddWithValue("quest_id", row.QuestId);
        cmd.Parameters.AddWithValue("com_count", (short)row.CompletionCount);
        cmd.Parameters.AddWithValue("monster_id", row.MonsterId);
        cmd.Parameters.AddWithValue("kill_count", row.KillCount);
        cmd.Parameters.AddWithValue("record_time", row.RecordTime);
        cmd.Parameters.AddWithValue("completed_at", row.CompletedAt.UtcDateTime);
    }

    private static QuestHistory ReadHistory(NpgsqlDataReader reader) => new()
    {
        CharacterId = unchecked((ulong)reader.GetInt64(0)),
        QuestType = (byte)reader.GetInt16(1),
        QuestId = reader.GetInt32(2),
        CompletionCount = (byte)reader.GetInt16(3),
        MonsterId = reader.GetInt32(4),
        KillCount = reader.GetInt16(5),
        RecordTime = reader.GetInt32(6),
        CompletedAt = new DateTimeOffset(reader.GetDateTime(7), TimeSpan.Zero),
    };
}
