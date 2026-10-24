using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

[WireOpcode((ushort)Opcode.ENTITY_ID_ECHO)]
public sealed class EntityIdEchoPacket
{
    public const int PayloadSize = 4;

        public uint EntityId { get; init; }

    public byte[] EncodePayload()
    {
        var w = new PacketWriter(PayloadSize);
        w.WriteUInt32(EntityId);
        return w.ToArray();
    }

    public static EntityIdEchoPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"EntityIdEcho payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        var r = new PacketReader(payload);
        return new EntityIdEchoPacket { EntityId = r.ReadUInt32() };
    }
}
