using tPoseidon.Core.Common;
using tPoseidon.Core.Net;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Kr2014.Wire;

[WireOpcode((ushort)Opcode.GAME_CHAR_STATS)]
public sealed class CharInfoPacket
{
    public const int PayloadSize = 278;
    public const int EquipSlotCount = 23;
    public const int EquipSlotBytes = 8;
    public const int StatBlockOffset = EquipSlotCount * EquipSlotBytes;
    public const int StateRecordOffset = StatBlockOffset + CharacterStatBlock.Size;
    public const int StateRecordSize = 61;

    public const ushort StateRecordLead = 1;
    public const byte FirstHairSlot = 1;
    public const ushort StarterWarehouseSlots = 80;

    private const int StateTailOffset = 0x36;
    private const int BytesBeforeStateTail = 0x22;

    public IReadOnlyList<EquipSlot> EquipSlots { get; init; } = Array.Empty<EquipSlot>();
    public required CharacterStatBlock Stats { get; init; }

    public ushort MapId { get; init; }
    public int PositionX { get; init; }
    public int PositionY { get; init; }
    public ushort ReturnMapId { get; init; }
    public uint Money { get; init; }
    public int Hp { get; init; }
    public int Mp { get; init; }
    public uint TmExp { get; init; }
    public uint Exp { get; init; }

    public byte Hair { get; init; }
    public byte HairSlot { get; init; } = FirstHairSlot;
    public ushort WarehouseSlots { get; init; } = StarterWarehouseSlots;

    public readonly record struct EquipSlot(uint ItemUid, uint Flags);

    public readonly record struct Placement(ushort MapId, int X, int Y);

    public static Placement ReadPlacement(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException($"The char info payload is {PayloadSize} bytes, got {payload.Length}.");
        }
        var record = payload[(StateRecordOffset + sizeof(ushort))..];
        return new Placement(
            System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(record),
            System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(record[sizeof(ushort)..]),
            System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(record[(sizeof(ushort) + sizeof(int))..]));
    }

    public static CharInfoPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException($"The char info payload is {PayloadSize} bytes, got {payload.Length}.");
        }
        var r = new PacketReader(payload);
        var slots = new EquipSlot[EquipSlotCount];
        for (var i = 0; i < EquipSlotCount; i++) slots[i] = new EquipSlot(r.ReadUInt32(), r.ReadUInt32());
        var stats = CharacterStatBlock.Read(ref r);
        r.ReadUInt16();
        var mapId = r.ReadUInt16();
        var x = r.ReadInt32();
        var y = r.ReadInt32();
        var returnMapId = r.ReadUInt16();
        var money = r.ReadUInt32();
        var hp = r.ReadInt32();
        var mp = r.ReadInt32();
        var tmExp = r.ReadUInt32();
        var exp = r.ReadUInt32();
        r.Skip(StateTailOffset - BytesBeforeStateTail);
        var hair = r.ReadByte();
        r.ReadByte();
        var hairSlot = r.ReadByte();
        var warehouseSlots = r.ReadUInt16();
        return new CharInfoPacket
        {
            EquipSlots = slots,
            Stats = stats,
            MapId = mapId,
            PositionX = x,
            PositionY = y,
            ReturnMapId = returnMapId,
            Money = money,
            Hp = hp,
            Mp = mp,
            TmExp = tmExp,
            Exp = exp,
            Hair = hair,
            HairSlot = hairSlot,
            WarehouseSlots = warehouseSlots,
        };
    }

    public byte[] EncodePayload()
    {
        if (EquipSlots.Count > EquipSlotCount)
        {
            throw new InvalidOperationException(
                $"A character has {EquipSlotCount} equipment slots, got {EquipSlots.Count}.");
        }

        var w = new PacketWriter(PayloadSize);
        for (var i = 0; i < EquipSlotCount; i++)
        {
            var slot = i < EquipSlots.Count ? EquipSlots[i] : default;
            w.WriteUInt32(slot.ItemUid);
            w.WriteUInt32(slot.Flags);
        }

        Stats.WriteTo(w);

        w.WriteUInt16(StateRecordLead);
        w.WriteUInt16(MapId);
        w.WriteInt32(PositionX);
        w.WriteInt32(PositionY);
        w.WriteUInt16(ReturnMapId);
        w.WriteUInt32(Money);
        w.WriteInt32(Hp);
        w.WriteInt32(Mp);
        w.WriteUInt32(TmExp);
        w.WriteUInt32(Exp);
        w.WriteBytes(new byte[StateTailOffset - BytesBeforeStateTail]);
        w.WriteByte(Hair);
        w.WriteByte(0);
        w.WriteByte(HairSlot);
        w.WriteUInt16(WarehouseSlots);
        w.WriteByte(0);
        w.WriteByte(0);

        var payload = w.ToArray();
        if (payload.Length != PayloadSize)
        {
            throw new InvalidOperationException($"The char info payload is {PayloadSize} bytes, built {payload.Length}.");
        }
        return payload;
    }
}
