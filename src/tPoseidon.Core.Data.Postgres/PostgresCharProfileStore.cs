using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresCharProfileStore : ICharProfileStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresCharProfileStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public CharProfile GetOrCreate(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO char_profile (char_uid)
            VALUES (@char_uid)
            ON CONFLICT (char_uid) DO UPDATE SET char_uid = EXCLUDED.char_uid
            RETURNING char_uid, public_flag_bitmask, self_intro, title_type, title_id, updated_at
            """,
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        using var reader = cmd.ExecuteReader();
        reader.Read();
        return Read(reader);
    }

    public CharProfile Upsert(CharProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO char_profile
                (char_uid, public_flag_bitmask, self_intro, title_type, title_id, updated_at)
            VALUES
                (@char_uid, @public, @intro, @ttype, @tid, NOW())
            ON CONFLICT (char_uid) DO UPDATE
                SET public_flag_bitmask = EXCLUDED.public_flag_bitmask,
                    self_intro          = EXCLUDED.self_intro,
                    title_type          = EXCLUDED.title_type,
                    title_id            = EXCLUDED.title_id,
                    updated_at          = NOW()
            RETURNING char_uid, public_flag_bitmask, self_intro, title_type, title_id, updated_at
            """,
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)profile.CharacterId));
        cmd.Parameters.AddWithValue("public", profile.PublicFlagBitmask);
        cmd.Parameters.AddWithValue("intro", profile.SelfIntro ?? "");
        cmd.Parameters.AddWithValue("ttype", profile.TitleType);
        cmd.Parameters.AddWithValue("tid", profile.TitleId);
        using var reader = cmd.ExecuteReader();
        reader.Read();
        return Read(reader);
    }

    public bool Delete(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "DELETE FROM char_profile WHERE char_uid = @char_uid",
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        return cmd.ExecuteNonQuery() == 1;
    }

    private static CharProfile Read(NpgsqlDataReader reader) => new()
    {
        CharacterId = unchecked((ulong)reader.GetInt64(0)),
        PublicFlagBitmask = reader.GetInt32(1),
        SelfIntro = reader.GetString(2),
        TitleType = reader.GetInt32(3),
        TitleId = reader.GetInt32(4),
        UpdatedAt = new DateTimeOffset(reader.GetDateTime(5), TimeSpan.Zero),
    };
}
