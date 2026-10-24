using System.Buffers.Binary;
using tPoseidon.Core.Common;

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

    public static Packet0020Tbd DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"Packet0020Tbd payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new Packet0020Tbd
        {
            EntityId = BinaryPrimitives.ReadUInt16LittleEndian(payload[0..2]),
        };
    }
}
