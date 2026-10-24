using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

[WireOpcode((ushort)Opcode.ENTITY_MOVE)]
public sealed class EntityMovementPacket
{
    public const int PayloadSize = 11;

    public ushort EntityId { get; init; }
    public ushort FromX { get; init; }
    public ushort FromY { get; init; }
    public ushort ToX { get; init; }
    public ushort ToY { get; init; }

        public byte Flag { get; init; }

    public byte[] EncodePayload()
    {
        var w = new PacketWriter(PayloadSize);
        w.WriteUInt16(EntityId);
        w.WriteUInt16(FromX);
        w.WriteUInt16(FromY);
        w.WriteUInt16(ToX);
        w.WriteUInt16(ToY);
        w.WriteByte(Flag);
        return w.ToArray();
    }

    public static EntityMovementPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"EntityMovement payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        var r = new PacketReader(payload);
        return new EntityMovementPacket
        {
            EntityId = r.ReadUInt16(),
            FromX = r.ReadUInt16(),
            FromY = r.ReadUInt16(),
            ToX = r.ReadUInt16(),
            ToY = r.ReadUInt16(),
            Flag = r.ReadByte(),
        };
    }
}
