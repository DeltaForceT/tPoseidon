using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresAngelStore : IAngelStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresAngelStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public IReadOnlyList<AngelRelation> Angels(ulong newbieCharacterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("""
            SELECT a.angel_char_uid, c.name FROM angel_info a JOIN characters c ON c.character_id = a.angel_char_uid
            WHERE a.newbie_char_uid = @n ORDER BY a.angel_char_uid
            """, conn);
        cmd.Parameters.AddWithValue("n", (long)newbieCharacterId);
        using var reader = cmd.ExecuteReader();
        var angels = new List<AngelRelation>();
        while (reader.Read()) angels.Add(new AngelRelation((ulong)reader.GetInt64(0), reader.GetString(1)));
        return angels;
    }

    public AngelResult? Result(ulong angelCharacterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("SELECT graduation_count, latest_graduation_date FROM angel_result WHERE angel_char_uid = @a", conn);
        cmd.Parameters.AddWithValue("a", (long)angelCharacterId);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new AngelResult(reader.GetInt32(0), new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(1), DateTimeKind.Utc)));
    }

    public AngelGraduation Graduate(ulong newbieCharacterId, uint newbieAccountId, DateTimeOffset at)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        var angels = new List<ulong>();
        using (var select = new NpgsqlCommand("SELECT angel_char_uid FROM angel_info WHERE newbie_char_uid = @n ORDER BY angel_char_uid", conn, tx))
        {
            select.Parameters.AddWithValue("n", (long)newbieCharacterId);
            using var reader = select.ExecuteReader();
            while (reader.Read()) angels.Add((ulong)reader.GetInt64(0));
        }
        if (angels.Count == 0) return new AngelGraduation(false, angels, 0);
        int gifts;
        using (var gift = new NpgsqlCommand("""
            INSERT INTO charged_item (item_class, account_id, item_index, item_attr, item_use_time, item_flag, sell_code, sell_type)
            SELECT item_class, @a, item_index, item_attr, item_use_time, item_flag, sell_code, 0 FROM angel_levelup_item_info
            """, conn, tx))
        {
            gift.Parameters.AddWithValue("a", (long)newbieAccountId);
            gifts = gift.ExecuteNonQuery();
        }
        if (gifts == 0)
        {
            tx.Rollback();
            return new AngelGraduation(false, angels, 0);
        }
        using (var result = new NpgsqlCommand("""
            INSERT INTO angel_result (angel_char_uid, graduation_count, latest_graduation_date)
            SELECT angel_char_uid, 1, @at FROM angel_info WHERE newbie_char_uid = @n
            ON CONFLICT (angel_char_uid) DO UPDATE
                SET graduation_count = angel_result.graduation_count + 1, latest_graduation_date = EXCLUDED.latest_graduation_date
            """, conn, tx))
        {
            result.Parameters.AddWithValue("n", (long)newbieCharacterId);
            result.Parameters.AddWithValue("at", at);
            result.ExecuteNonQuery();
        }
        using (var log = new NpgsqlCommand("""
            INSERT INTO angel_result_log (newbie_char_uid, angel_char_uid, created_at)
            SELECT newbie_char_uid, angel_char_uid, @at FROM angel_info WHERE newbie_char_uid = @n
            """, conn, tx))
        {
            log.Parameters.AddWithValue("n", (long)newbieCharacterId);
            log.Parameters.AddWithValue("at", at);
            log.ExecuteNonQuery();
        }
        using (var delete = new NpgsqlCommand("DELETE FROM angel_info WHERE newbie_char_uid = @n", conn, tx))
        {
            delete.Parameters.AddWithValue("n", (long)newbieCharacterId);
            delete.ExecuteNonQuery();
        }
        tx.Commit();
        return new AngelGraduation(true, angels, gifts);
    }

    public AngelStoreError Add(ulong newbieCharacterId, ulong angelCharacterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        using (var insert = new NpgsqlCommand("""
            INSERT INTO angel_info (newbie_char_uid, angel_char_uid, created_at) VALUES (@n, @a, NOW())
            ON CONFLICT DO NOTHING
            """, conn, tx))
        {
            insert.Parameters.AddWithValue("n", (long)newbieCharacterId);
            insert.Parameters.AddWithValue("a", (long)angelCharacterId);
            if (insert.ExecuteNonQuery() != 1) return AngelStoreError.AlreadyListed;
        }
        using (var result = new NpgsqlCommand("INSERT INTO angel_result (angel_char_uid) VALUES (@a) ON CONFLICT DO NOTHING", conn, tx))
        {
            result.Parameters.AddWithValue("a", (long)angelCharacterId);
            result.ExecuteNonQuery();
        }
        tx.Commit();
        return AngelStoreError.None;
    }

    public bool Remove(ulong newbieCharacterId, ulong angelCharacterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("DELETE FROM angel_info WHERE newbie_char_uid = @n AND angel_char_uid = @a", conn);
        cmd.Parameters.AddWithValue("n", (long)newbieCharacterId);
        cmd.Parameters.AddWithValue("a", (long)angelCharacterId);
        return cmd.ExecuteNonQuery() == 1;
    }
}
