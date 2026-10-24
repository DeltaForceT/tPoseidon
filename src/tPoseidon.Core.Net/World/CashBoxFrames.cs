using tPoseidon.Core.Data;

namespace tPoseidon.Core.Net.World;

public readonly record struct CashBoxTake(ulong WireUid, uint ItemId, ushort Count);

public static class CashBoxFrames
{
    public const int PageCapacity = 0x400;
    public const int PageReserve = 0x21;
    public const int TimedRecordSize = 0x15;
    public const byte HeaderPage = 0;
    public const byte MorePages = 1;
    public const byte LastPage = 2;

    private const int PageHeaderSize = 3;

    public static bool TryReadTake(ReadOnlySpan<byte> body, out CashBoxTake take)
    {
        take = default;
        if (body.Length < 14) return false;
        var r = new PacketReader(body);
        take = new CashBoxTake(r.ReadUInt64(), r.ReadUInt32(), r.ReadUInt16());
        return true;
    }

    public static byte WireClass(ChargedItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.ItemClass == ChargedItemRules.CommonClass ? ChargedItemRules.CommonClass : Item.RareClass;
    }

    public static ushort ListedCount(ChargedItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return (ushort)Math.Clamp((int)item.ItemAttr, 1, ushort.MaxValue);
    }

    public static byte[] Header(int entries)
    {
        var w = new PacketWriter(5);
        w.WriteByte(HeaderPage);
        w.WriteUInt16(0);
        w.WriteUInt16((ushort)Math.Clamp(entries, 0, ushort.MaxValue));
        return w.ToArray();
    }

    public static IReadOnlyList<byte[]> Pages(IReadOnlyList<ChargedItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var pages = new List<byte[]>();
        var next = 0;
        do
        {
            var entries = new PacketWriter(PageCapacity);
            var used = PageHeaderSize;
            var count = 0;
            while (next < items.Count && PageCapacity - used >= PageReserve)
            {
                var entry = Entry(items[next]);
                entries.WriteBytes(entry);
                used += entry.Length;
                count++;
                next++;
            }
            var page = new PacketWriter(used);
            page.WriteByte(next < items.Count ? MorePages : LastPage);
            page.WriteUInt16((ushort)count);
            page.WriteBytes(entries.ToArray());
            pages.Add(page.ToArray());
        }
        while (next < items.Count);
        return pages;
    }

    public static byte[] Taken(BagOperation bagSide, BagOperation boxSide, DateTimeOffset? breakedAt)
    {
        var w = new PacketWriter(64);
        w.WriteBytes(QuestResultFrames.ItemOperation(bagSide));
        w.WriteBytes(QuestResultFrames.ItemOperation(boxSide));
        if (breakedAt is { } expiry)
        {
            w.WriteUInt64(bagSide.ItemUid);
            w.WriteUInt32(bagSide.ItemId);
            w.WriteUInt64(ClientClock.FileTime(expiry));
            w.WriteByte(0);
        }
        else
        {
            w.WriteZero(TimedRecordSize);
        }
        return w.ToArray();
    }

    private static byte[] Entry(ChargedItem item)
    {
        var wireClass = WireClass(item);
        var w = new PacketWriter(22);
        w.WriteByte(wireClass);
        w.WriteUInt32((uint)item.ItemIndex);
        w.WriteUInt64(BagItemIds.OnWire(item.ItemUid));
        w.WriteUInt16(ListedCount(item));
        if (wireClass == Item.RareClass) w.WriteZero(QuestResultFrames.RareItemExtraBytes);
        w.WriteUInt16((ushort)item.UseTimeHours);
        w.WriteByte(item.ItemFlag);
        return w.ToArray();
    }
}
