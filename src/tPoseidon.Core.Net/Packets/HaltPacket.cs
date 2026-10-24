using System.Buffers.Binary;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class HaltPacket
{
    public const int PayloadSize = 7;
    public const byte HaltFlag = 0x01;

    public required ushort EntityId { get; init; }
    public required ushort X { get; init; }
    public required ushort Y { get; init; }
    public byte Flag { get; init; } = HaltFlag;

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0, 2), EntityId);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(2, 2), X);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(4, 2), Y);
        buf[6] = Flag;
        return buf;
    }

    public static HaltPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"Halt payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new HaltPacket
        {
            EntityId = BinaryPrimitives.ReadUInt16LittleEndian(payload[..2]),
            X = BinaryPrimitives.ReadUInt16LittleEndian(payload[2..4]),
            Y = BinaryPrimitives.ReadUInt16LittleEndian(payload[4..6]),
            Flag = payload[6],
        };
    }
}
