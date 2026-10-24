using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class ListCountPacket
{
    public const int PayloadSize = 2;

    public ushort Count { get; init; }

    public static readonly ListCountPacket Empty = new() { Count = 0 };

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        buf[0] = (byte)(Count & 0xFF);
        buf[1] = (byte)((Count >> 8) & 0xFF);
        return buf;
    }

    public static ListCountPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"ListCount payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new ListCountPacket
        {
            Count = (ushort)(payload[0] | (payload[1] << 8)),
        };
    }
}
