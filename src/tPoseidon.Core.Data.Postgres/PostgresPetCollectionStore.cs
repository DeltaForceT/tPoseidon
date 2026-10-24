using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresPetCollectionStore : IPetCollectionStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresPetCollectionStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public IReadOnlyList<PetCollectedItem> Load(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("""
            SELECT collected_uid, item_id, item_class, item_count FROM pet_collection_item
            WHERE char_uid = @c ORDER BY collected_uid
            """, conn);
        cmd.Parameters.AddWithValue("c", (long)characterId);
        using var reader = cmd.ExecuteReader();
        var items = new List<PetCollectedItem>();
        while (reader.Read()) items.Add(Read(reader));
        return items;
    }

    public PetCollectedItem Add(ulong characterId, int itemId, byte itemClass, ushort count)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("""
            INSERT INTO pet_collection_item (char_uid, item_id, item_class, item_count) VALUES (@c, @i, @k, @n)
            RETURNING collected_uid, item_id, item_class, item_count
            """, conn);
        cmd.Parameters.AddWithValue("c", (long)characterId);
        cmd.Parameters.AddWithValue("i", itemId);
        cmd.Parameters.AddWithValue("k", (short)itemClass);
        cmd.Parameters.AddWithValue("n", (int)count);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) throw new InvalidOperationException($"pet_collection_item insert for character {characterId} returned no row");
        return Read(reader);
    }

    public bool Remove(ulong characterId, ulong collectedUid)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("DELETE FROM pet_collection_item WHERE collected_uid = @u AND char_uid = @c", conn);
        cmd.Parameters.AddWithValue("u", (long)collectedUid);
        cmd.Parameters.AddWithValue("c", (long)characterId);
        return cmd.ExecuteNonQuery() > 0;
    }

    private static PetCollectedItem Read(NpgsqlDataReader reader) => new(
        (ulong)reader.GetInt64(0),
        reader.GetInt32(1),
        (byte)reader.GetInt16(2),
        (ushort)Math.Clamp(reader.GetInt32(3), 0, ushort.MaxValue));
}
