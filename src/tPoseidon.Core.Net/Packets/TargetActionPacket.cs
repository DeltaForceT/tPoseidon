using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class TargetActionPacket
{
    public const int PayloadSize = 3;

    public ushort EntityId { get; init; }

        public byte ActionFlag { get; init; } = 0x02;

    public byte[] EncodePayload()
    {
        var w = new PacketWriter(PayloadSize);
        w.WriteUInt16(EntityId);
        w.WriteByte(ActionFlag);
        return w.ToArray();
    }

    public static TargetActionPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"TargetAction payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        var r = new PacketReader(payload);
        return new TargetActionPacket
        {
            EntityId = r.ReadUInt16(),
            ActionFlag = r.ReadByte(),
        };
    }
}

public sealed class TargetMarkPacket
{
    public const int PayloadSize = 2;

    public ushort EntityId { get; init; }

    public byte[] EncodePayload()
    {
        var w = new PacketWriter(PayloadSize);
        w.WriteUInt16(EntityId);
        return w.ToArray();
    }

    public static TargetMarkPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"TargetMark payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        var r = new PacketReader(payload);
        return new TargetMarkPacket { EntityId = r.ReadUInt16() };
    }
}
