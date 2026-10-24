using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class EntityStateUpdatePacket
{
    public const int PayloadSize = 6;

    public ushort EntityId { get; init; }

        public byte StateA { get; init; }

        public byte StateB { get; init; }

        public byte Reserved4 { get; init; }

        public byte Flag { get; init; }

    public byte[] EncodePayload()
    {
        var w = new PacketWriter(PayloadSize);
        w.WriteUInt16(EntityId);
        w.WriteByte(StateA);
        w.WriteByte(StateB);
        w.WriteByte(Reserved4);
        w.WriteByte(Flag);
        return w.ToArray();
    }

    public static EntityStateUpdatePacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"EntityStateUpdate payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        var r = new PacketReader(payload);
        return new EntityStateUpdatePacket
        {
            EntityId = r.ReadUInt16(),
            StateA = r.ReadByte(),
            StateB = r.ReadByte(),
            Reserved4 = r.ReadByte(),
            Flag = r.ReadByte(),
        };
    }
}
