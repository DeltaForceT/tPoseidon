using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class XpOrGoldDeltaPacket
{
    public const int PayloadSize = 6;

        public byte Header { get; init; } = 0x01;

        public byte ResourceType { get; init; }

        public uint Value { get; init; }

    public byte[] EncodePayload()
    {
        var w = new PacketWriter(PayloadSize);
        w.WriteByte(Header);
        w.WriteByte(ResourceType);
        w.WriteUInt32(Value);
        return w.ToArray();
    }

    public static XpOrGoldDeltaPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"XpOrGoldDelta payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        var r = new PacketReader(payload);
        return new XpOrGoldDeltaPacket
        {
            Header = r.ReadByte(),
            ResourceType = r.ReadByte(),
            Value = r.ReadUInt32(),
        };
    }
}
