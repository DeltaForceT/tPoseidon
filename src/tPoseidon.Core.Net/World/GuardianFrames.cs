using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Guardian;

namespace tPoseidon.Core.Net.World;

public readonly record struct GuardianOrderRequest(int SkillId, int Second, bool Remove);

public static class GuardianFrames
{
    public const int RecordSize = 0x1F6;
    public const int SummarySize = 0x2B;
    public const int NameBytes = 30;
    public const int SlotBytes = 9;
    public const int CardGroupSize = 8;
    public const int CardSlotCount = 32;
    public const int SpecialSlotCount = 5;
    public const int OrderSlotCount = 6;
    public const int StatCount = 12;
    public const byte EquippedMarker = 1;
    public const int OrderRequestSize = 9;
    public const int EquipRequestSize = sizeof(ulong);
    public const byte RemoveFlag = 1;
    public const int CardSaveHeaderSize = 8;
    public const int CardSaveRecordSize = 0x14;

    public const int FTypeOffset = 0x1E;
    public const int RareTypeOffset = 0x1F;
    public const int TitleIndexOffset = 0x20;
    public const int PointsOffset = 0x21;
    public const int UidOffset = 0x25;
    public const int TypeOffset = 0x2D;
    public const int SlotOffset = 0x2F;
    public const int MarkerOffset = 0x30;
    public const int LevelOffset = 0x50;
    public const int SkillPointOffset = 0x52;
    public const int StatsOffset = 0x53;
    public const int ExpOffset = 0x6B;
    public const int GaugeOffset = 0x6F;
    public const int CardsOffset = 0x73;
    public const int SpecialsOffset = 0x193;
    public const int OrderOffset = 0x1C0;

    public const uint OrderNotListed = 2;

    public static byte[] Record(GuardianState? state, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(encoding);
        var record = new byte[RecordSize];
        if (state is null) return record;
        Name(encoding, state.Name).CopyTo(record, 0);
        record[FTypeOffset] = state.FType;
        record[RareTypeOffset] = state.RareType;
        record[TitleIndexOffset] = state.TitleIndex;
        record[PointsOffset] = state.PtsP;
        record[PointsOffset + 1] = state.PtsM;
        record[PointsOffset + 2] = state.PtsS;
        record[PointsOffset + 3] = state.PtsC;
        BitConverter.TryWriteBytes(record.AsSpan(UidOffset), state.GuardianUid);
        BitConverter.TryWriteBytes(record.AsSpan(TypeOffset), state.GuardianType);
        record[SlotOffset] = state.SlotNum;
        record[MarkerOffset] = EquippedMarker;
        BitConverter.TryWriteBytes(record.AsSpan(LevelOffset), state.Level);
        record[SkillPointOffset] = (byte)Math.Clamp(state.SkillPoint, byte.MinValue, byte.MaxValue);
        for (var i = 0; i < StatCount && i < state.StatPoints.Count; i++)
        {
            BitConverter.TryWriteBytes(record.AsSpan(StatsOffset + i * sizeof(short)), state.StatPoints[i]);
        }
        BitConverter.TryWriteBytes(record.AsSpan(ExpOffset), state.Exp);
        BitConverter.TryWriteBytes(record.AsSpan(GaugeOffset), state.Gauge);
        foreach (var skill in state.Skills)
        {
            if (SlotOffsetOf(skill) is { } offset) Slot(skill).CopyTo(record, offset);
        }
        return record;
    }

    public static int? SlotOffsetOf(GuardianSkill skill)
    {
        ArgumentNullException.ThrowIfNull(skill);
        return skill.Type switch
        {
            GuardianSkillType.Card when skill.Slot < CardGroupSize && skill.FType * CardGroupSize + skill.Slot < CardSlotCount
                => CardsOffset + (skill.FType * CardGroupSize + skill.Slot) * SlotBytes,
            GuardianSkillType.Special when skill.Slot < SpecialSlotCount => SpecialsOffset + skill.Slot * SlotBytes,
            GuardianSkillType.Order when skill.Slot < OrderSlotCount => OrderOffset + skill.Slot * SlotBytes,
            _ => null,
        };
    }

    public static byte[] Slot(GuardianSkill skill)
    {
        ArgumentNullException.ThrowIfNull(skill);
        var w = new PacketWriter(SlotBytes);
        w.WriteInt32(skill.SkillId);
        w.WriteByte((byte)skill.Type);
        w.WriteByte(skill.FType);
        w.WriteInt16(skill.Level);
        w.WriteByte(skill.Slot);
        return w.ToArray();
    }

    public static byte[] List(short slotMaxCount, IReadOnlyList<GuardianSummary> guardians, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(guardians);
        ArgumentNullException.ThrowIfNull(encoding);
        var count = Math.Min(guardians.Count, byte.MaxValue);
        var w = new PacketWriter(2 + count * SummarySize);
        w.WriteByte((byte)Math.Clamp(slotMaxCount, byte.MinValue, byte.MaxValue));
        w.WriteByte((byte)count);
        for (var i = 0; i < count; i++)
        {
            var guardian = guardians[i];
            w.WriteUInt64(guardian.GuardianUid);
            w.WriteInt16(guardian.GuardianType);
            w.WriteByte(guardian.SlotNum);
            w.WriteByte(guardian.Equipped ? EquippedMarker : (byte)0);
            w.WriteBytes(Name(encoding, guardian.Name));
            w.WriteByte(guardian.RareType);
        }
        return w.ToArray();
    }

