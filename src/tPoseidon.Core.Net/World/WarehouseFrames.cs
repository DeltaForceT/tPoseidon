using System.Buffers.Binary;
using tPoseidon.Core.Data;

namespace tPoseidon.Core.Net.World;

public static class WarehouseFrames
{
    public const int ItemRequestSize = 15;
    public const int MoveRequestSize = 16;

    public static byte[] AlreadyLoaded(byte tab) => new[] { tab, BankFrames.AlreadySent };

    public static byte[] Contents(
        byte tab, IReadOnlyList<(Item Item, byte ItemClass)> items, Func<Item, IReadOnlyList<ItemProperty>>? properties = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        var w = new PacketWriter(6 + items.Count * 26);
        w.WriteByte(tab);
        w.WriteByte(BankFrames.Sent);
        BankFrames.WriteStoredItems(w, items, properties);
        return w.ToArray();
    }

    public static bool TryReadItemRequest(ReadOnlySpan<byte> payload, out byte tab, out ulong wireItemUid, out int itemId, out int count)
    {
        tab = 0;
        wireItemUid = 0;
        itemId = 0;
        count = 0;
        if (payload.Length < ItemRequestSize) return false;
        tab = payload[0];
        return BankFrames.TryReadItemRequest(payload[1..], out wireItemUid, out itemId, out count);
    }

    public static bool TryReadMoveRequest(ReadOnlySpan<byte> payload, out byte from, out byte to, out ulong wireItemUid, out int count)
    {
        from = 0;
        to = 0;
        wireItemUid = 0;
        count = 0;
        if (payload.Length < MoveRequestSize) return false;
        from = payload[0];
        to = payload[1];
        wireItemUid = BinaryPrimitives.ReadUInt64LittleEndian(payload[2..]);
        count = BinaryPrimitives.ReadUInt16LittleEndian(payload[14..]);
        return true;
    }

    public static byte[] Moved(byte tab, BagOperation bag, BagOperation warehouse)
    {
        var w = new PacketWriter(48);
        w.WriteByte(tab);
        w.WriteBytes(BankFrames.Moved(bag, warehouse));
        return w.ToArray();
    }

    public static byte[] Refused(byte tab) => new[] { tab };

    public static byte[] Exchanged(byte from, BagOperation fromOperation, byte to, BagOperation toOperation)
    {
        var w = new PacketWriter(48);
        w.WriteByte(from);
        w.WriteBytes(QuestResultFrames.ItemOperation(fromOperation));
        w.WriteByte(to);
        w.WriteBytes(QuestResultFrames.ItemOperation(toOperation));
        return w.ToArray();
    }
}
