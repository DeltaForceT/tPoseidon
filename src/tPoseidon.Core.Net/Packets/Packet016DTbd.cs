using System.Buffers.Binary;
using tPoseidon.Core.Common;

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

    public static Packet016DTbd DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"Packet016DTbd payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new Packet016DTbd
        {
            TargetEntityId = BinaryPrimitives.ReadUInt16LittleEndian(payload[0..2]),
            CasterEntityId = BinaryPrimitives.ReadUInt16LittleEndian(payload[2..4]),
        };
    }
}
