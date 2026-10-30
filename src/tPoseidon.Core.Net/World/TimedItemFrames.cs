using tPoseidon.Core.Game.Items;

namespace tPoseidon.Core.Net.World;

public static class TimedItemFrames
{
    public static byte[] Login(DateTime utcNow, IReadOnlyList<TimedItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var w = new PacketWriter(10 + items.Count * CashBoxFrames.TimedRecordSize);
        w.WriteUInt64(ClientClock.FileTime(utcNow));
        w.WriteUInt16((ushort)items.Count);
        foreach (var item in items) WriteRecord(w, item);
        return w.ToArray();
    }

    public static byte[] Registered(IReadOnlyList<TimedItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var w = new PacketWriter(sizeof(uint) + items.Count * CashBoxFrames.TimedRecordSize);
        w.WriteUInt32((uint)items.Count);
        foreach (var item in items) WriteRecord(w, item);
        return w.ToArray();
    }

    private static void WriteRecord(PacketWriter w, TimedItem item)
    {
        w.WriteUInt64(BagItemIds.OnWire(item.ItemUid));
        w.WriteUInt32((uint)item.ItemIndex);
        w.WriteUInt64(ClientClock.FileTime(item.ExpiresAt));
        w.WriteByte(0);
    }

    public static byte[] Clock(DateTime utcNow)
    {
        var w = new PacketWriter(sizeof(ulong));
        w.WriteUInt64(ClientClock.FileTime(utcNow));
        return w.ToArray();
    }

    public static byte[] Notice(ulong itemUid, int minutesLeft)
    {
        var w = new PacketWriter(12);
        w.WriteUInt64(BagItemIds.OnWire(itemUid));
        w.WriteUInt32((uint)Math.Max(0, minutesLeft));
        return w.ToArray();
    }

    public static byte[] Expired(ulong itemUid)
    {
        var w = new PacketWriter(9);
        w.WriteUInt64(BagItemIds.OnWire(itemUid));
        w.WriteByte(0);
        return w.ToArray();
    }
}
