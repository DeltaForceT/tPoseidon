using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class EntityRefreshPacket
{
    public const int PayloadSize = 2;

    public ushort EntityId { get; init; }

    public byte[] EncodePayload()
    {
        var w = new PacketWriter(PayloadSize);
        w.WriteUInt16(EntityId);
        return w.ToArray();
    }

    public static EntityRefreshPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"EntityRefresh payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        var r = new PacketReader(payload);
        return new EntityRefreshPacket
        {
            EntityId = r.ReadUInt16(),
        };
    }
}
