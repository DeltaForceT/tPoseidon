using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Net.World;

public static class BankFrames
{
    public const int ItemRequestSize = 14;
    public const int MoneyRequestSize = 4;
    public const byte AlreadySent = 1;
    public const byte Sent = 0;

    public static byte[] AlreadyLoaded() => new[] { AlreadySent };

    public static byte[] Contents(
        long bankMoney, IReadOnlyList<(Item Item, byte ItemClass)> items, Func<Item, IReadOnlyList<ItemProperty>>? properties = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        var w = new PacketWriter(16 + items.Count * 26);
        w.WriteByte(Sent);
        w.WriteUInt32Field(bankMoney);
        WriteStoredItems(w, items, properties);
        return w.ToArray();
    }

    internal static void WriteStoredItems(
        PacketWriter w, IReadOnlyList<(Item Item, byte ItemClass)> items, Func<Item, IReadOnlyList<ItemProperty>>? properties)
    {
        w.WriteUInt16((ushort)items.Count);
        foreach (var (item, itemClass) in items)
        {
            w.WriteByte(itemClass);
            w.WriteUInt32((uint)item.ItemIndex);
            w.WriteUInt64(BagItemIds.OnWire(item.ItemUid));
            w.WriteUInt16(item.Stack);
            if (itemClass == ItemParamEntry.RareClass)
            {
                w.WriteByte((byte)item.RefineType);
                w.WriteByte(item.RefineLevel);
                w.WriteByte(item.RefineState);
                w.WriteByte(item.ItemSlot);
            }
            w.WriteByte(0);
        }
        var rare = items.Where(entry => entry.ItemClass == ItemParamEntry.RareClass).ToList();
        w.WriteUInt16((ushort)rare.Count);
        foreach (var (item, _) in rare)
        {
            w.WriteUInt64(BagItemIds.OnWire(item.ItemUid));
            var options = properties?.Invoke(item) ?? Array.Empty<ItemProperty>();
            w.WriteByte((byte)options.Count);
            foreach (var option in options) tPoseidon.Core.Net.Packets.GameItemListPacket.WriteOption(w, option);
        }
    }

    public static bool TryReadItemRequest(ReadOnlySpan<byte> payload, out ulong wireItemUid, out int itemId, out int count)
    {
        wireItemUid = 0;
        itemId = 0;
        count = 0;
        if (payload.Length < ItemRequestSize) return false;
        wireItemUid = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(payload);
        itemId = (int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(payload[8..]);
        count = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(payload[12..]);
        return true;
    }

    public static byte[] Moved(BagOperation bag, BagOperation bank)
    {
        var w = new PacketWriter(48);
        w.WriteBytes(QuestResultFrames.ItemOperation(bag));
        w.WriteBytes(QuestResultFrames.ItemOperation(bank));
        return w.ToArray();
    }

    public static byte[] Money(long bankMoney, long money)
    {
        var w = new PacketWriter(8);
        w.WriteUInt32Field(bankMoney);
        w.WriteUInt32Field(money);
        return w.ToArray();
    }

    public static byte[] Code(ushort code)
    {
        var w = new PacketWriter(2);
        w.WriteUInt16(code);
        return w.ToArray();
    }
}
