using System.Text;
using tPoseidon.Core.Game.Items;

namespace tPoseidon.Core.Net.World;

public static class RecycleFrames
{
    public const ushort Ok = 1;
    public const int CloseRequestSize = 2;
    public const int CouponExchangeRequestSize = 28;

    public const ushort Refused = 0;
    public const ushort ItemRefusalCode = 1;
    public const ushort ItemRefusalKind = 0;
    public const int ItemRequestHeaderSize = 8;
    public const int ItemEntrySize = 14;
    public const int MyCampEntrySize = 12;
    public const int MyCampListTimeSize = 8;
    public const int MyCampListReadyTimeSize = 16;

    public static bool TryReadItems(ReadOnlySpan<byte> payload, out ItemRecycleRequest request)
    {
        request = default;
        if (payload.Length < ItemRequestHeaderSize) return false;
        var r = new PacketReader(payload);
        var reserved = r.ReadUInt32();
        var total = r.ReadUInt16();
        var count = r.ReadUInt16();
        if (payload.Length < ItemRequestHeaderSize + count * ItemEntrySize) return false;
        var entries = new List<ItemRecycleEntry>(count);
        for (var i = 0; i < count; i++)
        {
            var uid = r.ReadUInt64();
            var itemId = r.ReadUInt32();
            r.ReadUInt16();
            entries.Add(new ItemRecycleEntry(uid, itemId));
        }
        request = new ItemRecycleRequest(reserved, total, entries);
        return true;
    }

    public static bool TryReadMyCampItems(ReadOnlySpan<byte> payload, out MyCampRecycleRequest request)
    {
        request = default;
        if (payload.Length < ItemRequestHeaderSize) return false;
        var r = new PacketReader(payload);
        var couponItemId = r.ReadUInt32();
        var couponCount = r.ReadUInt16();
        var count = r.ReadUInt16();
        if (payload.Length < ItemRequestHeaderSize + count * MyCampEntrySize) return false;
        var entries = new List<MyCampRecycleEntry>(count);
        for (var i = 0; i < count; i++)
        {
            var uid = r.ReadUInt64();
            var itemId = r.ReadUInt32();
            entries.Add(new MyCampRecycleEntry(uid, unchecked((ushort)itemId)));
        }
        request = new MyCampRecycleRequest(couponItemId, couponCount, entries);
        return true;
    }

    public static byte[] MyCampRefused(ushort code)
    {
        var w = new PacketWriter(6);
        w.WriteUInt16(Refused);
        w.WriteUInt16(code);
        w.WriteUInt16((ushort)RecycleKind.MyCamp);
        return w.ToArray();
    }

    public static byte[] MyCampListTime(DateTime localNow)
    {
        var w = new PacketWriter(MyCampListTimeSize);
        w.WriteUInt64(unchecked((ulong)DateTime.SpecifyKind(localNow, DateTimeKind.Utc).ToFileTimeUtc()));
        return w.ToArray();
    }

    public static byte[] MyCampListReady(string ownerName, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(ownerName);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(24 + ownerName.Length * 2);
        w.WriteByte(0);
        w.WriteNulTerminated(string.Empty, text);
        w.WriteNulTerminated(string.Empty, text);
        w.WriteNulTerminated(ownerName, text);
        w.WriteBytes(new byte[MyCampListReadyTimeSize]);
        w.WriteUInt16(0);
        return w.ToArray();
    }

    public static byte[] ItemsRefused()
    {
        var w = new PacketWriter(6);
        w.WriteUInt16(Refused);
        w.WriteUInt16(ItemRefusalCode);
        w.WriteUInt16(ItemRefusalKind);
        return w.ToArray();
    }

    public static byte[] Opened(RecycleKind kind) => OkWithKind(kind);

    public static byte[] Closed(RecycleKind kind) => OkWithKind(kind);

    public static bool TryReadClose(ReadOnlySpan<byte> payload, out ushort kind)
    {
        kind = 0;
        if (payload.Length < CloseRequestSize) return false;
        kind = new PacketReader(payload).ReadUInt16();
        return true;
    }

    public static bool TryReadCouponExchange(ReadOnlySpan<byte> payload, out CouponExchangeRequest request)
    {
        request = default;
        if (payload.Length < CouponExchangeRequestSize) return false;
        var r = new PacketReader(payload);
        request = new CouponExchangeRequest(r.ReadUInt32(), r.ReadUInt64(), r.ReadUInt16(), r.ReadUInt32(), r.ReadUInt64(), r.ReadUInt16());
        return true;
    }

    public static byte[] Exchanged(IReadOnlyList<BagOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        var w = new PacketWriter(3 + operations.Count * 24);
        w.WriteUInt16(Ok);
        w.WriteByte((byte)operations.Count);
        foreach (var operation in operations) w.WriteBytes(QuestResultFrames.ItemOperation(operation));
        return w.ToArray();
    }

    private static byte[] OkWithKind(RecycleKind kind)
    {
        var w = new PacketWriter(4);
        w.WriteUInt16(Ok);
        w.WriteUInt16((ushort)kind);
        return w.ToArray();
    }
}
