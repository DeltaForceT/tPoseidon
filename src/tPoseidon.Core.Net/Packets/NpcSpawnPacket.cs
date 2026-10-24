using System.Text;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

[WireOpcode((ushort)Opcode.NPC_SPAWN)]
public sealed class NpcSpawnPacket
{
        public enum EntityKind : byte
    {
                Npc = 0x02,

                Mob = 0x03,

                Pet = 0x04,

                Shop = 0x05,

                SkillShop = 0x0E,
    }

        public ushort EntityId { get; init; }

    public EntityKind Kind { get; init; } = EntityKind.Npc;

        public ushort TemplateId { get; init; }

        public ushort UnknownField6 { get; init; }

        public ushort UnknownField8 { get; init; }

        public ushort SpawnX { get; init; }

        public ushort SpawnY { get; init; }

        public ushort CurrentX { get; init; }

        public ushort CurrentY { get; init; }

        public string Name { get; init; } = string.Empty;

        public ReadOnlyMemory<byte> Trailer { get; init; }

    public const int TrailerSpeedOffset = 2;

    public byte TrailerSpeedQuarter => Trailer.Length > TrailerSpeedOffset ? Trailer.Span[TrailerSpeedOffset] : (byte)0;

        public byte ReservedByte3 { get; init; }

        public uint Reserved10 { get; init; }

        public byte[] EncodePayload()
    {
        ArgumentNullException.ThrowIfNull(Name);

        var nameBytes = Encoding.UTF8.GetBytes(Name);

        var trailer = Trailer.Span;
        var total = 22 + nameBytes.Length + 1 + trailer.Length;

        var w = new PacketWriter(total);
        w.WriteUInt16(EntityId);
        w.WriteByte((byte)Kind);
        w.WriteByte(ReservedByte3);
        w.WriteUInt16(TemplateId);
        w.WriteUInt16(UnknownField6);
        w.WriteUInt16(UnknownField8);
        w.WriteUInt32(Reserved10);
        w.WriteUInt16(SpawnX);
        w.WriteUInt16(SpawnY);
        w.WriteUInt16(CurrentX);
        w.WriteUInt16(CurrentY);
        w.WriteBytes(nameBytes);
        w.WriteByte(0);
        w.WriteBytes(trailer);

        return w.ToArray();
    }

        public static NpcSpawnPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        const int FixedPrefixSize = 22;
        if (payload.Length < FixedPrefixSize + 1)
        {
            throw new InvalidPacketException(
                $"NpcSpawn payload {payload.Length} bytes is below {FixedPrefixSize + 1}-byte minimum.");
        }

        var r = new PacketReader(payload);
        var entityId = r.ReadUInt16();
        var kind = (EntityKind)r.ReadByte();
        var reservedByte3 = r.ReadByte();
        var templateId = r.ReadUInt16();
        var unk6 = r.ReadUInt16();
        var unk8 = r.ReadUInt16();
        var reserved10 = r.ReadUInt32();
        var sx = r.ReadUInt16();
        var sy = r.ReadUInt16();
        var cx = r.ReadUInt16();
        var cy = r.ReadUInt16();

        var rest = payload[FixedPrefixSize..];
        var nul = rest.IndexOf((byte)0);
        if (nul < 0)
        {
            throw new InvalidPacketException(
                "NpcSpawn payload has no NUL terminator after name field.");
        }

        var name = Encoding.UTF8.GetString(rest[..nul]);
        var trailer = rest[(nul + 1)..].ToArray();

        return new NpcSpawnPacket
        {
            EntityId = entityId,
            Kind = kind,
            ReservedByte3 = reservedByte3,
            TemplateId = templateId,
            UnknownField6 = unk6,
            UnknownField8 = unk8,
            Reserved10 = reserved10,
            SpawnX = sx,
            SpawnY = sy,
            CurrentX = cx,
            CurrentY = cy,
            Name = name,
            Trailer = trailer,
        };
    }
}
