using tPoseidon.Core.Common;

namespace tPoseidon.Kr2014.Wire;

[WireOpcode((ushort)Opcode.SESSION_OPEN)]
public sealed class SessionOpenPacket
{
    public const int PayloadSize = 2;

    public static byte[] EncodePayload() => new byte[] { 0x00, 0x00 };

    public static SessionOpenPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"Kr2014 SessionOpen payload must be {PayloadSize} bytes, got {payload.Length}.");
        }
        return new SessionOpenPacket();
    }
}
