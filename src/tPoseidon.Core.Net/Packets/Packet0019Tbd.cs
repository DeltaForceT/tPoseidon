using System.Buffers.Binary;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class Packet0019Tbd
{
    public const int PayloadSize = 7;

    public const byte StateFlagHalt = 0x01;

    public required ushort EntityId { get; init; }
    public required ushort WorldX { get; init; }
    public required ushort WorldY { get; init; }
    public required byte StateFlag { get; init; }

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0, 2), EntityId);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(2, 2), WorldX);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(4, 2), WorldY);
        buf[6] = StateFlag;
        return buf;
    }

    public static Packet0019Tbd DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"Packet0019Tbd payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new Packet0019Tbd
        {
            EntityId = BinaryPrimitives.ReadUInt16LittleEndian(payload[0..2]),
            WorldX = BinaryPrimitives.ReadUInt16LittleEndian(payload[2..4]),
            WorldY = BinaryPrimitives.ReadUInt16LittleEndian(payload[4..6]),
            StateFlag = payload[6],
        };
    }
}
