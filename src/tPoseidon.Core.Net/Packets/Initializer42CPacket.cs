using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class Initializer42CPacket
{
    public const int PayloadSize = 164;

    public byte FillByte { get; init; }

    public static readonly Initializer42CPacket AllZero = new();

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        if (FillByte != 0)
        {
            Array.Fill(buf, FillByte);
        }
        return buf;
    }

    public static Initializer42CPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"Initializer42C payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new Initializer42CPacket { FillByte = payload[0] };
    }
}
