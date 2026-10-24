using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class SessionOpenPacket
{
    public const int PayloadSize = 2;

        public static byte[] EncodePayload() => new byte[] { 0x00, 0x00 };

    public static SessionOpenPacket DecodePayload(ReadOnlySpan<byte> payload)
    {

        

        if (payload.Length is not (0 or 2))
        {
            throw new InvalidPacketException(
                $"SessionOpen payload must be 0 or 2 bytes, got {payload.Length}.");
        }
        return new SessionOpenPacket();
    }
}
