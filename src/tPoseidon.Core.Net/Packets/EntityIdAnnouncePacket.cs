using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

[WireOpcode((ushort)Opcode.GAME_ENTITY_ANNOUNCE_3D1)]
[WireOpcode((ushort)Opcode.S2C_PROFILE_TITLE)]
public sealed class EntityIdAnnouncePacket
{
    public ushort EntityId { get; init; }

        public int PadBytes { get; init; } = 10;

    public byte[] EncodePayload()
    {
        if (PadBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(PadBytes), PadBytes,
                "PadBytes must be non-negative.");
        }

        var w = new PacketWriter(2 + PadBytes);
        w.WriteUInt16(EntityId);
        w.WriteZero(PadBytes);
        return w.ToArray();
    }

    public static EntityIdAnnouncePacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 2)
        {
            throw new InvalidPacketException(
                $"EntityIdAnnounce payload too short: {payload.Length} bytes, need >= 2.");
        }
        var r = new PacketReader(payload);
        return new EntityIdAnnouncePacket
        {
            EntityId = r.ReadUInt16(),
            PadBytes = payload.Length - 2,
        };
    }
}
