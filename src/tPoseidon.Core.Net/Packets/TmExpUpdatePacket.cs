using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class TmExpUpdatePacket
{
    public const int PayloadSize = 5;

    public byte OpaqueAt0x00Tbd { get; init; }
    public required uint TmExp { get; init; }

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        buf[0] = OpaqueAt0x00Tbd;
        buf[1] = (byte)(TmExp & 0xFF);
        buf[2] = (byte)((TmExp >> 8) & 0xFF);
        buf[3] = (byte)((TmExp >> 16) & 0xFF);
        buf[4] = (byte)((TmExp >> 24) & 0xFF);
        return buf;
    }

    public static TmExpUpdatePacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"TmExpUpdate payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new TmExpUpdatePacket
        {
            OpaqueAt0x00Tbd = payload[0],
            TmExp = (uint)(payload[1] | (payload[2] << 8) | (payload[3] << 16) | (payload[4] << 24)),
        };
    }
}
