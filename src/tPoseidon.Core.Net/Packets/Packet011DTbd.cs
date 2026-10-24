using System.Buffers.Binary;

namespace tPoseidon.Core.Net.Packets;

public sealed class Packet011DTbd
{
    public const int PayloadSize = 5;

    public required byte Toggle { get; init; }
    public required uint CombatSequence { get; init; }

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        buf[0] = Toggle;
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(1, 4), CombatSequence);
        return buf;
    }

}
