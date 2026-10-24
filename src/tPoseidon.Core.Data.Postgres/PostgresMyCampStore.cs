using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresMyCampStore : IMyCampStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresMyCampStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public bool IsInitializedForAccount(uint accountId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "SELECT 1 FROM user_mycamp_log WHERE account_id = @account_id",
            conn);
        cmd.Parameters.AddWithValue("account_id", (long)accountId);
        return cmd.ExecuteScalar() is not null;
    }

    public void MarkInitializedForAccount(uint accountId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO user_mycamp_log (account_id, initialized_at)
            VALUES (@account_id, NOW())
            ON CONFLICT (account_id) DO NOTHING
            """,
            conn);
        cmd.Parameters.AddWithValue("account_id", (long)accountId);
        cmd.ExecuteNonQuery();
    }

    public MyCampSlot CreateSlotForCharacter(ulong characterId, string slotName = "")
    {
        ArgumentNullException.ThrowIfNull(slotName);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO mycamp_slot (char_uid, slot_name, slot_uptodate)
            VALUES (@char_uid, @slot_name, NOW())
            RETURNING slot_code, slot_uptodate
            """,
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("slot_name", slotName);
        using var reader = cmd.ExecuteReader();
        reader.Read();
        return new MyCampSlot
        {
            SlotCode = unchecked((ulong)reader.GetInt64(0)),
            CharacterId = characterId,
            SlotName = slotName,
            SlotUpToDate = new DateTimeOffset(reader.GetDateTime(1), TimeSpan.Zero),
        };
    }

    public MyCampSlot? FindSlot(ulong slotCode)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "SELECT slot_code, char_uid, slot_name, slot_uptodate FROM mycamp_slot WHERE slot_code = @slot_code",
            conn);
        cmd.Parameters.AddWithValue("slot_code", unchecked((long)slotCode));
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadSlot(reader) : null;
    }

    public IReadOnlyList<MyCampSlot> GetSlotsForCharacter(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "SELECT slot_code, char_uid, slot_name, slot_uptodate FROM mycamp_slot WHERE char_uid = @char_uid ORDER BY slot_code",
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        using var reader = cmd.ExecuteReader();
        var result = new List<MyCampSlot>();
        while (reader.Read()) result.Add(ReadSlot(reader));
        return result;
    }

    public bool UpdateSlotName(ulong slotCode, string slotName)
    {
        ArgumentNullException.ThrowIfNull(slotName);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "UPDATE mycamp_slot SET slot_name = @slot_name, slot_uptodate = NOW() WHERE slot_code = @slot_code",
            conn);
        cmd.Parameters.AddWithValue("slot_code", unchecked((long)slotCode));
        cmd.Parameters.AddWithValue("slot_name", slotName);
        return cmd.ExecuteNonQuery() == 1;
    }

    public bool DeleteSlot(ulong slotCode)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "DELETE FROM mycamp_slot WHERE slot_code = @slot_code",
            conn);
        cmd.Parameters.AddWithValue("slot_code", unchecked((long)slotCode));
        return cmd.ExecuteNonQuery() == 1;
    }

    public MyCampItem AddItemForAccount(uint accountId, int itemIndex, byte itemWhere = MyCampItemWhere.New)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO mycamp_item (account_id, item_index, item_where, item_buy_date)
            VALUES (@account_id, @item_index, @item_where, NOW())
            RETURNING item_uid, item_buy_date
            """,
            conn);
        cmd.Parameters.AddWithValue("account_id", (long)accountId);
        cmd.Parameters.AddWithValue("item_index", itemIndex);
        cmd.Parameters.AddWithValue("item_where", (short)itemWhere);
        using var reader = cmd.ExecuteReader();
        reader.Read();
        return new MyCampItem
        {
            ItemUid = unchecked((ulong)reader.GetInt64(0)),
            AccountId = accountId,
            ItemIndex = itemIndex,
            ItemWhere = itemWhere,
            ItemBuyDate = new DateTimeOffset(reader.GetDateTime(1), TimeSpan.Zero),
            ItemUseDate = null,
            ItemUseTime = 0,
            ItemFlag = 0,
            SellCode = 0,
            SellType = 0,
        };
    }

    public MyCampItem? FindItem(ulong itemUid)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            ItemSelectColumns + " WHERE item_uid = @item_uid",
            conn);
        cmd.Parameters.AddWithValue("item_uid", unchecked((long)itemUid));
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadItem(reader) : null;
    }

    public IReadOnlyList<MyCampItem> GetItemsForAccount(uint accountId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            ItemSelectColumns + " WHERE account_id = @account_id ORDER BY item_uid",
            conn);
        cmd.Parameters.AddWithValue("account_id", (long)accountId);
        using var reader = cmd.ExecuteReader();
        var result = new List<MyCampItem>();
        while (reader.Read()) result.Add(ReadItem(reader));
        return result;
    }

    public IReadOnlyList<MyCampItem> GetItemsForAccountByWhere(uint accountId, byte itemWhere)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            ItemSelectColumns + " WHERE account_id = @account_id AND item_where = @item_where ORDER BY item_uid",
            conn);
        cmd.Parameters.AddWithValue("account_id", (long)accountId);
        cmd.Parameters.AddWithValue("item_where", (short)itemWhere);
        using var reader = cmd.ExecuteReader();
        var result = new List<MyCampItem>();
        while (reader.Read()) result.Add(ReadItem(reader));
        return result;
    }

    public MyCampItem? GetLatestBuyItem(uint accountId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            ItemSelectColumns + " WHERE account_id = @account_id ORDER BY item_buy_date DESC LIMIT 1",
            conn);
        cmd.Parameters.AddWithValue("account_id", (long)accountId);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadItem(reader) : null;
    }

    public bool MoveItemToInventory(ulong itemUid) => MoveItemTo(itemUid, MyCampItemWhere.Inventory);

    public bool MoveItemTo(ulong itemUid, byte itemWhere)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "UPDATE mycamp_item SET item_where = @item_where WHERE item_uid = @item_uid",
            conn);
        cmd.Parameters.AddWithValue("item_uid", unchecked((long)itemUid));
        cmd.Parameters.AddWithValue("item_where", (short)itemWhere);
        return cmd.ExecuteNonQuery() == 1;
    }

    public bool DeleteItem(ulong itemUid)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "DELETE FROM mycamp_item WHERE item_uid = @item_uid",
            conn);
        cmd.Parameters.AddWithValue("item_uid", unchecked((long)itemUid));
        return cmd.ExecuteNonQuery() == 1;
    }

    public int BulkDeleteItems(IEnumerable<ulong> itemUids)
    {
        ArgumentNullException.ThrowIfNull(itemUids);
        var uids = itemUids.Select(u => unchecked((long)u)).ToArray();
        if (uids.Length == 0) return 0;
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "DELETE FROM mycamp_item WHERE item_uid = ANY(@item_uids)",
            conn);
        cmd.Parameters.AddWithValue("item_uids", uids);
        return cmd.ExecuteNonQuery();
    }

    public MyCampDeco InsertDeco(ulong slotCode, ulong itemUid, short x, short y, byte z, byte direction, ulong parentsUid = 0)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();

        using (var insertCmd = new NpgsqlCommand(
            """
            INSERT INTO mycamp_deco (slot_code, item_uid, deco_x, deco_y, deco_z, deco_direction, parents_uid)
            VALUES (@slot_code, @item_uid, @deco_x, @deco_y, @deco_z, @deco_direction, @parents_uid)
            ON CONFLICT (slot_code, item_uid) DO UPDATE
                SET deco_x         = EXCLUDED.deco_x,
                    deco_y         = EXCLUDED.deco_y,
                    deco_z         = EXCLUDED.deco_z,
                    deco_direction = EXCLUDED.deco_direction,
                    parents_uid    = EXCLUDED.parents_uid
            """,
            conn, tx))
        {
            insertCmd.Parameters.AddWithValue("slot_code", unchecked((long)slotCode));
            insertCmd.Parameters.AddWithValue("item_uid", unchecked((long)itemUid));
            insertCmd.Parameters.AddWithValue("deco_x", x);
            insertCmd.Parameters.AddWithValue("deco_y", y);
            insertCmd.Parameters.AddWithValue("deco_z", (short)z);
            insertCmd.Parameters.AddWithValue("deco_direction", (short)direction);
            insertCmd.Parameters.AddWithValue("parents_uid", unchecked((long)parentsUid));
            insertCmd.ExecuteNonQuery();
        }

        using (var updCmd = new NpgsqlCommand(
            "UPDATE mycamp_item SET item_where = @inventory, item_use_date = NOW() WHERE item_uid = @item_uid AND item_where = @new",
            conn, tx))
        {
            updCmd.Parameters.AddWithValue("inventory", (short)MyCampItemWhere.Inventory);
            updCmd.Parameters.AddWithValue("new", (short)MyCampItemWhere.New);
            updCmd.Parameters.AddWithValue("item_uid", unchecked((long)itemUid));
            updCmd.ExecuteNonQuery();
        }

        tx.Commit();

        return new MyCampDeco
        {
            SlotCode = slotCode,
            ItemUid = itemUid,
            DecoX = x,
            DecoY = y,
            DecoZ = z,
            DecoDirection = direction,
            ParentsUid = parentsUid,
        };
    }

    public IReadOnlyList<MyCampDeco> GetDecoForSlot(ulong slotCode)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            SELECT slot_code, item_uid, deco_x, deco_y, deco_z, deco_direction, parents_uid
            FROM mycamp_deco
            WHERE slot_code = @slot_code
            ORDER BY item_uid
            """,
            conn);
        cmd.Parameters.AddWithValue("slot_code", unchecked((long)slotCode));
        using var reader = cmd.ExecuteReader();
        var result = new List<MyCampDeco>();
        while (reader.Read()) result.Add(ReadDeco(reader));
        return result;
    }

    public IReadOnlySet<ulong> GetPlacedItemUidsForAccount(uint accountId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "SELECT d.item_uid FROM mycamp_deco d JOIN mycamp_item i ON i.item_uid = d.item_uid WHERE i.account_id = @account",
            conn);
        cmd.Parameters.AddWithValue("account", (long)accountId);
        using var reader = cmd.ExecuteReader();
        var placed = new HashSet<ulong>();
        while (reader.Read()) placed.Add(unchecked((ulong)reader.GetInt64(0)));
        return placed;
    }

    public void ReplaceDeco(ulong slotCode, IReadOnlyList<MyCampDeco> placed)
    {
        ArgumentNullException.ThrowIfNull(placed);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        using (var delCmd = new NpgsqlCommand("DELETE FROM mycamp_deco WHERE slot_code = @slot_code", conn, tx))
        {
            delCmd.Parameters.AddWithValue("slot_code", unchecked((long)slotCode));
            delCmd.ExecuteNonQuery();
        }
        foreach (var deco in placed)
        {
            using (var insertCmd = new NpgsqlCommand(
                """
                INSERT INTO mycamp_deco (slot_code, item_uid, deco_x, deco_y, deco_z, deco_direction, parents_uid)
                VALUES (@slot_code, @item_uid, @deco_x, @deco_y, @deco_z, @deco_direction, @parents_uid)
                """,
                conn, tx))
            {
                insertCmd.Parameters.AddWithValue("slot_code", unchecked((long)slotCode));
                insertCmd.Parameters.AddWithValue("item_uid", unchecked((long)deco.ItemUid));
                insertCmd.Parameters.AddWithValue("deco_x", deco.DecoX);
                insertCmd.Parameters.AddWithValue("deco_y", deco.DecoY);
                insertCmd.Parameters.AddWithValue("deco_z", (short)deco.DecoZ);
                insertCmd.Parameters.AddWithValue("deco_direction", (short)deco.DecoDirection);
                insertCmd.Parameters.AddWithValue("parents_uid", unchecked((long)deco.ParentsUid));
                insertCmd.ExecuteNonQuery();
            }
            using var updCmd = new NpgsqlCommand(
                "UPDATE mycamp_item SET item_where = @inventory, item_use_date = NOW() WHERE item_uid = @item_uid AND item_where = @new",
                conn, tx);
            updCmd.Parameters.AddWithValue("inventory", (short)MyCampItemWhere.Inventory);
            updCmd.Parameters.AddWithValue("new", (short)MyCampItemWhere.New);
            updCmd.Parameters.AddWithValue("item_uid", unchecked((long)deco.ItemUid));
            updCmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public bool RemoveDeco(ulong slotCode, ulong itemUid)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();

        int affected;
        using (var delCmd = new NpgsqlCommand(
            "DELETE FROM mycamp_deco WHERE slot_code = @slot_code AND item_uid = @item_uid",
            conn, tx))
        {
            delCmd.Parameters.AddWithValue("slot_code", unchecked((long)slotCode));
            delCmd.Parameters.AddWithValue("item_uid", unchecked((long)itemUid));
            affected = delCmd.ExecuteNonQuery();
        }

        if (affected == 1)
        {
            using var updCmd = new NpgsqlCommand(
                "UPDATE mycamp_item SET item_where = @inventory WHERE item_uid = @item_uid",
                conn, tx);
            updCmd.Parameters.AddWithValue("inventory", (short)MyCampItemWhere.Inventory);
            updCmd.Parameters.AddWithValue("item_uid", unchecked((long)itemUid));
            updCmd.ExecuteNonQuery();
        }

        tx.Commit();
        return affected == 1;
    }

    public int InitDecoForSlot(ulong slotCode)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "DELETE FROM mycamp_deco WHERE slot_code = @slot_code",
            conn);
        cmd.Parameters.AddWithValue("slot_code", unchecked((long)slotCode));
        return cmd.ExecuteNonQuery();
    }

    public int MoveDecoToInventory(ulong slotCode)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();

        long[] affectedUids;
        using (var selCmd = new NpgsqlCommand(
            "SELECT item_uid FROM mycamp_deco WHERE slot_code = @slot_code",
            conn, tx))
        {
            selCmd.Parameters.AddWithValue("slot_code", unchecked((long)slotCode));
            using var reader = selCmd.ExecuteReader();
            var list = new List<long>();
            while (reader.Read()) list.Add(reader.GetInt64(0));
            affectedUids = list.ToArray();
        }

        if (affectedUids.Length == 0)
        {
            tx.Commit();
            return 0;
        }

        using (var delCmd = new NpgsqlCommand(
            "DELETE FROM mycamp_deco WHERE slot_code = @slot_code",
            conn, tx))
        {
            delCmd.Parameters.AddWithValue("slot_code", unchecked((long)slotCode));
            delCmd.ExecuteNonQuery();
        }

        using (var updCmd = new NpgsqlCommand(
            "UPDATE mycamp_item SET item_where = @inventory WHERE item_uid = ANY(@uids)",
            conn, tx))
        {
            updCmd.Parameters.AddWithValue("inventory", (short)MyCampItemWhere.Inventory);
            updCmd.Parameters.AddWithValue("uids", affectedUids);
            updCmd.ExecuteNonQuery();
        }

        tx.Commit();
        return affectedUids.Length;
    }

    private const string ItemSelectColumns = """
        SELECT item_uid, account_id, item_index, item_where,
               item_buy_date, item_use_date, item_use_time,
               item_flag, sell_code, sell_type
        FROM mycamp_item
        """;

    private static MyCampSlot ReadSlot(NpgsqlDataReader reader) => new()
    {
        SlotCode = unchecked((ulong)reader.GetInt64(0)),
        CharacterId = unchecked((ulong)reader.GetInt64(1)),
        SlotName = reader.GetString(2),
        SlotUpToDate = new DateTimeOffset(reader.GetDateTime(3), TimeSpan.Zero),
    };

    private static MyCampItem ReadItem(NpgsqlDataReader reader) => new()
    {
        ItemUid = unchecked((ulong)reader.GetInt64(0)),
        AccountId = unchecked((uint)reader.GetInt64(1)),
        ItemIndex = reader.GetInt32(2),
        ItemWhere = (byte)reader.GetInt16(3),
        ItemBuyDate = new DateTimeOffset(reader.GetDateTime(4), TimeSpan.Zero),
        ItemUseDate = reader.IsDBNull(5) ? null : new DateTimeOffset(reader.GetDateTime(5), TimeSpan.Zero),
        ItemUseTime = reader.GetInt16(6),
        ItemFlag = (byte)reader.GetInt16(7),
        SellCode = reader.GetInt64(8),
        SellType = (byte)reader.GetInt16(9),
    };

    private static MyCampDeco ReadDeco(NpgsqlDataReader reader) => new()
    {
        SlotCode = unchecked((ulong)reader.GetInt64(0)),
        ItemUid = unchecked((ulong)reader.GetInt64(1)),
        DecoX = reader.GetInt16(2),
        DecoY = reader.GetInt16(3),
        DecoZ = (byte)reader.GetInt16(4),
        DecoDirection = (byte)reader.GetInt16(5),
        ParentsUid = unchecked((ulong)reader.GetInt64(6)),
    };
}
