using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresItemPropertyStore : IItemPropertyStore, IItemRefineStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresItemPropertyStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public IReadOnlyList<ItemProperty> ForItem(ulong itemUid)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "SELECT prop_type, prop_id, prop_value, prop_extra FROM item_option WHERE item_uid = @item_uid ORDER BY position",
            conn);
        cmd.Parameters.AddWithValue("item_uid", unchecked((long)itemUid));
        using var reader = cmd.ExecuteReader();
        var result = new List<ItemProperty>();
        while (reader.Read())
        {
            result.Add(new ItemProperty(reader.GetInt16(0), reader.GetInt32(1), reader.GetInt16(2), unchecked((uint)reader.GetInt64(3))));
        }
        return result;
    }

    public void Replace(ulong itemUid, IReadOnlyList<ItemProperty> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        using (var delete = new NpgsqlCommand("DELETE FROM item_option WHERE item_uid = @item_uid", conn, tx))
        {
            delete.Parameters.AddWithValue("item_uid", unchecked((long)itemUid));
            delete.ExecuteNonQuery();
        }
        for (var position = 0; position < properties.Count; position++)
        {
            var property = properties[position];
            using var insert = new NpgsqlCommand(
                """
                INSERT INTO item_option (item_uid, position, prop_type, prop_id, prop_value, prop_extra)
                VALUES (@item_uid, @position, @prop_type, @prop_id, @prop_value, @prop_extra)
                """,
                conn, tx);
            insert.Parameters.AddWithValue("item_uid", unchecked((long)itemUid));
            insert.Parameters.AddWithValue("position", (short)position);
            insert.Parameters.AddWithValue("prop_type", (short)property.Type);
            insert.Parameters.AddWithValue("prop_id", property.Id);
            insert.Parameters.AddWithValue("prop_value", property.Value);
            insert.Parameters.AddWithValue("prop_extra", (long)property.Extra);
            insert.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public void SetRefine(ulong itemUid, byte level, byte state)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "UPDATE items SET refine_level = @refine_level, refine_state = @refine_state WHERE item_uid = @item_uid",
            conn);
        cmd.Parameters.AddWithValue("item_uid", unchecked((long)itemUid));
        cmd.Parameters.AddWithValue("refine_level", (short)level);
        cmd.Parameters.AddWithValue("refine_state", (short)state);
        cmd.ExecuteNonQuery();
    }

    public void SetMixSlot(ulong itemUid, byte slots)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("UPDATE items SET item_slot = @item_slot WHERE item_uid = @item_uid", conn);
        cmd.Parameters.AddWithValue("item_uid", unchecked((long)itemUid));
        cmd.Parameters.AddWithValue("item_slot", (short)slots);
        cmd.ExecuteNonQuery();
    }

    public void Delete(ulong itemUid)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("DELETE FROM item_option WHERE item_uid = @item_uid", conn);
        cmd.Parameters.AddWithValue("item_uid", unchecked((long)itemUid));
        cmd.ExecuteNonQuery();
    }
}
