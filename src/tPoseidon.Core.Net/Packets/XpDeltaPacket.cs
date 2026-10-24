using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class XpDeltaPacket
{
    public const int PayloadSize = 4;

    public required uint XpAmount { get; init; }

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        buf[0] = (byte)(XpAmount & 0xFF);
        buf[1] = (byte)((XpAmount >> 8) & 0xFF);
        buf[2] = (byte)((XpAmount >> 16) & 0xFF);
        buf[3] = (byte)((XpAmount >> 24) & 0xFF);
        return buf;
    }

    public static XpDeltaPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"XpDelta payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new XpDeltaPacket
        {
            XpAmount = (uint)(payload[0] | (payload[1] << 8) | (payload[2] << 16) | (payload[3] << 24)),
        };
    }
}
