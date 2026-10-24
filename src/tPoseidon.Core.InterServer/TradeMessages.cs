using tPoseidon.Core.Data;
using tPoseidon.Core.Net;

namespace tPoseidon.Core.InterServer;

public static class TradeMessages
{
    public static byte[] EncodeTrade(TradeOffer first, TradeOffer second, int bagCapacity)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        var w = new PacketWriter(64);
        WriteOffer(w, first);
        WriteOffer(w, second);
        w.WriteInt32(bagCapacity);
        return w.ToArray();
    }

    public static (TradeOffer First, TradeOffer Second, int BagCapacity) DecodeTrade(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        var first = ReadOffer(ref r);
        var second = ReadOffer(ref r);
        return (first, second, r.ReadInt32());
    }

    public static byte[] EncodeResult(TradeSideResult first, TradeSideResult second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        var w = new PacketWriter(64);
        WriteSide(w, first);
        WriteSide(w, second);
        return w.ToArray();
    }

    public static TradeResult DecodeResult(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        var first = ReadSide(ref r);
        var second = ReadSide(ref r);
        return new TradeResult(TradeRules.Ok, first, second);
    }

    public static byte[] EncodeError(int error)
    {
        var w = new PacketWriter(4);
        w.WriteInt32(error);
        return w.ToArray();
    }

    public static int DecodeError(ReadOnlySpan<byte> body) => body.Length >= sizeof(int) ? new PacketReader(body).ReadInt32() : TradeRules.StoreFailed;

    private static void WriteOffer(PacketWriter w, TradeOffer offer)
    {
        w.WriteUInt64(offer.CharacterId);
        w.WriteUInt32(offer.Money);
        w.WriteByte((byte)offer.Slots.Count);
        foreach (var slot in offer.Slots)
        {
            w.WriteUInt64(slot.ItemUid);
            w.WriteInt32(slot.ItemIndex);
            w.WriteUInt16(slot.Count);
        }
    }

    private static TradeOffer ReadOffer(ref PacketReader r)
    {
        var characterId = r.ReadUInt64();
        var money = r.ReadUInt32();
        var count = r.ReadByte();
        var slots = new List<TradeSlot>(count);
        for (var i = 0; i < count; i++) slots.Add(new TradeSlot(r.ReadUInt64(), r.ReadInt32(), r.ReadUInt16()));
        return new TradeOffer(characterId, money, slots);
    }

    private static void WriteSide(PacketWriter w, TradeSideResult side)
    {
        w.WriteUInt64(side.CharacterId);
        w.WriteUInt32(side.Money);
        w.WriteUInt16((ushort)side.Changes.Count);
        foreach (var change in side.Changes)
        {
            w.WriteByte((byte)change.Kind);
            w.WriteByte(change.ItemClass);
            w.WriteInt32(change.ItemIndex);
            w.WriteUInt64(change.ItemUid);
            w.WriteUInt16(change.Count);
            w.WriteByte(change.MixSlot);
        }
    }

    private static TradeSideResult ReadSide(ref PacketReader r)
    {
        var characterId = r.ReadUInt64();
        var money = r.ReadUInt32();
        var count = r.ReadUInt16();
        var changes = new List<TradeItemChange>(count);
        for (var i = 0; i < count; i++)
        {
            changes.Add(new TradeItemChange((TradeChangeKind)r.ReadByte(), r.ReadByte(), r.ReadInt32(), r.ReadUInt64(), r.ReadUInt16(), r.ReadByte()));
        }
        return new TradeSideResult(characterId, money, changes);
    }
}
