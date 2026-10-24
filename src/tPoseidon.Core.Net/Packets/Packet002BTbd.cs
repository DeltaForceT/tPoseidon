using System.Buffers.Binary;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class Packet002BTbd
{
    public const int PayloadSize = 3;

    public const byte EndFlagInstant = 0x00;
    public const byte EndFlagContinuous = 0x03;

    public required ushort EntityId { get; init; }
    public required byte EndFlag { get; init; }

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0, 2), EntityId);
        buf[2] = EndFlag;
        return buf;
    }

    public static Packet002BTbd DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"Packet002BTbd payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new Packet002BTbd
        {
            EntityId = BinaryPrimitives.ReadUInt16LittleEndian(payload[0..2]),
            EndFlag = payload[2],
        };
    }
}
