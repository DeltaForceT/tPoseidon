using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class DeathConfirmPacket
{
    public const int PayloadSize = 3;

    public required ushort EntityId { get; init; }
    public byte Flag { get; init; }

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        buf[0] = (byte)(EntityId & 0xFF);
        buf[1] = (byte)((EntityId >> 8) & 0xFF);
        buf[2] = Flag;
        return buf;
    }

    public static DeathConfirmPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"DeathConfirm payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new DeathConfirmPacket
        {
            EntityId = (ushort)(payload[0] | (payload[1] << 8)),
            Flag = payload[2],
        };
    }
}
