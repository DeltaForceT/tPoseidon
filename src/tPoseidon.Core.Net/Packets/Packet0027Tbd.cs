using System.Buffers.Binary;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class Packet0027Tbd
{
    public const int PayloadSize = 45;

    public required byte[] StatBlock33B { get; init; }
    public required uint X { get; init; }
    public required uint Y { get; init; }
    public required uint HpCurrent { get; init; }

    public byte[] EncodePayload()
    {
        if (StatBlock33B.Length != 33)
        {
            throw new InvalidPacketException(
                $"StatBlock33B must be 33 bytes, got {StatBlock33B.Length}");
        }
        var buf = new byte[PayloadSize];
        StatBlock33B.AsSpan().CopyTo(buf.AsSpan(0, 33));
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(33, 4), X);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(37, 4), Y);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(41, 4), HpCurrent);
        return buf;
    }

    public static Packet0027Tbd DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"Packet0027Tbd payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new Packet0027Tbd
        {
            StatBlock33B = payload[0..33].ToArray(),
            X = BinaryPrimitives.ReadUInt32LittleEndian(payload[33..37]),
            Y = BinaryPrimitives.ReadUInt32LittleEndian(payload[37..41]),
            HpCurrent = BinaryPrimitives.ReadUInt32LittleEndian(payload[41..45]),
        };
    }
}
