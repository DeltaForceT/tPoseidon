using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresHonorTitleStore : IHonorTitleStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresHonorTitleStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public HonorTitleLogin Load(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        var titles = new List<ushort>();
        using (var list = new NpgsqlCommand(
            "SELECT title_id FROM char_honor_title WHERE char_uid = @c ORDER BY created_at, title_id", conn))
        {
            list.Parameters.AddWithValue("c", unchecked((long)characterId));
            using var reader = list.ExecuteReader();
            while (reader.Read()) titles.Add((ushort)reader.GetInt32(0));
        }
        using var profile = new NpgsqlCommand("SELECT title_type, title_id FROM char_profile WHERE char_uid = @c", conn);
        profile.Parameters.AddWithValue("c", unchecked((long)characterId));
        using var row = profile.ExecuteReader();
        return row.Read()
            ? new HonorTitleLogin(titles, (ushort)row.GetInt32(0), unchecked((uint)row.GetInt32(1)))
            : new HonorTitleLogin(titles, 0, 0);
    }

    public bool Insert(ulong characterId, IReadOnlyList<HonorTitleRecord> titles)
    {
        ArgumentNullException.ThrowIfNull(titles);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        foreach (var title in titles)
        {
            using (var insert = new NpgsqlCommand(
                "INSERT INTO char_honor_title (char_uid, title_id) VALUES (@c, @t) ON CONFLICT (char_uid, title_id) DO NOTHING", conn, tx))
            {
                insert.Parameters.AddWithValue("c", unchecked((long)characterId));
                insert.Parameters.AddWithValue("t", (int)title.TitleId);
                insert.ExecuteNonQuery();
            }
            using var log = new NpgsqlCommand("""
                INSERT INTO char_honor_title_log (char_uid, title_id, quest_id, quest_type, clear_time, hunter_count)
                VALUES (@c, @t, @q, @qt, @ct, @h)
                """, conn, tx);
            log.Parameters.AddWithValue("c", unchecked((long)characterId));
            log.Parameters.AddWithValue("t", (int)title.TitleId);
            log.Parameters.AddWithValue("q", unchecked((int)title.QuestId));
            log.Parameters.AddWithValue("qt", (short)title.QuestType);
            log.Parameters.AddWithValue("ct", (int)title.ClearTime);
            log.Parameters.AddWithValue("h", (int)title.HunterCount);
            log.ExecuteNonQuery();
        }
        tx.Commit();
        return true;
    }

    public void SetProfileTitle(ulong characterId, ushort titleType, uint titleId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("""
            INSERT INTO char_profile (char_uid, title_type, title_id, updated_at)
            VALUES (@c, @tt, @t, NOW())
            ON CONFLICT (char_uid) DO UPDATE
                SET title_type = EXCLUDED.title_type,
                    title_id   = EXCLUDED.title_id,
                    updated_at = NOW()
            """, conn);
        cmd.Parameters.AddWithValue("c", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("tt", (int)titleType);
        cmd.Parameters.AddWithValue("t", unchecked((int)titleId));
        cmd.ExecuteNonQuery();
    }
}
