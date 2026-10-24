using System.Buffers.Binary;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class TargetReleasePacket
{
    public const int PayloadSize = 2;

    public required ushort EntityId { get; init; }

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0, 2), EntityId);
        return buf;
    }

    public static TargetReleasePacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"TargetRelease payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new TargetReleasePacket
        {
            EntityId = BinaryPrimitives.ReadUInt16LittleEndian(payload[..2]),
        };
    }
}