    public static byte[] LevelUp(int level, int exp, int skillPoint)
    {
        var w = new PacketWriter(12);
        w.WriteInt32(level);
        w.WriteInt32(exp);
        w.WriteInt32(skillPoint);
        return w.ToArray();
    }

    public static byte[] LevelUpEffect(ushort guardianEntityId)
    {
        var w = new PacketWriter(sizeof(ushort));
        w.WriteUInt16(guardianEntityId);
        return w.ToArray();
    }

    public static byte[] ExpGained(ushort guardianEntityId, int gained)
    {
        var w = new PacketWriter(6);
        w.WriteUInt16(guardianEntityId);
        w.WriteInt32(gained);
        return w.ToArray();
    }

    public static byte[] OrderChanged(GuardianSkill slot, bool cleared)
    {
        ArgumentNullException.ThrowIfNull(slot);
        var w = new PacketWriter(SlotBytes + 1);
        w.WriteBytes(Slot(slot));
        w.WriteByte(cleared ? RemoveFlag : (byte)0);
        return w.ToArray();
    }

    public static bool TryReadCardSave(ReadOnlySpan<byte> payload, out GuardianCardSaveRequest request)
    {
        request = default;
        if (payload.Length < CardSaveHeaderSize) return false;
        var r = new PacketReader(payload);
        var skillPoint = r.ReadUInt32();
        var count = r.ReadUInt32();
        if (count > (uint)((payload.Length - CardSaveHeaderSize) / CardSaveRecordSize)) return false;
        var records = new GuardianCardRecord[count];
        for (var i = 0; i < records.Length; i++)
        {
            records[i] = new GuardianCardRecord(r.ReadInt32(), r.ReadUInt64(), r.ReadInt32(), r.ReadInt32());
        }
        request = new GuardianCardSaveRequest(skillPoint, records);
        return true;
    }

    public static byte[] CardsSaved(IReadOnlyList<BagOperation> items, ulong guardianUid, IReadOnlyList<GuardianSkill?> reserved, IReadOnlyList<GuardianSkill> order)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(reserved);
        ArgumentNullException.ThrowIfNull(order);
        var w = new PacketWriter(1 + items.Count * 24 + sizeof(ulong) + (CardSlotCount + OrderSlotCount) * SlotBytes);
        w.WriteByte((byte)items.Count);
        foreach (var item in items) w.WriteBytes(QuestResultFrames.ItemOperation(item));
        w.WriteUInt64(guardianUid);
        var cards = new byte[CardSlotCount * SlotBytes];
        for (var i = 0; i < reserved.Count && i < CardSlotCount; i++)
        {
            if (reserved[i] is { } card) Slot(card).CopyTo(cards, i * SlotBytes);
        }
        w.WriteBytes(cards);
        var orders = new byte[OrderSlotCount * SlotBytes];
        foreach (var slot in order)
        {
            if (slot.Slot < OrderSlotCount) Slot(slot).CopyTo(orders, slot.Slot * SlotBytes);
        }
        w.WriteBytes(orders);
        return w.ToArray();
    }

    public static byte[] OrderRefused(uint code)
    {
        var w = new PacketWriter(sizeof(uint));
        w.WriteUInt32(code);
        return w.ToArray();
    }

    public static byte[] Empty() => [];

    public static bool TryReadOrder(ReadOnlySpan<byte> payload, out GuardianOrderRequest request)
    {
        request = default;
        if (payload.Length < OrderRequestSize) return false;
        var r = new PacketReader(payload);
        request = new GuardianOrderRequest(r.ReadInt32(), r.ReadInt32(), r.ReadByte() == RemoveFlag);
        return true;
    }

    public static bool TryReadEquip(ReadOnlySpan<byte> payload, out ulong guardianUid)
    {
        guardianUid = 0;
        if (payload.Length < EquipRequestSize) return false;
        guardianUid = new PacketReader(payload).ReadUInt64();
        return true;
    }

    public static bool TryReadRename(ReadOnlySpan<byte> payload, Encoding encoding, out ulong itemWireUid, out string name)
    {
        ArgumentNullException.ThrowIfNull(encoding);
        itemWireUid = 0;
        name = string.Empty;
        if (payload.Length < sizeof(ulong)) return false;
        itemWireUid = new PacketReader(payload).ReadUInt64();
        var text = payload[sizeof(ulong)..];
        var end = text.IndexOf((byte)0);
        var field = end < 0 ? text : text[..end];
        name = encoding.GetString(field[..Math.Min(field.Length, NameBytes - 1)]);
        return true;
    }

    public static byte[] Renamed(ulong guardianUid, string name, BagOperation itemChange, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(encoding);
        var w = new PacketWriter(64);
        w.WriteUInt64(guardianUid);
        w.WriteNulTerminated(name, encoding);
        w.WriteBytes(QuestResultFrames.ItemOperation(itemChange));
        return w.ToArray();
    }

    private static byte[] Name(Encoding encoding, string name)
    {
        var field = new byte[NameBytes];
        var bytes = encoding.GetBytes(name ?? string.Empty);
        Array.Copy(bytes, field, Math.Min(bytes.Length, NameBytes - 1));
        return field;
    }
}
