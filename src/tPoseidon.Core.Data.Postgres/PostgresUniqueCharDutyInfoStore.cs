using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresUniqueCharDutyInfoStore : IUniqueCharDutyInfoStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresUniqueCharDutyInfoStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public UniqueCharDutyInfo GetOrCreate(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO unique_char_duty_info (char_uid)
            VALUES (@char_uid)
            ON CONFLICT (char_uid) DO UPDATE SET char_uid = EXCLUDED.char_uid
            RETURNING char_uid, bossmonmedal_count, unique_item_count, duty_end_date
            """,
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        using var reader = cmd.ExecuteReader();
        reader.Read();
        return Read(reader);
    }

    public UniqueCharDutyInfo Upsert(UniqueCharDutyInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO unique_char_duty_info
                (char_uid, bossmonmedal_count, unique_item_count, duty_end_date)
            VALUES
                (@char_uid, @medal, @item, @end_at)
            ON CONFLICT (char_uid) DO UPDATE
                SET bossmonmedal_count = EXCLUDED.bossmonmedal_count,
                    unique_item_count  = EXCLUDED.unique_item_count,
                    duty_end_date        = EXCLUDED.duty_end_date
            """,
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)info.CharacterId));
        cmd.Parameters.AddWithValue("medal", info.BossMonMedalCount);
        cmd.Parameters.AddWithValue("item", info.UniqueItemCount);
        cmd.Parameters.AddWithValue("end_at", info.DutyEndAt.HasValue ? (object)info.DutyEndAt.Value.UtcDateTime : DBNull.Value);
        cmd.ExecuteNonQuery();
        return info;
    }

    public bool Delete(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "DELETE FROM unique_char_duty_info WHERE char_uid = @char_uid",
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        return cmd.ExecuteNonQuery() == 1;
    }

    private static UniqueCharDutyInfo Read(NpgsqlDataReader reader) => new()
    {
        CharacterId = unchecked((ulong)reader.GetInt64(0)),
        BossMonMedalCount = reader.GetInt16(1),
        UniqueItemCount = reader.GetInt16(2),
        DutyEndAt = reader.IsDBNull(3) ? null : new DateTimeOffset(reader.GetDateTime(3), TimeSpan.Zero),
    };
}
