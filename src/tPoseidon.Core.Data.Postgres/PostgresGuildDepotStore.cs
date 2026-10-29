using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresGuildDepotStore : IGuildDepotStore
{
    private const byte InventoryKind = 1;
    private const int LastBagPosition = 254;

    private const string ItemColumns =
        "i.item_uid, i.item_index, i.item_class, i.item_attr, i.item_charged, i.item_breaked, " +
        "i.refine_type, i.refine_level, i.refine_state, i.item_slot, i.item_stack";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresGuildDepotStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public GuildDepotAccess? AccessOf(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            SELECT m.guild_code, m.member_right, g.master_char_uid = m.char_uid, g.depot_tabs
            FROM guild_member m JOIN guild g ON g.guild_code = m.guild_code
            WHERE m.char_uid = @c
            """,
            conn);
        cmd.Parameters.AddWithValue("c", unchecked((long)characterId));
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        var rights = reader.GetBoolean(2) ? GuildDepotRules.MasterRights : (ushort)reader.GetInt16(1);
        return new GuildDepotAccess(unchecked((ulong)reader.GetInt64(0)), rights, reader.GetInt16(3));
    }

    public int TabsOf(ulong guildCode)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("SELECT depot_tabs FROM guild WHERE guild_code = @g", conn);
        cmd.Parameters.AddWithValue("g", unchecked((long)guildCode));
        return cmd.ExecuteScalar() is short tabs ? tabs : 0;
    }

    public bool SetTabs(ulong guildCode, int tabs)
    {
        if (tabs < 0 || tabs > GuildDepotRules.MaxTabs) return false;
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        if (LockGuild(conn, tx, guildCode) is not { } current) return false;
        if (tabs < current && EntriesAbove(conn, tx, guildCode, tabs) > 0) return false;
        using var cmd = new NpgsqlCommand("UPDATE guild SET depot_tabs = @t WHERE guild_code = @g", conn, tx);
        cmd.Parameters.AddWithValue("t", (short)tabs);
        cmd.Parameters.AddWithValue("g", unchecked((long)guildCode));
        cmd.ExecuteNonQuery();
        tx.Commit();
        return true;
    }

    public IReadOnlyList<GuildDepotItem> Contents(ulong guildCode, byte tab)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            $"SELECT {ItemColumns}, d.tab FROM guild_depot_item d JOIN items i ON i.item_uid = d.item_uid " +
            "WHERE d.guild_code = @g AND d.tab = @t ORDER BY d.entry_seq",
            conn);
        cmd.Parameters.AddWithValue("g", unchecked((long)guildCode));
        cmd.Parameters.AddWithValue("t", (short)tab);
        using var reader = cmd.ExecuteReader();
        var result = new List<GuildDepotItem>();
        while (reader.Read()) result.Add(ReadDepotItem(reader));
        return result;
    }

    public IReadOnlyList<GuildDepotHistoryEntry> History(ulong guildCode)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            SELECT history_seq, char_name, item_index, item_count, action, created_at
            FROM (
                SELECT * FROM guild_depot_history WHERE guild_code = @g ORDER BY history_seq DESC LIMIT @n
            ) latest
            ORDER BY history_seq
            """,
            conn);
        cmd.Parameters.AddWithValue("g", unchecked((long)guildCode));
        cmd.Parameters.AddWithValue("n", GuildDepotRules.HistoryShown);
        using var reader = cmd.ExecuteReader();
        var result = new List<GuildDepotHistoryEntry>();
        while (reader.Read()) result.Add(ReadHistory(reader));
        return result;
    }

    public GuildDepotTransfer Deposit(GuildDepotDeposit request)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        if (CheckTab(conn, tx, request.GuildCode, request.Tab, request.Count) is { } refused) return GuildDepotTransfer.Fail(refused);

        var bag = BagEntry(conn, tx, request.CharacterId, request.ItemUid);
        if (bag is null || bag.Stack < request.Count) return GuildDepotTransfer.Fail(GuildDepotErrors.ItemMissing);

        var merge = request.Stackable ? DepotMergeTarget(conn, tx, request.GuildCode, request.Tab, bag) : null;
        if (merge is not null && merge.Stack + request.Count > GuildDepotRules.StackLimit) return GuildDepotTransfer.Fail(GuildDepotErrors.CannotAdd);
        if (merge is null && TabEntries(conn, tx, request.GuildCode, request.Tab) >= GuildDepotRules.TabSlots)
        {
            return GuildDepotTransfer.Fail(GuildDepotErrors.NoSlot);
        }

        var whole = bag.Stack == request.Count;
        GuildDepotSide depotSide;
        if (merge is not null)
        {
            var grown = merge with { Stack = (ushort)(merge.Stack + request.Count) };
            SetStack(conn, tx, merge.ItemUid, grown.Stack);
            depotSide = new GuildDepotSide(grown, GuildDepotChange.Grown, request.Count);
        }
        else if (whole)
        {
            Execute(conn, tx, "DELETE FROM item_char WHERE item_uid = @u", ("u", unchecked((long)bag.ItemUid)));
            InsertDepotEntry(conn, tx, request.GuildCode, request.Tab, bag.ItemUid);
            depotSide = new GuildDepotSide(bag with { Tab = request.Tab }, GuildDepotChange.Created, request.Count);
        }
        else
        {
            var split = CloneItem(conn, tx, bag.ItemUid, request.Count);
            InsertDepotEntry(conn, tx, request.GuildCode, request.Tab, split);
            depotSide = new GuildDepotSide(bag with { ItemUid = split, Stack = request.Count, Tab = request.Tab }, GuildDepotChange.Created, request.Count);
        }

        GuildDepotSide bagSide;
        if (whole && merge is not null)
        {
            Execute(conn, tx, "DELETE FROM items WHERE item_uid = @u", ("u", unchecked((long)bag.ItemUid)));
            bagSide = new GuildDepotSide(bag with { Stack = 0 }, GuildDepotChange.Removed, request.Count);
        }
        else if (whole)
        {
            bagSide = new GuildDepotSide(bag with { Stack = 0 }, GuildDepotChange.Removed, request.Count);
        }
        else
        {
            var left = (ushort)(bag.Stack - request.Count);
            SetStack(conn, tx, bag.ItemUid, left);
            bagSide = new GuildDepotSide(bag with { Stack = left }, GuildDepotChange.Shrunk, request.Count);
        }

        var history = AppendHistory(conn, tx, request.GuildCode, request.CharacterName, bag.ItemIndex, request.Count, GuildDepotRules.DepositAction);
        tx.Commit();
        return new GuildDepotTransfer(GuildDepotErrors.None, depotSide, bagSide, history);
    }

    public GuildDepotTransfer Withdraw(GuildDepotWithdrawal request)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        if (CheckTab(conn, tx, request.GuildCode, request.Tab, request.Count) is { } refused) return GuildDepotTransfer.Fail(refused);

        var stored = DepotEntry(conn, tx, request.GuildCode, request.Tab, request.ItemUid);
        if (stored is null || stored.Stack < request.Count) return GuildDepotTransfer.Fail(GuildDepotErrors.ItemMissing);
        if ((long)request.UnitWeight * request.Count > request.WeightRoom) return GuildDepotTransfer.Fail(GuildDepotErrors.TooManyCarried);

        var carried = BagPositions(conn, tx, request.CharacterId);
        var merge = request.Stackable ? BagMergeTarget(conn, tx, request.CharacterId, stored) : null;
        if (merge is not null && merge.Stack + request.Count > GuildDepotRules.StackLimit) return GuildDepotTransfer.Fail(GuildDepotErrors.CannotAdd);
        if (merge is null && carried.Count >= GuildDepotRules.BagCapacity) return GuildDepotTransfer.Fail(GuildDepotErrors.TooManyCarried);

        var whole = stored.Stack == request.Count;
        GuildDepotSide bagSide;
        ulong deliveredUid;
        if (merge is not null)
        {
            var grown = merge with { Stack = (ushort)(merge.Stack + request.Count) };
            SetStack(conn, tx, merge.ItemUid, grown.Stack);
            bagSide = new GuildDepotSide(grown, GuildDepotChange.Grown, request.Count);
            deliveredUid = merge.ItemUid;
        }
        else
        {
            var position = FirstFreePosition(carried);
            if (position < 0) return GuildDepotTransfer.Fail(GuildDepotErrors.TooManyCarried);
            deliveredUid = whole ? stored.ItemUid : CloneItem(conn, tx, stored.ItemUid, request.Count);
            if (whole) Execute(conn, tx, "DELETE FROM guild_depot_item WHERE item_uid = @u", ("u", unchecked((long)stored.ItemUid)));
            InsertBagEntry(conn, tx, request.CharacterId, deliveredUid, (byte)position);
            bagSide = new GuildDepotSide(stored with { ItemUid = deliveredUid, Stack = request.Count, Tab = 0 }, GuildDepotChange.Created, request.Count);
        }

        GuildDepotSide depotSide;
        if (whole)
        {
            if (merge is not null) Execute(conn, tx, "DELETE FROM items WHERE item_uid = @u", ("u", unchecked((long)stored.ItemUid)));
            depotSide = new GuildDepotSide(stored with { Stack = 0 }, GuildDepotChange.Removed, request.Count);
        }
        else
        {
            var left = (ushort)(stored.Stack - request.Count);
            SetStack(conn, tx, stored.ItemUid, left);
            depotSide = new GuildDepotSide(stored with { Stack = left }, GuildDepotChange.Shrunk, request.Count);
        }

        var history = AppendHistory(conn, tx, request.GuildCode, request.CharacterName, stored.ItemIndex, request.Count, GuildDepotRules.WithdrawAction);
        tx.Commit();
        return new GuildDepotTransfer(GuildDepotErrors.None, depotSide, bagSide, history);
    }

    private static int? CheckTab(NpgsqlConnection conn, NpgsqlTransaction tx, ulong guildCode, byte tab, int count)
    {
        if (LockGuild(conn, tx, guildCode) is not { } tabs) return GuildDepotErrors.Unknown;
        if (tab < 1 || tab > GuildDepotRules.MaxTabs || count < 1) return GuildDepotErrors.BadItem;
        if (tabs == 0) return GuildDepotErrors.NotPurchased;
        return tab > tabs ? GuildDepotErrors.NotExpanded : null;
    }

    private static int? LockGuild(NpgsqlConnection conn, NpgsqlTransaction tx, ulong guildCode)
    {
        using var cmd = new NpgsqlCommand("SELECT depot_tabs FROM guild WHERE guild_code = @g FOR UPDATE", conn, tx);
        cmd.Parameters.AddWithValue("g", unchecked((long)guildCode));
        return cmd.ExecuteScalar() is short tabs ? tabs : null;
    }

    private static long EntriesAbove(NpgsqlConnection conn, NpgsqlTransaction tx, ulong guildCode, int tabs)
    {
        using var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM guild_depot_item WHERE guild_code = @g AND tab > @t", conn, tx);
        cmd.Parameters.AddWithValue("g", unchecked((long)guildCode));
        cmd.Parameters.AddWithValue("t", (short)tabs);
        return (long)cmd.ExecuteScalar()!;
    }

    private static long TabEntries(NpgsqlConnection conn, NpgsqlTransaction tx, ulong guildCode, byte tab)
    {
        using var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM guild_depot_item WHERE guild_code = @g AND tab = @t", conn, tx);
        cmd.Parameters.AddWithValue("g", unchecked((long)guildCode));
        cmd.Parameters.AddWithValue("t", (short)tab);
        return (long)cmd.ExecuteScalar()!;
    }

    private static GuildDepotItem? BagEntry(NpgsqlConnection conn, NpgsqlTransaction tx, ulong characterId, ulong itemUid)
    {
        using var cmd = new NpgsqlCommand(
            $"SELECT {ItemColumns}, 0::smallint FROM items i JOIN item_char c ON c.item_uid = i.item_uid " +
            "WHERE c.char_uid = @c AND c.item_uid = @u AND c.item_where = @w FOR UPDATE OF i, c",
            conn, tx);
        cmd.Parameters.AddWithValue("c", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("u", unchecked((long)itemUid));
        cmd.Parameters.AddWithValue("w", (short)InventoryKind);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadDepotItem(reader) : null;
    }

    private static GuildDepotItem? DepotEntry(NpgsqlConnection conn, NpgsqlTransaction tx, ulong guildCode, byte tab, ulong itemUid)
    {
        using var cmd = new NpgsqlCommand(
            $"SELECT {ItemColumns}, d.tab FROM guild_depot_item d JOIN items i ON i.item_uid = d.item_uid " +
            "WHERE d.guild_code = @g AND d.tab = @t AND d.item_uid = @u FOR UPDATE OF i, d",
            conn, tx);
        cmd.Parameters.AddWithValue("g", unchecked((long)guildCode));
        cmd.Parameters.AddWithValue("t", (short)tab);
        cmd.Parameters.AddWithValue("u", unchecked((long)itemUid));
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadDepotItem(reader) : null;
    }

    private static GuildDepotItem? DepotMergeTarget(NpgsqlConnection conn, NpgsqlTransaction tx, ulong guildCode, byte tab, GuildDepotItem like)
    {
        using var cmd = new NpgsqlCommand(
            $"SELECT {ItemColumns}, d.tab FROM guild_depot_item d JOIN items i ON i.item_uid = d.item_uid " +
            "JOIN items twin ON twin.item_uid = @u " +
            "WHERE d.guild_code = @g AND d.tab = @t AND i.item_uid <> @u AND i.item_index = twin.item_index " +
            "AND i.item_attr = twin.item_attr AND i.item_charged = twin.item_charged AND i.item_breaked = twin.item_breaked " +
            "ORDER BY d.entry_seq LIMIT 1 FOR UPDATE OF i, d",
            conn, tx);
        cmd.Parameters.AddWithValue("g", unchecked((long)guildCode));
        cmd.Parameters.AddWithValue("t", (short)tab);
        cmd.Parameters.AddWithValue("u", unchecked((long)like.ItemUid));
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadDepotItem(reader) : null;
    }

    private static GuildDepotItem? BagMergeTarget(NpgsqlConnection conn, NpgsqlTransaction tx, ulong characterId, GuildDepotItem like)
    {
        using var cmd = new NpgsqlCommand(
            $"SELECT {ItemColumns}, 0::smallint FROM item_char c JOIN items i ON i.item_uid = c.item_uid " +
            "JOIN items twin ON twin.item_uid = @u " +
            "WHERE c.char_uid = @c AND c.item_where = @w AND i.item_uid <> @u AND i.item_index = twin.item_index " +
            "AND i.item_attr = twin.item_attr AND i.item_charged = twin.item_charged AND i.item_breaked = twin.item_breaked " +
            "ORDER BY c.item_pos LIMIT 1 FOR UPDATE OF i, c",
            conn, tx);
        cmd.Parameters.AddWithValue("c", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("w", (short)InventoryKind);
        cmd.Parameters.AddWithValue("u", unchecked((long)like.ItemUid));
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadDepotItem(reader) : null;
    }

    private static List<int> BagPositions(NpgsqlConnection conn, NpgsqlTransaction tx, ulong characterId)
    {
        using var cmd = new NpgsqlCommand(
            "SELECT item_pos FROM item_char WHERE char_uid = @c AND item_where = @w ORDER BY item_pos FOR UPDATE", conn, tx);
        cmd.Parameters.AddWithValue("c", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("w", (short)InventoryKind);
        using var reader = cmd.ExecuteReader();
        var positions = new List<int>();
        while (reader.Read()) positions.Add(reader.GetInt16(0));
        return positions;
    }

    private static int FirstFreePosition(List<int> taken)
    {
        var used = new HashSet<int>(taken);
        for (var position = 0; position <= LastBagPosition; position++)
        {
            if (!used.Contains(position)) return position;
        }
        return -1;
    }

    private static ulong CloneItem(NpgsqlConnection conn, NpgsqlTransaction tx, ulong itemUid, int stack)
    {
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO items (item_index, item_class, item_attr, item_charged, item_breaked, refine_type, refine_level, refine_state, item_slot, item_stack)
            SELECT item_index, item_class, item_attr, item_charged, item_breaked, refine_type, refine_level, refine_state, item_slot, @n
            FROM items WHERE item_uid = @u
            RETURNING item_uid
            """,
            conn, tx);
        cmd.Parameters.AddWithValue("u", unchecked((long)itemUid));
        cmd.Parameters.AddWithValue("n", stack);
        return unchecked((ulong)(long)cmd.ExecuteScalar()!);
    }

    private static void InsertDepotEntry(NpgsqlConnection conn, NpgsqlTransaction tx, ulong guildCode, byte tab, ulong itemUid) =>
        Execute(conn, tx, "INSERT INTO guild_depot_item (item_uid, guild_code, tab) VALUES (@u, @g, @t)",
            ("u", unchecked((long)itemUid)), ("g", unchecked((long)guildCode)), ("t", (short)tab));

    private static void InsertBagEntry(NpgsqlConnection conn, NpgsqlTransaction tx, ulong characterId, ulong itemUid, byte position) =>
        Execute(conn, tx, "INSERT INTO item_char (char_uid, item_uid, item_where, item_pos) VALUES (@c, @u, @w, @p)",
            ("c", unchecked((long)characterId)), ("u", unchecked((long)itemUid)), ("w", (short)InventoryKind), ("p", (short)position));

    private static void SetStack(NpgsqlConnection conn, NpgsqlTransaction tx, ulong itemUid, int stack) =>
        Execute(conn, tx, "UPDATE items SET item_stack = @s WHERE item_uid = @u", ("s", stack), ("u", unchecked((long)itemUid)));

    private static GuildDepotHistoryEntry AppendHistory(
        NpgsqlConnection conn, NpgsqlTransaction tx, ulong guildCode, string characterName, int itemIndex, ushort count, byte action)
    {
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO guild_depot_history (guild_code, char_name, item_index, item_count, action)
            VALUES (@g, @n, @i, @c, @a)
            RETURNING history_seq, created_at
            """,
            conn, tx);
        cmd.Parameters.AddWithValue("g", unchecked((long)guildCode));
        cmd.Parameters.AddWithValue("n", characterName);
        cmd.Parameters.AddWithValue("i", itemIndex);
        cmd.Parameters.AddWithValue("c", (int)count);
        cmd.Parameters.AddWithValue("a", (short)action);
        GuildDepotHistoryEntry entry;
        using (var reader = cmd.ExecuteReader())
        {
            reader.Read();
            entry = new GuildDepotHistoryEntry(
                reader.GetInt64(0), characterName, itemIndex, count, action, new DateTimeOffset(reader.GetDateTime(1), TimeSpan.Zero));
        }
        Execute(conn, tx,
            """
            DELETE FROM guild_depot_history
            WHERE guild_code = @g AND history_seq NOT IN (
                SELECT history_seq FROM guild_depot_history WHERE guild_code = @g ORDER BY history_seq DESC LIMIT @k)
            """,
            ("g", unchecked((long)guildCode)), ("k", GuildDepotRules.HistoryKept));
        return entry;
    }

    private static void Execute(NpgsqlConnection conn, NpgsqlTransaction tx, string sql, params (string Name, object Value)[] parameters)
    {
        using var cmd = new NpgsqlCommand(sql, conn, tx);
        foreach (var (name, value) in parameters) cmd.Parameters.AddWithValue(name, value);
        cmd.ExecuteNonQuery();
    }

    private static GuildDepotItem ReadDepotItem(NpgsqlDataReader reader) => new(
        unchecked((ulong)reader.GetInt64(0)),
        reader.GetInt32(1),
        (byte)reader.GetInt16(2),
        (ushort)reader.GetInt32(10),
        reader.GetInt16(6),
        (byte)reader.GetInt16(7),
        (byte)reader.GetInt16(8),
        (byte)reader.GetInt16(9),
        (byte)reader.GetInt16(11));

    private static GuildDepotHistoryEntry ReadHistory(NpgsqlDataReader reader) => new(
        reader.GetInt64(0),
        reader.GetString(1),
        reader.GetInt32(2),
        (ushort)reader.GetInt32(3),
        (byte)reader.GetInt16(4),
        new DateTimeOffset(reader.GetDateTime(5), TimeSpan.Zero));
}
