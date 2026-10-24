using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

[WireOpcode((ushort)Opcode.S2C_FORTUNE_APPLIED)]
public sealed class BatchCounterPacket3D7
{
    public const int PayloadSize = 229;

    public uint CounterValue { get; init; } = 1;
    public uint MagicConstant { get; init; } = 0xD62E5000u;
    public uint UnknownU32At8 { get; init; } = 65;
    public ulong Timestamp { get; init; }

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        WriteU32(buf, 0,  CounterValue);
        WriteU32(buf, 4,  MagicConstant);
        WriteU32(buf, 8,  UnknownU32At8);
        WriteU64(buf, 21, Timestamp);
        return buf;
    }

    public static BatchCounterPacket3D7 DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"BatchCounter3D7 payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new BatchCounterPacket3D7
        {
            CounterValue = ReadU32(payload, 0),
            MagicConstant = ReadU32(payload, 4),
            UnknownU32At8 = ReadU32(payload, 8),
            Timestamp = ReadU64(payload, 21),
        };
    }

    private static void WriteU32(byte[] buf, int o, uint v)
    {
        buf[o] = (byte)(v & 0xFF);
        buf[o + 1] = (byte)((v >> 8) & 0xFF);
        buf[o + 2] = (byte)((v >> 16) & 0xFF);
        buf[o + 3] = (byte)((v >> 24) & 0xFF);
    }
    private static void WriteU64(byte[] buf, int o, ulong v)
    {
        for (var i = 0; i < 8; i++) buf[o + i] = (byte)((v >> (i * 8)) & 0xFF);
    }
    private static uint ReadU32(ReadOnlySpan<byte> buf, int o) =>
        (uint)(buf[o] | (buf[o + 1] << 8) | (buf[o + 2] << 16) | (buf[o + 3] << 24));
    private static ulong ReadU64(ReadOnlySpan<byte> buf, int o)
    {
        ulong v = 0;
        for (var i = 0; i < 8; i++) v |= (ulong)buf[o + i] << (i * 8);
        return v;
    }
}
