using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class Packet05DDTbd
{
    public const int PayloadSize = 0;

    public static byte[] EncodePayload() => Array.Empty<byte>();

    public static Packet05DDTbd DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"Packet05DDTbd payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new Packet05DDTbd();
    }
}
