using System.Text;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Net.World;

public readonly record struct TradeOfferSlot(ulong WireUid, uint ItemId, uint Count)
{
    public bool IsEmpty => WireUid == 0 && ItemId == 0 && Count == 0;
}

public readonly record struct TradeItemView(byte ItemClass, uint ItemId, ulong WireUid, ushort Count, byte RefineType, byte RefineLevel, byte RefineState, byte MixSlot);

public sealed record TradeRecordView(bool Ready, uint Money, IReadOnlyList<TradeItemView?> Slots);

public static class TradeFrames
{
    public const int SlotsPerSide = 6;
    public const int OfferSlotSize = 16;
    public const byte EmptySlot = 0;
    public const ushort NoTimedEntries = 0;
    public const ushort CodeOk = 0;
    public const ushort CodeAlreadyChecked = 5;
    public const ushort CodeBagMismatch = 6;
    public const ushort CodeNotExchangeable = 7;
    public const ushort CodeMoneyInvalid = 8;
    public const ushort CodeOppositeMoneyOver = 9;
    public const ushort CodeBadCount = 10;
    private const int OfferHeaderSize = 7;

    public static bool TryReadInvite(ReadOnlySpan<byte> body, out ushort targetEntityId)
    {
        targetEntityId = 0;
        if (body.Length < sizeof(ushort)) return false;
        targetEntityId = new PacketReader(body).ReadUInt16();
        return true;
    }

    public static bool TryReadUpdate(ReadOnlySpan<byte> body, out bool ready, out uint money, out IReadOnlyList<TradeOfferSlot> slots)
    {
        ready = false;
        money = 0;
        slots = Array.Empty<TradeOfferSlot>();
        if (body.Length < OfferHeaderSize) return false;
        var r = new PacketReader(body);
        ready = r.ReadByte() != 0;
        money = r.ReadUInt32();
        var count = Math.Min((int)r.ReadUInt16(), SlotsPerSide);
        if (body.Length < OfferHeaderSize + count * OfferSlotSize) return false;
        var read = new List<TradeOfferSlot>(count);
        for (var index = 0; index < count; index++) read.Add(new TradeOfferSlot(r.ReadUInt64(), r.ReadUInt32(), r.ReadUInt32()));
        slots = read;
        return true;
    }

    public static bool TryReadFlag(ReadOnlySpan<byte> body, out bool flag)
    {
        flag = false;
        if (body.Length < 1) return false;
        flag = body[0] != 0;
        return true;
    }

    public static byte[] Invite(ushort inviterEntityId, string inviterName, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(inviterName);
        var w = new PacketWriter(3 + inviterName.Length * 2);
        w.WriteUInt16(inviterEntityId);
        w.WriteNulTerminated(inviterName, encoding);
        return w.ToArray();
    }

    public static byte[] Update(TradeRecordView own, ushort ownExcess, TradeRecordView opposite, ushort oppositeExcess, ushort code)
    {
        ArgumentNullException.ThrowIfNull(own);
        ArgumentNullException.ThrowIfNull(opposite);
        var w = new PacketWriter(256);
        WriteRecord(w, own);
        w.WriteUInt16(ownExcess);
        WriteRecord(w, opposite);
        w.WriteUInt16(NoTimedEntries);
        w.WriteUInt16(oppositeExcess);
        w.WriteUInt16(code);
        return w.ToArray();
    }

    public static byte[] CheckState(bool isChecked, bool isFirstChecker) => new[] { isChecked ? (byte)1 : (byte)0, isFirstChecker ? (byte)1 : (byte)0 };

    public static byte[] Settled(long money, IReadOnlyList<BagOperation> operations)
    {
        var w = new PacketWriter(64);
        w.WriteUInt32Field(money);
        w.WriteBytes(QuestResultFrames.ItemOperations(operations));
        return w.ToArray();
    }

    public static (uint Money, IReadOnlyList<BagOperation> Operations) ReadSettled(ReadOnlySpan<byte> payload)
    {
        var money = new PacketReader(payload).ReadUInt32();
        return (money, QuestResultFrames.ReadItemOperations(payload[sizeof(uint)..]));
    }

    private static void WriteRecord(PacketWriter w, TradeRecordView record)
    {
        w.WriteByte(record.Ready ? (byte)1 : (byte)0);
        w.WriteUInt32Field(record.Money);
        w.WriteUInt16(SlotsPerSide);
        for (var index = 0; index < SlotsPerSide; index++)
        {
            if (index >= record.Slots.Count || record.Slots[index] is not { } item)
            {
                w.WriteByte(EmptySlot);
                continue;
            }
            w.WriteByte(item.ItemClass);
            w.WriteUInt32(item.ItemId);
            w.WriteUInt64(item.WireUid);
            w.WriteUInt16(item.Count);
            if (item.ItemClass != ItemParamEntry.RareClass) continue;
            w.WriteByte(item.RefineType);
            w.WriteByte(item.RefineLevel);
            w.WriteByte(item.RefineState);
            w.WriteByte(item.MixSlot);
        }
    }
}
