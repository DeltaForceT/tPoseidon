using System.Buffers.Binary;

namespace tPoseidon.Core.Net.Packets;

public sealed class Packet016DTbd
{
    public const int PayloadSize = 4;

    public required ushort TargetEntityId { get; init; }
    public required ushort CasterEntityId { get; init; }

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0, 2), TargetEntityId);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(2, 2), CasterEntityId);
        return buf;
    }

}
