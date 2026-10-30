using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresShadowBonusStore : IShadowBonusStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresShadowBonusStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public ShadowBonusRecord? Load(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("SELECT reward_id, expires_at FROM shadow_bonus WHERE char_uid = @char_uid", conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        using var reader = cmd.ExecuteReader();
        return reader.Read()
            ? new ShadowBonusRecord(characterId, reader.GetInt32(0), new DateTimeOffset(reader.GetDateTime(1), TimeSpan.Zero))
            : null;
    }

    public void Save(ShadowBonusRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO shadow_bonus (char_uid, reward_id, expires_at)
            VALUES (@char_uid, @reward_id, @expires_at)
            ON CONFLICT (char_uid) DO UPDATE
                SET reward_id = EXCLUDED.reward_id, expires_at = EXCLUDED.expires_at
            """,
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)record.CharacterId));
        cmd.Parameters.AddWithValue("reward_id", record.RewardId);
        cmd.Parameters.AddWithValue("expires_at", record.ExpiresAt.UtcDateTime);
        cmd.ExecuteNonQuery();
    }
}
