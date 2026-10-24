using System.Buffers.Binary;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class Packet00F2Tbd
{
    public const int PayloadSize = 9;

    public const byte ConstAt02 = 0x02;
    public const byte ConstAt05 = 0x01;
    public const byte ConstAt06 = 0x01;

    public required ushort PlayerEntityId { get; init; }
    public required byte FieldAt02Tbd { get; init; }
    public required ushort FieldAt03Tbd { get; init; }
    public required byte FieldAt05Tbd { get; init; }
    public required byte FieldAt06Tbd { get; init; }
    public required ushort TargetEntityId { get; init; }

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0, 2), PlayerEntityId);
        buf[2] = FieldAt02Tbd;
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(3, 2), FieldAt03Tbd);
        buf[5] = FieldAt05Tbd;
        buf[6] = FieldAt06Tbd;
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(7, 2), TargetEntityId);
        return buf;
    }

    public static Packet00F2Tbd DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"Packet00F2Tbd payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new Packet00F2Tbd
        {
            PlayerEntityId = BinaryPrimitives.ReadUInt16LittleEndian(payload[0..2]),
            FieldAt02Tbd = payload[2],
            FieldAt03Tbd = BinaryPrimitives.ReadUInt16LittleEndian(payload[3..5]),
            FieldAt05Tbd = payload[5],
            FieldAt06Tbd = payload[6],
            TargetEntityId = BinaryPrimitives.ReadUInt16LittleEndian(payload[7..9]),
        };
    }
}
