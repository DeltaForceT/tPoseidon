using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresMonsterHuntStore : IMonsterHuntStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresMonsterHuntStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public MonsterHuntRecord? Find(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            SELECT quest_id, grade, active, remaining_ms, accumulator_ms, kill_count, target_count
            FROM monster_quest_hunts
            WHERE char_uid = @char_uid
            """,
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new MonsterHuntRecord
        {
            CharacterId = characterId,
            QuestId = reader.GetInt32(0),
            Grade = reader.GetInt16(1),
            Active = reader.GetBoolean(2),
            RemainingMs = reader.GetInt32(3),
            AccumulatorMs = reader.GetInt32(4),
            Kills = reader.GetInt32(5),
            Target = reader.GetInt32(6),
        };
    }

    public void Save(MonsterHuntRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO monster_quest_hunts
                (char_uid, quest_id, grade, active, remaining_ms, accumulator_ms, kill_count, target_count, updated_at)
            VALUES
                (@char_uid, @quest_id, @grade, @active, @remaining_ms, @accumulator_ms, @kill_count, @target_count, NOW())
            ON CONFLICT (char_uid) DO UPDATE
                SET quest_id       = EXCLUDED.quest_id,
                    grade          = EXCLUDED.grade,
                    active         = EXCLUDED.active,
                    remaining_ms   = EXCLUDED.remaining_ms,
                    accumulator_ms = EXCLUDED.accumulator_ms,
                    kill_count     = EXCLUDED.kill_count,
                    target_count   = EXCLUDED.target_count,
                    updated_at     = EXCLUDED.updated_at
            """,
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)record.CharacterId));
        cmd.Parameters.AddWithValue("quest_id", record.QuestId);
        cmd.Parameters.AddWithValue("grade", (short)record.Grade);
        cmd.Parameters.AddWithValue("active", record.Active);
        cmd.Parameters.AddWithValue("remaining_ms", record.RemainingMs);
        cmd.Parameters.AddWithValue("accumulator_ms", record.AccumulatorMs);
        cmd.Parameters.AddWithValue("kill_count", record.Kills);
        cmd.Parameters.AddWithValue("target_count", record.Target);
        cmd.ExecuteNonQuery();
    }

    public bool Delete(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "DELETE FROM monster_quest_hunts WHERE char_uid = @char_uid",
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        return cmd.ExecuteNonQuery() == 1;
    }
}
