using System.Buffers.Binary;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class Packet00F0Tbd
{
    public const int PayloadSize = 3;

    public required ushort FieldAt00Tbd { get; init; }
    public required byte FieldAt02Tbd { get; init; }

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0, 2), FieldAt00Tbd);
        buf[2] = FieldAt02Tbd;
        return buf;
    }

    public static Packet00F0Tbd DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"Packet00F0Tbd payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new Packet00F0Tbd
        {
            FieldAt00Tbd = BinaryPrimitives.ReadUInt16LittleEndian(payload[0..2]),
            FieldAt02Tbd = payload[2],
        };
    }
}
