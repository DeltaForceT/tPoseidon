using System.Buffers.Binary;
using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.Dialogs;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Social;

public sealed record GuildDepotListing(GuildDepotItem Item, IReadOnlyList<ItemProperty> Properties);

public static class GuildDepotFrames
{
    public const int ItemRequestSize = 11;
    public const int HistoryEntrySize = 47;
    public const int HistoryNameBytes = 32;
    public const byte StackChanged = QuestResultFrames.StackChanged;
    public const byte Removed = QuestResultFrames.Removed;
    public const byte Added = BankService.Added;
    public const byte Taken = BankService.Taken;
    public const byte TabLeadByte = 0;

    public static bool TryReadTab(ReadOnlySpan<byte> payload, out byte tab)
    {
        tab = 0;
        if (payload.Length < 1) return false;
        tab = payload[0];
        return true;
    }

    public static bool TryReadItemRequest(ReadOnlySpan<byte> payload, out ulong wireItemUid, out int count, out byte tab)
    {
        wireItemUid = 0;
        count = 0;
        tab = 0;
        if (payload.Length < ItemRequestSize) return false;
        wireItemUid = BinaryPrimitives.ReadUInt64LittleEndian(payload);
        count = BinaryPrimitives.ReadUInt16LittleEndian(payload[8..]);
        tab = payload[10];
        return true;
    }

    public static byte[] Error(int code)
    {
        var w = new PacketWriter(4);
        w.WriteUInt32((uint)code);
        return w.ToArray();
    }

    public static byte[] TabContents(byte tab, IReadOnlyList<GuildDepotListing> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var w = new PacketWriter(3 + items.Count * 26);
        w.WriteByte(TabLeadByte);
        w.WriteByte(tab);
        w.WriteByte((byte)items.Count);
        foreach (var listing in items)
        {
            var item = listing.Item;
            w.WriteByte(item.ItemClass);
            WriteIdentity(w, item.ItemClass, (uint)item.ItemIndex, BagItemIds.OnWire(item.ItemUid), item.Stack, item.RefineType, item.RefineLevel, item.RefineState, item.MixSlot);
            w.WriteUInt16((ushort)listing.Properties.Count);
            foreach (var property in listing.Properties)
            {
                w.WriteByte((byte)property.Type);
                w.WriteUInt16((ushort)property.Id);
                w.WriteUInt16((ushort)property.Value);
                w.WriteUInt32(property.Extra);
            }
        }
        return w.ToArray();
    }

    public static byte[] Transfer(
        byte tab,
        GuildDepotSide depot,
        GuildDepotSide bag,
        IReadOnlyList<ItemProperty> movedProperties,
        GuildDepotHistoryEntry history,
        Encoding text)
    {
        ArgumentNullException.ThrowIfNull(depot);
        ArgumentNullException.ThrowIfNull(bag);
        ArgumentNullException.ThrowIfNull(movedProperties);
        ArgumentNullException.ThrowIfNull(history);
        var w = new PacketWriter(96);
        w.WriteByte(tab);
        w.WriteBytes(QuestResultFrames.ItemOperation(DepotOperation(depot)));
        w.WriteBytes(QuestResultFrames.ItemOperation(BagOperationOf(bag)));
        w.WriteByte((byte)movedProperties.Count);
        foreach (var property in movedProperties) tPoseidon.Core.Net.Packets.GameItemListPacket.WriteOption(w, property);
        WriteHistoryEntry(w, history, text);
        return w.ToArray();
    }

    public static byte[] History(IReadOnlyList<GuildDepotHistoryEntry> entries, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var w = new PacketWriter(1 + entries.Count * HistoryEntrySize);
        w.WriteByte((byte)entries.Count);
        foreach (var entry in entries) WriteHistoryEntry(w, entry, text);
        return w.ToArray();
    }

    public static BagOperation DepotOperation(GuildDepotSide side)
    {
        ArgumentNullException.ThrowIfNull(side);
        var item = side.Item;
        var kind = side.Change switch
        {
            GuildDepotChange.Created => Added,
            GuildDepotChange.Removed => Removed,
            _ => StackChanged,
        };
        var count = side.Change == GuildDepotChange.Removed ? (ushort)0 : side.Moved;
        return Operation(kind, item, count);
    }

    public static BagOperation BagOperationOf(GuildDepotSide side)
    {
        ArgumentNullException.ThrowIfNull(side);
        var item = side.Item;
        return side.Change switch
        {
            GuildDepotChange.Created => Operation(Added, item, side.Moved),
            GuildDepotChange.Removed => Operation(Taken, item, 0),
            _ => Operation(StackChanged, item, item.Stack),
        };
    }

    private static BagOperation Operation(byte kind, GuildDepotItem item, ushort count) => new(
        kind, item.ItemClass, (uint)item.ItemIndex, BagItemIds.OnWire(item.ItemUid), count,
        item.MixSlot, (byte)item.RefineType, item.RefineLevel, item.RefineState);

    private static void WriteIdentity(
        PacketWriter w, byte itemClass, uint itemId, ulong wireUid, ushort count, short refineType, byte refineLevel, byte refineState, byte mixSlot)
    {
        w.WriteUInt32(itemId);
        w.WriteUInt64(wireUid);
        w.WriteUInt16(count);
        if (itemClass != ItemParamEntry.RareClass) return;
        w.WriteByte((byte)refineType);
        w.WriteByte(refineLevel);
        w.WriteByte(refineState);
        w.WriteByte(mixSlot);
    }

    private static void WriteHistoryEntry(PacketWriter w, GuildDepotHistoryEntry entry, Encoding text)
    {
        var limit = HistoryNameBytes - 1;
        var length = entry.CharacterName.Length;
        while (length > 0 && text.GetByteCount(entry.CharacterName.AsSpan(0, length)) > limit) length--;
        var name = text.GetBytes(entry.CharacterName[..length]);
        w.WriteBytes(name);
        w.WriteZero(HistoryNameBytes - name.Length);
        w.WriteUInt32((uint)entry.ItemIndex);
        w.WriteUInt16(entry.Count);
        w.WriteByte(entry.Action);
        w.WriteUInt64((ulong)entry.At.ToFileTime());
    }
}
