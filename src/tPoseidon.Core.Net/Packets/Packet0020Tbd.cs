using System.Buffers.Binary;

namespace tPoseidon.Core.Net.Packets;

public sealed class Packet0020Tbd
{
    public const int PayloadSize = 2;

    public required ushort EntityId { get; init; }

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0, 2), EntityId);
        return buf;
    }
}
