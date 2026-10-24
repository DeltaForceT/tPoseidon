using tPoseidon.Core.Data;
using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresItemStore : IItemStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresItemStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public Item GrantToCharacter(
        ulong characterId,
        int itemIndex,
        byte itemClass,
        byte slotKind,
        byte slotPosition,
        ushort stack = 1)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();

        
        if (slotKind == SlotKind.Equipment)
        {
            using var checkCmd = new NpgsqlCommand(
                "SELECT 1 FROM item_char WHERE char_uid = @char_uid " +
                "AND item_where = @item_where AND item_pos = @item_pos LIMIT 1",
                conn, tx);
            checkCmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
            checkCmd.Parameters.AddWithValue("item_where", (short)slotKind);
            checkCmd.Parameters.AddWithValue("item_pos", (short)slotPosition);
            if (checkCmd.ExecuteScalar() is not null)
            {
                throw new InvalidOperationException(
                    $"Equipment slot ({slotKind}, {slotPosition}) is already occupied for char {characterId}.");
            }
        }

        long newUid;
        using (var insertItemCmd = new NpgsqlCommand(
            """
            INSERT INTO items
                (item_index, item_class, item_attr, item_charged, item_breaked,
                 refine_type, refine_level, refine_state, item_slot, item_stack)
            VALUES
                (@item_index, @item_class, 0, 0, '2020-01-01',
                 0, 0, 0, 0, @item_stack)
            RETURNING item_uid
            """,
            conn, tx))
        {
            insertItemCmd.Parameters.AddWithValue("item_index", itemIndex);
            insertItemCmd.Parameters.AddWithValue("item_class", (short)itemClass);
            insertItemCmd.Parameters.AddWithValue("item_stack", (int)stack);
            newUid = (long)insertItemCmd.ExecuteScalar()!;
        }

        using (var insertOwnCmd = new NpgsqlCommand(
            """
            INSERT INTO item_char (char_uid, item_uid, item_where, item_pos)
            VALUES (@char_uid, @item_uid, @item_where, @item_pos)
            """,
            conn, tx))
        {
            insertOwnCmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
            insertOwnCmd.Parameters.AddWithValue("item_uid", newUid);
            insertOwnCmd.Parameters.AddWithValue("item_where", (short)slotKind);
            insertOwnCmd.Parameters.AddWithValue("item_pos", (short)slotPosition);
            insertOwnCmd.ExecuteNonQuery();
        }

        tx.Commit();

        return new Item
        {
            ItemUid = unchecked((ulong)newUid),
            ItemIndex = itemIndex,
            ItemClass = itemClass,
            Stack = stack,
            CharacterId = characterId,
            SlotKind = slotKind,
            SlotPosition = slotPosition,
        };
    }

    public IReadOnlyList<Item> GetAllForCharacter(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            BaseSelectSql + " WHERE c.char_uid = @char_uid " +
            "ORDER BY c.item_where ASC, c.item_pos ASC",
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        using var reader = cmd.ExecuteReader();
        var result = new List<Item>();
        while (reader.Read()) result.Add(ReadItem(reader));
        return result;
    }

    public IReadOnlyList<Item> GetForCharacterBySlotKind(ulong characterId, byte slotKind)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            BaseSelectSql + " WHERE c.char_uid = @char_uid AND c.item_where = @item_where " +
            "ORDER BY c.item_pos ASC",
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("item_where", (short)slotKind);
        using var reader = cmd.ExecuteReader();
        var result = new List<Item>();
        while (reader.Read()) result.Add(ReadItem(reader));
        return result;
    }

    public Item? FindByUid(ulong itemUid)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            BaseSelectSql + " WHERE i.item_uid = @item_uid",
            conn);
        cmd.Parameters.AddWithValue("item_uid", unchecked((long)itemUid));
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadItem(reader) : null;
    }

    public Item? FindBySlot(ulong characterId, byte slotKind, byte slotPosition)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            BaseSelectSql + " WHERE c.char_uid = @char_uid " +
            "AND c.item_where = @item_where AND c.item_pos = @item_pos " +
            "LIMIT 1",
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("item_where", (short)slotKind);
        cmd.Parameters.AddWithValue("item_pos", (short)slotPosition);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadItem(reader) : null;
    }

    public bool MoveToSlot(ulong itemUid, byte newSlotKind, byte newSlotPosition)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();

        
        long charUid;
        using (var lookupCmd = new NpgsqlCommand(
            "SELECT char_uid FROM item_char WHERE item_uid = @item_uid",
            conn, tx))
        {
            lookupCmd.Parameters.AddWithValue("item_uid", unchecked((long)itemUid));
            var owner = lookupCmd.ExecuteScalar();
            if (owner is null) return false;
            charUid = (long)owner;
        }

        if (newSlotKind == SlotKind.Equipment)
        {
            using var checkCmd = new NpgsqlCommand(
                "SELECT item_uid FROM item_char WHERE char_uid = @char_uid " +
                "AND item_where = @item_where AND item_pos = @item_pos LIMIT 1",
                conn, tx);
            checkCmd.Parameters.AddWithValue("char_uid", charUid);
            checkCmd.Parameters.AddWithValue("item_where", (short)newSlotKind);
            checkCmd.Parameters.AddWithValue("item_pos", (short)newSlotPosition);
            if (checkCmd.ExecuteScalar() is long occupiedUid && occupiedUid != unchecked((long)itemUid))
            {
                throw new InvalidOperationException(
                    $"Equipment slot ({newSlotKind}, {newSlotPosition}) is already occupied.");
            }
        }

        using var updateCmd = new NpgsqlCommand(
            """
            UPDATE item_char
            SET item_where = @item_where, item_pos = @item_pos
            WHERE item_uid = @item_uid
            """,
            conn, tx);
        updateCmd.Parameters.AddWithValue("item_uid", unchecked((long)itemUid));
        updateCmd.Parameters.AddWithValue("item_where", (short)newSlotKind);
        updateCmd.Parameters.AddWithValue("item_pos", (short)newSlotPosition);
        var affected = updateCmd.ExecuteNonQuery();
        tx.Commit();
        return affected == 1;
    }

    public bool UnassignFromCharacter(ulong itemUid)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "DELETE FROM item_char WHERE item_uid = @item_uid",
            conn);
        cmd.Parameters.AddWithValue("item_uid", unchecked((long)itemUid));
        return cmd.ExecuteNonQuery() == 1;
    }

    public bool AssignToCharacter(ulong itemUid, ulong characterId, byte slotKind, byte slotPosition)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();

        if (slotKind == SlotKind.Equipment)
        {
            using var checkCmd = new NpgsqlCommand(
                "SELECT 1 FROM item_char WHERE char_uid = @char_uid " +
                "AND item_where = @item_where AND item_pos = @item_pos LIMIT 1",
                conn, tx);
            checkCmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
            checkCmd.Parameters.AddWithValue("item_where", (short)slotKind);
            checkCmd.Parameters.AddWithValue("item_pos", (short)slotPosition);
            if (checkCmd.ExecuteScalar() is not null)
            {
                throw new InvalidOperationException(
                    $"Equipment slot ({slotKind}, {slotPosition}) is already occupied for char {characterId}.");
            }
        }

        using (var insertCmd = new NpgsqlCommand(
            """
            INSERT INTO item_char (char_uid, item_uid, item_where, item_pos)
            VALUES (@char_uid, @item_uid, @item_where, @item_pos)
            ON CONFLICT (char_uid, item_uid) DO UPDATE
                SET item_where = EXCLUDED.item_where,
                    item_pos   = EXCLUDED.item_pos
            """,
            conn, tx))
        {
            insertCmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
            insertCmd.Parameters.AddWithValue("item_uid", unchecked((long)itemUid));
            insertCmd.Parameters.AddWithValue("item_where", (short)slotKind);
            insertCmd.Parameters.AddWithValue("item_pos", (short)slotPosition);
            insertCmd.ExecuteNonQuery();
        }

        tx.Commit();
        return true;
    }

    public bool Delete(ulong itemUid)
    {


        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "DELETE FROM items WHERE item_uid = @item_uid",
            conn);
        cmd.Parameters.AddWithValue("item_uid", unchecked((long)itemUid));
        return cmd.ExecuteNonQuery() == 1;
    }

    public ushort AdjustStack(ulong itemUid, int delta)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();

        long currentStack;
        using (var readCmd = new NpgsqlCommand(
            "SELECT item_stack FROM items WHERE item_uid = @item_uid",
            conn, tx))
        {
            readCmd.Parameters.AddWithValue("item_uid", unchecked((long)itemUid));
            var result = readCmd.ExecuteScalar();
            if (result is null) { tx.Rollback(); return 0; }
            currentStack = (int)result;
        }

        var newStack = currentStack + delta;
        if (newStack <= 0)
        {
            using var delCmd = new NpgsqlCommand(
                "DELETE FROM items WHERE item_uid = @item_uid",
                conn, tx);
            delCmd.Parameters.AddWithValue("item_uid", unchecked((long)itemUid));
            delCmd.ExecuteNonQuery();
            tx.Commit();
            return 0;
        }
        if (newStack > ushort.MaxValue) newStack = ushort.MaxValue;

        using (var updCmd = new NpgsqlCommand(
            "UPDATE items SET item_stack = @item_stack WHERE item_uid = @item_uid",
            conn, tx))
        {
            updCmd.Parameters.AddWithValue("item_stack", (int)newStack);
            updCmd.Parameters.AddWithValue("item_uid", unchecked((long)itemUid));
            updCmd.ExecuteNonQuery();
        }
        tx.Commit();
        return (ushort)newStack;
    }

    public bool SetStack(ulong itemUid, ushort stack)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("UPDATE items SET item_stack = @item_stack WHERE item_uid = @item_uid", conn);
        cmd.Parameters.AddWithValue("item_stack", (int)stack);
        cmd.Parameters.AddWithValue("item_uid", unchecked((long)itemUid));
        return cmd.ExecuteNonQuery() == 1;
    }

    private const string BaseSelectSql = """
        SELECT i.item_uid, i.item_index, i.item_class, i.item_attr,
               i.item_charged, i.item_breaked, i.refine_type, i.refine_level,
               i.refine_state, i.item_slot, i.item_stack,
               c.char_uid, c.item_where, c.item_pos
        FROM items i
        JOIN item_char c ON c.item_uid = i.item_uid
        """;

    private static Item ReadItem(NpgsqlDataReader reader) => new()
    {
        ItemUid = unchecked((ulong)reader.GetInt64(0)),
        ItemIndex = reader.GetInt32(1),
        ItemClass = (byte)reader.GetInt16(2),
        ItemAttr = reader.GetInt16(3),
        ItemCharged = (byte)reader.GetInt16(4),
        BreakedAt = new DateTimeOffset(reader.GetDateTime(5), TimeSpan.Zero),
        RefineType = reader.GetInt16(6),
        RefineLevel = (byte)reader.GetInt16(7),
        RefineState = (byte)reader.GetInt16(8),
        ItemSlot = (byte)reader.GetInt16(9),
        Stack = (ushort)reader.GetInt32(10),
        CharacterId = unchecked((ulong)reader.GetInt64(11)),
        SlotKind = (byte)reader.GetInt16(12),
        SlotPosition = (byte)reader.GetInt16(13),
    };
}
