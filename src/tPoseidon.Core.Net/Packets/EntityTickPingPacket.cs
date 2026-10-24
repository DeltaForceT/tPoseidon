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

}
