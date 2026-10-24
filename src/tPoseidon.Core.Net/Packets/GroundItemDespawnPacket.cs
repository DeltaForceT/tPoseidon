using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

[WireOpcode((ushort)Opcode.S2C_GROUND_ITEM_DESPAWN)]
public sealed class GroundItemDespawnPacket
{
    public const int PayloadSize = 3;

    public required ushort GroundEntityId { get; init; }
    public byte Reason { get; init; }

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        buf[0] = (byte)(GroundEntityId & 0xFF);
        buf[1] = (byte)((GroundEntityId >> 8) & 0xFF);
        buf[2] = Reason;
        return buf;
    }

    public static GroundItemDespawnPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"GroundItemDespawn payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new GroundItemDespawnPacket
        {
            GroundEntityId = (ushort)(payload[0] | (payload[1] << 8)),
            Reason = payload[2],
        };
    }
}
