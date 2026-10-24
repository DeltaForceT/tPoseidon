using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed record PenaltyKind(short Code, string Name, short Kind);

public sealed record RunningPenalty(string AccountName, string PenaltyName, short Count, DateTimeOffset AppliedAt, int Hours, DateTimeOffset ReleaseAt, string? Note);

public static class PenaltyResults
{
    public const int Ok = 0;
    public const int NoAccount = 100;
    public const int AlreadyPenalized = 101;
    public const int Withdrawn = 102;
}

public sealed class PostgresPenaltyStore
{
    public const short PenaltyWorld = 1;
    private readonly NpgsqlDataSource _dataSource;

    public PostgresPenaltyStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public void SetKind(PenaltyKind kind)
    {
        ArgumentNullException.ThrowIfNull(kind);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            @"INSERT INTO penalty (penalty_code, penalty_name, penalty_kind)
              VALUES (@code, @name, @kind)
              ON CONFLICT (penalty_code) DO UPDATE SET penalty_name = EXCLUDED.penalty_name, penalty_kind = EXCLUDED.penalty_kind", conn);
        cmd.Parameters.AddWithValue("code", kind.Code);
        cmd.Parameters.AddWithValue("name", kind.Name);
        cmd.Parameters.AddWithValue("kind", kind.Kind);
        cmd.ExecuteNonQuery();
    }

    public void SetTime(short code, short count, int hours)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            @"INSERT INTO penalty_time (penalty_code, penalty_count, penalty_time)
              VALUES (@code, @count, @hours)
              ON CONFLICT (penalty_code, penalty_count) DO UPDATE SET penalty_time = EXCLUDED.penalty_time", conn);
        cmd.Parameters.AddWithValue("code", code);
        cmd.Parameters.AddWithValue("count", count);
        cmd.Parameters.AddWithValue("hours", hours);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<PenaltyKind> Kinds()
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "SELECT penalty_code, penalty_name, penalty_kind FROM penalty WHERE penalty_type = 1 ORDER BY penalty_code", conn);
        using var reader = cmd.ExecuteReader();
        var kinds = new List<PenaltyKind>();
        while (reader.Read())
        {
            kinds.Add(new PenaltyKind(reader.GetInt16(0), reader.GetString(1), reader.GetInt16(2)));
        }

        return kinds;
    }

    public int Penalize(string accountName, short code, long gameMasterAccountId, string note)
    {
        ArgumentException.ThrowIfNullOrEmpty(accountName);
        ArgumentNullException.ThrowIfNull(note);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "SELECT uspt_penalty_user_insert_block(@world, NULL, @account, @code, @gm, @note)", conn);
        cmd.Parameters.AddWithValue("world", PenaltyWorld);
        cmd.Parameters.AddWithValue("account", accountName);
        cmd.Parameters.AddWithValue("code", code);
        cmd.Parameters.AddWithValue("gm", gameMasterAccountId);
        cmd.Parameters.AddWithValue("note", note);
        return Convert.ToInt32(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    public int Release(string accountName, string gameMasterName)
    {
        ArgumentException.ThrowIfNullOrEmpty(accountName);
        ArgumentException.ThrowIfNullOrEmpty(gameMasterName);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            @"SELECT uspt_penalty_user_update_release_by_gm(@gm, pu.account_id, pu.release_date)
                FROM penalty_user AS pu
                JOIN accounts AS a ON a.account_id = pu.account_id
               WHERE a.username = @account AND pu.released_date IS NULL", conn);
        cmd.Parameters.AddWithValue("gm", gameMasterName);
        cmd.Parameters.AddWithValue("account", accountName);
        using var reader = cmd.ExecuteReader();
        var released = 0;
        while (reader.Read())
        {
            if (reader.GetInt32(0) == PenaltyResults.Ok)
            {
                released++;
            }
        }

        return released;
    }

    public IReadOnlyList<RunningPenalty> Running()
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            @"SELECT a.username, p.penalty_name, pu.penalty_count, pu.apply_date, pu.penalty_time, pu.release_date, pu.penalty_note
                FROM penalty_user AS pu
                JOIN accounts AS a ON a.account_id = pu.account_id
                JOIN penalty AS p ON p.penalty_code = pu.penalty_code
               WHERE pu.released_date IS NULL
               ORDER BY pu.release_date", conn);
        using var reader = cmd.ExecuteReader();
        var running = new List<RunningPenalty>();
        while (reader.Read())
        {
            running.Add(new RunningPenalty(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt16(2),
                reader.GetFieldValue<DateTimeOffset>(3),
                reader.GetInt32(4),
                reader.GetFieldValue<DateTimeOffset>(5),
                reader.IsDBNull(6) ? null : reader.GetString(6)));
        }

        return running;
    }
}
