using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresImprintStore : IImprintStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresImprintStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public IReadOnlyList<ImprintRecord> Load(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "SELECT imprint_type, item_id, expires_at FROM imprint_active WHERE char_uid = @char_uid ORDER BY imprint_type",
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        using var reader = cmd.ExecuteReader();
        var records = new List<ImprintRecord>();
        while (reader.Read())
        {
            records.Add(new ImprintRecord(characterId, reader.GetInt16(0), reader.GetInt32(1), new DateTimeOffset(reader.GetDateTime(2), TimeSpan.Zero)));
        }
        return records;
    }

    public void Save(ImprintRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO imprint_active (char_uid, imprint_type, item_id, expires_at)
            VALUES (@char_uid, @type, @item_id, @expires_at)
            ON CONFLICT (char_uid, imprint_type) DO UPDATE
                SET item_id = EXCLUDED.item_id, expires_at = EXCLUDED.expires_at
            """,
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)record.CharacterId));
        cmd.Parameters.AddWithValue("type", (short)record.Type);
        cmd.Parameters.AddWithValue("item_id", record.ItemId);
        cmd.Parameters.AddWithValue("expires_at", record.ExpiresAt.UtcDateTime);
        cmd.ExecuteNonQuery();
    }
}
