using Npgsql;
using tPoseidon.Core.Data;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresOccupationStore : IOccupationStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresOccupationStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public bool ChangeOccupation(ulong characterId, byte tier, ushort type, byte job, byte hair)
    {
        var sql = tier switch
        {
            OccupationRules.SecondJobTier =>
                "UPDATE characters SET type_2 = @t, job_2 = @j, job_2_changed = NOW(), change_job_queststate = 0, hair_2 = @h, job_state = @tier WHERE character_id = @c",
            OccupationRules.ThirdJobTier =>
                "UPDATE characters SET type_3 = @t, job_3 = @j, job_3_changed = NOW(), change_job_queststate = 0, hair_3 = @h, job_state = @tier WHERE character_id = @c",
            _ => null,
        };
        if (sql is null) return false;
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("t", (short)type);
        cmd.Parameters.AddWithValue("j", (short)job);
        cmd.Parameters.AddWithValue("h", (int)hair);
        cmd.Parameters.AddWithValue("tier", (short)tier);
        cmd.Parameters.AddWithValue("c", (long)characterId);
        return cmd.ExecuteNonQuery() == 1;
    }

    public bool SetJobState(ulong characterId, byte jobIndex)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("UPDATE characters SET job_state = @i WHERE character_id = @c", conn);
        cmd.Parameters.AddWithValue("i", (short)jobIndex);
        cmd.Parameters.AddWithValue("c", (long)characterId);
        return cmd.ExecuteNonQuery() == 1;
    }

    public bool SetChangeJobQuestState(ulong characterId, byte state)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "UPDATE characters SET change_job_queststate = @s WHERE character_id = @c AND job_3 = 0", conn);
        cmd.Parameters.AddWithValue("s", (short)state);
        cmd.Parameters.AddWithValue("c", (long)characterId);
        return cmd.ExecuteNonQuery() == 1;
    }
}
