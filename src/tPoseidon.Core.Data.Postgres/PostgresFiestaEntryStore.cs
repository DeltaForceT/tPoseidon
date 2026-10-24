using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresFiestaEntryStore : IFiestaEntryStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresFiestaEntryStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public (byte Count, DateTime? Slot) Load(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("SELECT entry_count, slot_at FROM fiesta_entry WHERE char_uid = @c", conn);
        cmd.Parameters.AddWithValue("c", (long)characterId);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return (0, null);
        return ((byte)reader.GetInt16(0), reader.IsDBNull(1) ? null : DateTime.SpecifyKind(reader.GetDateTime(1), DateTimeKind.Unspecified));
    }

    public void Save(ulong characterId, byte count, DateTime? slot)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("""
            INSERT INTO fiesta_entry (char_uid, entry_count, slot_at) VALUES (@c, @n, @s)
            ON CONFLICT (char_uid) DO UPDATE SET entry_count = EXCLUDED.entry_count, slot_at = EXCLUDED.slot_at
            """, conn);
        cmd.Parameters.AddWithValue("c", (long)characterId);
        cmd.Parameters.AddWithValue("n", (short)count);
        cmd.Parameters.Add(new NpgsqlParameter("s", NpgsqlTypes.NpgsqlDbType.Timestamp) { Value = slot is { } at ? DateTime.SpecifyKind(at, DateTimeKind.Unspecified) : DBNull.Value });
        cmd.ExecuteNonQuery();
    }
}
