using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class GroundItemSpawnPacket
{
    public const int PayloadSize = 19;

    public required ushort OwnerEntityId { get; init; }
    public required ushort GroundEntityId { get; init; }
    public required uint ItemIndex { get; init; }

        public byte TrailingFlag { get; init; } = 1;

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        buf[0] = (byte)(OwnerEntityId & 0xFF);
        buf[1] = (byte)((OwnerEntityId >> 8) & 0xFF);
        buf[2] = (byte)(GroundEntityId & 0xFF);
        buf[3] = (byte)((GroundEntityId >> 8) & 0xFF);
        buf[4] = (byte)(ItemIndex & 0xFF);
        buf[5] = (byte)((ItemIndex >> 8) & 0xFF);
        buf[6] = (byte)((ItemIndex >> 16) & 0xFF);
        buf[7] = (byte)((ItemIndex >> 24) & 0xFF);

        buf[17] = TrailingFlag;

        return buf;
    }

    public static GroundItemSpawnPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"GroundItemSpawn payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new GroundItemSpawnPacket
        {
            OwnerEntityId = (ushort)(payload[0] | (payload[1] << 8)),
            GroundEntityId = (ushort)(payload[2] | (payload[3] << 8)),
            ItemIndex = (uint)(payload[4] | (payload[5] << 8) | (payload[6] << 16) | (payload[7] << 24)),
            TrailingFlag = payload[17],
        };
    }
}
