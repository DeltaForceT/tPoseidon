using System.Buffers.Binary;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class GalderCumulativePacket
{
    public const int PayloadSize = 5;

    public required uint Cumulative { get; init; }

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        buf[0] = 0x00;
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(1, 4), Cumulative);
        return buf;
    }

    public static GalderCumulativePacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"GalderCumulative payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new GalderCumulativePacket
        {
            Cumulative = BinaryPrimitives.ReadUInt32LittleEndian(payload[1..5]),
        };
    }
}
