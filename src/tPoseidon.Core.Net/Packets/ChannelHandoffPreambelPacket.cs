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
}
