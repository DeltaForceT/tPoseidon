using System.Buffers.Binary;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class Packet0406Tbd
{
    public const int PayloadSize = 4;

    public const uint MagicSoulDefault = 0x0000000E;

    public required uint PostAnimCode { get; init; }

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0, 4), PostAnimCode);
        return buf;
    }

    public static Packet0406Tbd DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"Packet0406Tbd payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new Packet0406Tbd
        {
            PostAnimCode = BinaryPrimitives.ReadUInt32LittleEndian(payload[0..4]),
        };
    }
}
