using System.Buffers.Binary;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class Packet001BTbd
{
    public const int PayloadSize = 7;

    public const byte StateFlagHalt = 0x01;
    public const byte StateFlagCombatStance = 0x02;

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

    public static Packet001BTbd DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"Packet001BTbd payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new Packet001BTbd
        {
            EntityId = BinaryPrimitives.ReadUInt16LittleEndian(payload[0..2]),
            WorldX = BinaryPrimitives.ReadUInt16LittleEndian(payload[2..4]),
            WorldY = BinaryPrimitives.ReadUInt16LittleEndian(payload[4..6]),
            StateFlag = payload[6],
        };
    }
}
