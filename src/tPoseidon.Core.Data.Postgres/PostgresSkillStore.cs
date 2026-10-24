using tPoseidon.Core.Data;
using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresSkillStore : ISkillStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresSkillStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public IReadOnlyList<Skill> GetForCharacter(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            SELECT skill_id, skill_level, skill_updated
            FROM skills
            WHERE char_uid = @char_uid
            ORDER BY skill_id
            """,
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        using var reader = cmd.ExecuteReader();
        var result = new List<Skill>();
        while (reader.Read())
        {
            result.Add(new Skill
            {
                CharacterId = characterId,
                SkillId = reader.GetInt32(0),
                SkillLevel = reader.GetInt32(1),
                UpdatedAt = new DateTimeOffset(reader.GetDateTime(2), TimeSpan.Zero),
            });
        }
        return result;
    }

    public Skill? Find(ulong characterId, int skillId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            SELECT skill_level, skill_updated
            FROM skills
            WHERE char_uid = @char_uid AND skill_id = @skill_id
            """,
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("skill_id", skillId);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new Skill
        {
            CharacterId = characterId,
            SkillId = skillId,
            SkillLevel = reader.GetInt32(0),
            UpdatedAt = new DateTimeOffset(reader.GetDateTime(1), TimeSpan.Zero),
        };
    }

    public void Upsert(Skill skill)
    {
        ArgumentNullException.ThrowIfNull(skill);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO skills (char_uid, skill_id, skill_level, skill_updated)
            VALUES (@char_uid, @skill_id, @skill_level, NOW())
            ON CONFLICT (char_uid, skill_id) DO UPDATE
                SET skill_level   = EXCLUDED.skill_level,
                    skill_updated = EXCLUDED.skill_updated
            """,
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)skill.CharacterId));
        cmd.Parameters.AddWithValue("skill_id", skill.SkillId);
        cmd.Parameters.AddWithValue("skill_level", skill.SkillLevel);
        cmd.ExecuteNonQuery();
    }

    public int DeleteAllForCharacter(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "DELETE FROM skills WHERE char_uid = @char_uid",
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        return cmd.ExecuteNonQuery();
    }

    public int DeleteAboveLevel(ulong characterId, int maxLevel)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "DELETE FROM skills WHERE char_uid = @char_uid AND skill_level > @max_level",
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("max_level", maxLevel);
        return cmd.ExecuteNonQuery();
    }
}
