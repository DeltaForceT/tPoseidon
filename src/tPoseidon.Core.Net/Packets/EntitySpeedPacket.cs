using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class EntitySpeedPacket
{
    public const int PayloadSize = 3;

        public const byte RunSpeedQuarter = 0x2E;

        public const byte WalkSpeedQuarter = 0x1C;

    public ushort EntityId { get; init; }

        public byte SpeedQuarter { get; init; }

    public byte[] EncodePayload()
    {
        return new byte[]
        {
            (byte)(EntityId & 0xFF),
            (byte)((EntityId >> 8) & 0xFF),
            SpeedQuarter,
        };
    }

    public static EntitySpeedPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"EntitySpeed payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new EntitySpeedPacket
        {
            EntityId = (ushort)(payload[0] | (payload[1] << 8)),
            SpeedQuarter = payload[2],
        };
    }
}
