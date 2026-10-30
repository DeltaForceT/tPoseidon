using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresGmSkillSwapStore : IGmSkillSwapStore
{
    private const string LiveSkills = "skills";
    private const string SavedSkills = "gm_skill_swap_skills";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresGmSkillSwapStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public SkillSwapResult? Swap(ulong characterId, IReadOnlyList<Skill> gmSkills)
    {
        ArgumentNullException.ThrowIfNull(gmSkills);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        using (var marker = new NpgsqlCommand(
            "INSERT INTO gm_skill_swap (char_uid) VALUES (@char_uid) ON CONFLICT (char_uid) DO NOTHING", conn, tx))
        {
            marker.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
            if (marker.ExecuteNonQuery() == 0) return null;
        }
        var before = ReadSkills(conn, tx, LiveSkills, characterId);
        using (var copy = new NpgsqlCommand(
            "INSERT INTO gm_skill_swap_skills (char_uid, skill_id, skill_level) SELECT char_uid, skill_id, skill_level FROM skills WHERE char_uid = @char_uid", conn, tx))
        {
            copy.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
            copy.ExecuteNonQuery();
        }
        ReplaceSkills(conn, tx, characterId, gmSkills);
        var after = ReadSkills(conn, tx, LiveSkills, characterId);
        tx.Commit();
        return new SkillSwapResult(before, after);
    }

    public SkillSwapResult? Restore(ulong characterId, IReadOnlySet<int> gmSkillIds)
    {
        ArgumentNullException.ThrowIfNull(gmSkillIds);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        using (var lockRow = new NpgsqlCommand("SELECT 1 FROM gm_skill_swap WHERE char_uid = @char_uid FOR UPDATE", conn, tx))
        {
            lockRow.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
            if (lockRow.ExecuteScalar() is null) return null;
        }
        var saved = ReadSkills(conn, tx, SavedSkills, characterId);
        var before = ReadSkills(conn, tx, LiveSkills, characterId);
        ReplaceSkills(conn, tx, characterId, GmSkillSwapRules.Restored(saved, before, gmSkillIds));
        using (var clear = new NpgsqlCommand("DELETE FROM gm_skill_swap WHERE char_uid = @char_uid", conn, tx))
        {
            clear.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
            clear.ExecuteNonQuery();
        }
        var after = ReadSkills(conn, tx, LiveSkills, characterId);
        tx.Commit();
        return new SkillSwapResult(before, after);
    }

    private static List<Skill> ReadSkills(NpgsqlConnection conn, NpgsqlTransaction tx, string table, ulong characterId)
    {
        using var cmd = new NpgsqlCommand($"SELECT skill_id, skill_level FROM {table} WHERE char_uid = @char_uid ORDER BY skill_id", conn, tx);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        using var reader = cmd.ExecuteReader();
        var skills = new List<Skill>();
        while (reader.Read())
        {
            skills.Add(new Skill { CharacterId = characterId, SkillId = reader.GetInt32(0), SkillLevel = reader.GetInt32(1) });
        }
        return skills;
    }

    private static void ReplaceSkills(NpgsqlConnection conn, NpgsqlTransaction tx, ulong characterId, IReadOnlyList<Skill> skills)
    {
        using (var delete = new NpgsqlCommand("DELETE FROM skills WHERE char_uid = @char_uid", conn, tx))
        {
            delete.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
            delete.ExecuteNonQuery();
        }
        foreach (var skill in skills)
        {
            using var insert = new NpgsqlCommand(
                "INSERT INTO skills (char_uid, skill_id, skill_level) VALUES (@char_uid, @skill_id, @skill_level)", conn, tx);
            insert.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
            insert.Parameters.AddWithValue("skill_id", skill.SkillId);
            insert.Parameters.AddWithValue("skill_level", skill.SkillLevel);
            insert.ExecuteNonQuery();
        }
    }
}
