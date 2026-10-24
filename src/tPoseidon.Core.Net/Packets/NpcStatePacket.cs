using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class NpcStatePacket
{
    public const int PayloadSize = 3;

        public ushort EntityId { get; init; }

        public byte State { get; init; } = 0xA0;

    public byte[] EncodePayload()
    {
        var w = new PacketWriter(PayloadSize);
        w.WriteUInt16(EntityId);
        w.WriteByte(State);
        return w.ToArray();
    }

    public static NpcStatePacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"NpcState payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        var r = new PacketReader(payload);
        return new NpcStatePacket
        {
            EntityId = r.ReadUInt16(),
            State = r.ReadByte(),
        };
    }
}
