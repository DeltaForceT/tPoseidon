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
}
