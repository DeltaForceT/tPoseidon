using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresMemoryPortStore : IMemoryPortStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresMemoryPortStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public MemoryPort Upsert(MemoryPort entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO memory_port
                (char_uid, port_type, slot_num, zone, pos_x, pos_y, pt_desc, created_at)
            VALUES
                (@char_uid, @port_type, @slot_num, @zone, @pos_x, @pos_y, @pt_desc, NOW())
            ON CONFLICT (char_uid, port_type, slot_num) DO UPDATE
                SET zone       = EXCLUDED.zone,
                    pos_x      = EXCLUDED.pos_x,
                    pos_y      = EXCLUDED.pos_y,
                    pt_desc    = EXCLUDED.pt_desc,
                    created_at = NOW()
            RETURNING created_at
            """,
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)entry.CharacterId));
        cmd.Parameters.AddWithValue("port_type", (short)entry.PortType);
        cmd.Parameters.AddWithValue("slot_num", (short)entry.SlotNum);
        cmd.Parameters.AddWithValue("zone", entry.Zone);
        cmd.Parameters.AddWithValue("pos_x", entry.PositionX);
        cmd.Parameters.AddWithValue("pos_y", entry.PositionY);
        cmd.Parameters.AddWithValue("pt_desc", entry.Description ?? "");
        var createdAt = (DateTime)cmd.ExecuteScalar()!;
        return entry with { CreatedAt = new DateTimeOffset(createdAt, TimeSpan.Zero) };
    }

    public bool TryInsert(MemoryPort entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO memory_port
                (char_uid, port_type, slot_num, zone, pos_x, pos_y, pt_desc, created_at)
            VALUES
                (@char_uid, @port_type, @slot_num, @zone, @pos_x, @pos_y, @pt_desc, NOW())
            ON CONFLICT (char_uid, port_type, slot_num) DO NOTHING
            """,
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)entry.CharacterId));
        cmd.Parameters.AddWithValue("port_type", (short)entry.PortType);
        cmd.Parameters.AddWithValue("slot_num", (short)entry.SlotNum);
        cmd.Parameters.AddWithValue("zone", entry.Zone);
        cmd.Parameters.AddWithValue("pos_x", entry.PositionX);
        cmd.Parameters.AddWithValue("pos_y", entry.PositionY);
        cmd.Parameters.AddWithValue("pt_desc", entry.Description ?? "");
        return cmd.ExecuteNonQuery() == 1;
    }

    public MemoryPort? Find(ulong characterId, byte portType, byte slotNum)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            BaseSelectSql + " WHERE char_uid = @char_uid AND port_type = @port_type AND slot_num = @slot_num",
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("port_type", (short)portType);
        cmd.Parameters.AddWithValue("slot_num", (short)slotNum);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? Read(reader) : null;
    }

    public IReadOnlyList<MemoryPort> GetForCharacter(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            BaseSelectSql + " WHERE char_uid = @char_uid ORDER BY port_type, slot_num",
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        using var reader = cmd.ExecuteReader();
        var result = new List<MemoryPort>();
        while (reader.Read()) result.Add(Read(reader));
        return result;
    }

    public IReadOnlyList<MemoryPort> GetForCharacterByType(ulong characterId, byte portType)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            BaseSelectSql + " WHERE char_uid = @char_uid AND port_type = @port_type ORDER BY slot_num",
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("port_type", (short)portType);
        using var reader = cmd.ExecuteReader();
        var result = new List<MemoryPort>();
        while (reader.Read()) result.Add(Read(reader));
        return result;
    }

    public bool Delete(ulong characterId, byte portType, byte slotNum)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "DELETE FROM memory_port WHERE char_uid = @char_uid AND port_type = @port_type AND slot_num = @slot_num",
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("port_type", (short)portType);
        cmd.Parameters.AddWithValue("slot_num", (short)slotNum);
        return cmd.ExecuteNonQuery() == 1;
    }

    public int DeleteAllForCharacter(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "DELETE FROM memory_port WHERE char_uid = @char_uid",
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        return cmd.ExecuteNonQuery();
    }

    private const string BaseSelectSql = """
        SELECT char_uid, port_type, slot_num, zone, pos_x, pos_y, pt_desc, created_at
        FROM memory_port
        """;

    private static MemoryPort Read(NpgsqlDataReader reader) => new()
    {
        CharacterId = unchecked((ulong)reader.GetInt64(0)),
        PortType = (byte)reader.GetInt16(1),
        SlotNum = (byte)reader.GetInt16(2),
        Zone = reader.GetInt16(3),
        PositionX = reader.GetInt32(4),
        PositionY = reader.GetInt32(5),
        Description = reader.GetString(6),
        CreatedAt = new DateTimeOffset(reader.GetDateTime(7), TimeSpan.Zero),
    };
}
