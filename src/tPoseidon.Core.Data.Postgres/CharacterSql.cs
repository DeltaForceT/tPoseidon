namespace tPoseidon.Core.Data.Postgres;

internal static class CharacterSql
{
    public const string Job = "CASE WHEN c.job_2 = 0 THEN c.species WHEN c.job_3 = 0 THEN c.job_2 ELSE c.job_3 END";
}
