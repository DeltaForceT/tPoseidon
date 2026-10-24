using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

[WireOpcode((ushort)Opcode.S2C_EQUIP_ITEM_ACK)]
public sealed class EquipItemPacket
{
    public const int PayloadSize = 9;

    public required uint ItemUid { get; init; }
    public uint ClassTag { get; init; } = 1;
    public required byte SlotPosition { get; init; }

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        buf[0] = (byte)(ItemUid & 0xFF);
        buf[1] = (byte)((ItemUid >> 8) & 0xFF);
        buf[2] = (byte)((ItemUid >> 16) & 0xFF);
        buf[3] = (byte)((ItemUid >> 24) & 0xFF);
        buf[4] = (byte)(ClassTag & 0xFF);
        buf[5] = (byte)((ClassTag >> 8) & 0xFF);
        buf[6] = (byte)((ClassTag >> 16) & 0xFF);
        buf[7] = (byte)((ClassTag >> 24) & 0xFF);
        buf[8] = SlotPosition;
        return buf;
    }

    public static EquipItemPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"EquipItem payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new EquipItemPacket
        {
            ItemUid = (uint)(payload[0]
                    | (payload[1] << 8)
                    | (payload[2] << 16)
                    | (payload[3] << 24)),
            ClassTag = (uint)(payload[4]
                    | (payload[5] << 8)
                    | (payload[6] << 16)
                    | (payload[7] << 24)),
            SlotPosition = payload[8],
        };
    }
}
