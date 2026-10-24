using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

[WireOpcode((ushort)Opcode.S2C_ADD_TO_INVENTORY)]
public sealed class AddToInventoryPacket
{
    public const int PayloadSize = 17;

    public required uint ItemIndex { get; init; }
    public required uint ItemUid { get; init; }

    public byte ClassByte { get; init; } = 4;
    public byte FlagByte { get; init; } = 1;
    public uint OpaqueU32 { get; init; } = 1;
    public ushort Stack { get; init; } = 1;

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        buf[0] = ClassByte;
        buf[1] = FlagByte;
        WriteU32(buf, 2, ItemIndex);
        WriteU32(buf, 6, ItemUid);
        WriteU32(buf, 10, OpaqueU32);

        buf[14] = (byte)(Stack & 0xFF);
        buf[15] = (byte)((Stack >> 8) & 0xFF);

        return buf;
    }

    public static AddToInventoryPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"AddToInventory payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new AddToInventoryPacket
        {
            ClassByte = payload[0],
            FlagByte = payload[1],
            ItemIndex = ReadU32(payload, 2),
            ItemUid = ReadU32(payload, 6),
            OpaqueU32 = ReadU32(payload, 10),
            Stack = (ushort)(payload[14] | (payload[15] << 8)),
        };
    }

    private static void WriteU32(byte[] buf, int o, uint v)
    {
        buf[o] = (byte)(v & 0xFF);
        buf[o + 1] = (byte)((v >> 8) & 0xFF);
        buf[o + 2] = (byte)((v >> 16) & 0xFF);
        buf[o + 3] = (byte)((v >> 24) & 0xFF);
    }

    private static uint ReadU32(ReadOnlySpan<byte> buf, int o) =>
        (uint)(buf[o] | (buf[o + 1] << 8) | (buf[o + 2] << 16) | (buf[o + 3] << 24));
}
