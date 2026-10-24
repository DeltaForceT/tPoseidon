using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

[WireOpcode((ushort)Opcode.GAME_UNK_99)]
public sealed class ChannelHandoffPreambelPacket
{
    public const int PayloadSize = 6;

    public byte Tag { get; init; } = 0x99;

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        buf[0] = Tag;
        return buf;
    }

    public static ChannelHandoffPreambelPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
            throw new InvalidPacketException($"ChannelHandoffPreambel must be {PayloadSize} bytes, got {payload.Length}.");
        return new ChannelHandoffPreambelPacket { Tag = payload[0] };
    }
}
