using System.Buffers.Binary;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class EntityTickPacket
{
    public const int PayloadSize = 3;

    public required ushort EntityId { get; init; }
    public byte Flag { get; init; }

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0, 2), EntityId);
        buf[2] = Flag;
        return buf;
    }

    public static EntityTickPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"EntityTick payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new EntityTickPacket
        {
            EntityId = BinaryPrimitives.ReadUInt16LittleEndian(payload[..2]),
            Flag = payload[2],
        };
    }
}
