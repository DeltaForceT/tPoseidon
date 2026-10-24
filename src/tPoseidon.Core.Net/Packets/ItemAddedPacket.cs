using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

[WireOpcode((ushort)Opcode.S2C_ITEM_ADDED)]
public sealed class ItemAddedPacket
{
    public const int PayloadSize = 18;

        public ushort Count { get; init; } = 1;

        public byte ItemClass { get; init; }

        public byte SlotKind { get; init; } = 1;

        public required int ItemIndex { get; init; }

        public required uint ItemUid { get; init; }

        public uint Stack { get; init; } = 1;

        public ushort Trailer { get; init; } = 1;

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        buf[0] = (byte)(Count & 0xFF);
        buf[1] = (byte)((Count >> 8) & 0xFF);
        buf[2] = ItemClass;
        buf[3] = SlotKind;
        buf[4] = (byte)(ItemIndex & 0xFF);
        buf[5] = (byte)((ItemIndex >> 8) & 0xFF);
        buf[6] = (byte)((ItemIndex >> 16) & 0xFF);
        buf[7] = (byte)((ItemIndex >> 24) & 0xFF);
        buf[8] = (byte)(ItemUid & 0xFF);
        buf[9] = (byte)((ItemUid >> 8) & 0xFF);
        buf[10] = (byte)((ItemUid >> 16) & 0xFF);
        buf[11] = (byte)((ItemUid >> 24) & 0xFF);
        buf[12] = (byte)(Stack & 0xFF);
        buf[13] = (byte)((Stack >> 8) & 0xFF);
        buf[14] = (byte)((Stack >> 16) & 0xFF);
        buf[15] = (byte)((Stack >> 24) & 0xFF);
        buf[16] = (byte)(Trailer & 0xFF);
        buf[17] = (byte)((Trailer >> 8) & 0xFF);
        return buf;
    }

    public static ItemAddedPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"ItemAdded payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new ItemAddedPacket
        {
            Count = (ushort)(payload[0] | (payload[1] << 8)),
            ItemClass = payload[2],
            SlotKind = payload[3],
            ItemIndex = payload[4] | (payload[5] << 8) | (payload[6] << 16) | (payload[7] << 24),
            ItemUid = (uint)(payload[8] | (payload[9] << 8) | (payload[10] << 16) | (payload[11] << 24)),
            Stack = (uint)(payload[12] | (payload[13] << 8) | (payload[14] << 16) | (payload[15] << 24)),
            Trailer = (ushort)(payload[16] | (payload[17] << 8)),
        };
    }
}
