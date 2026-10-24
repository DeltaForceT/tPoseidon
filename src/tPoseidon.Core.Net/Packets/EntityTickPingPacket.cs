using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

[WireOpcode((ushort)Opcode.S2C_OP_003F_TBD)]
public sealed class EntityTickPingPacket
{
    public const int PayloadSize = 3;

    public const byte ReservedByte = 0x00;

    public ushort EntityId { get; init; }

    public byte[] EncodePayload()
    {
        var w = new PacketWriter(PayloadSize);
        w.WriteUInt16(EntityId);
        w.WriteByte(ReservedByte);
        return w.ToArray();
    }

    public static EntityTickPingPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"EntityTickPing payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        var r = new PacketReader(payload);
        return new EntityTickPingPacket
        {
            EntityId = r.ReadUInt16(),
        };
    }
}
