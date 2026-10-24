using tPoseidon.Core.Data;
using tPoseidon.Core.Net;

namespace tPoseidon.Core.InterServer;

public sealed record GuardianOrderChange(ulong GuardianUid, int SkillId, byte Slot, bool Remove);

public sealed record GuardianStatusUpdate(ulong GuardianUid, short Level, int SkillPoint, IReadOnlyList<short> StatPoints);

public sealed record GuardianStateUpdate(ulong GuardianUid, int Exp, int Gauge);

public sealed record GuardianCardSave(ulong GuardianUid, int SkillPoint, int UpdateCount, IReadOnlyList<GuardianSkill> Cards, EquipStatusUpgrade Items);

public sealed record GuardianRename(ulong GuardianUid, string Name, EquipStatusUpgrade Item);

public static class GuardianMessages
{
    public static byte[] EncodeLogin(GuardianLogin login)
    {
        ArgumentNullException.ThrowIfNull(login);
        var w = new PacketWriter(256);
        w.WriteByte(login.Awakened ? (byte)1 : (byte)0);
        w.WriteInt16(login.SlotMaxCount);
        w.WriteUInt16((ushort)login.Guardians.Count);
        foreach (var guardian in login.Guardians)
        {
            w.WriteUInt64(guardian.GuardianUid);
            w.WriteInt16(guardian.GuardianType);
            w.WriteByte(guardian.SlotNum);
            w.WriteByte(guardian.Equipped ? (byte)1 : (byte)0);
            w.WriteString(guardian.Name);
            w.WriteByte(guardian.RareType);
        }
        WriteOptionalState(w, login.Equipped);
        return w.ToArray();
    }

    public static GuardianLogin DecodeLogin(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        var awakened = r.ReadByte() != 0;
        var slotMax = r.ReadInt16();
        var count = r.ReadUInt16();
        var guardians = new List<GuardianSummary>(count);
        for (var i = 0; i < count; i++)
        {
            guardians.Add(new GuardianSummary(r.ReadUInt64(), r.ReadInt16(), r.ReadByte(), r.ReadByte() != 0, r.ReadString(), r.ReadByte()));
        }
        return new GuardianLogin(awakened, slotMax, guardians, ReadOptionalState(ref r));
    }

    public static byte[] EncodeState(GuardianState? state)
    {
        var w = new PacketWriter(128);
        WriteOptionalState(w, state);
        return w.ToArray();
    }

    public static GuardianState? DecodeState(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        return ReadOptionalState(ref r);
    }

    public static byte[] EncodeRename(GuardianRename rename)
    {
        ArgumentNullException.ThrowIfNull(rename);
        var w = new PacketWriter(96);
        w.WriteUInt64(rename.GuardianUid);
        w.WriteString(rename.Name);
        w.WriteBytes(EquipStatusUpgradeMessages.EncodeRequest(rename.Item));
        return w.ToArray();
    }

    public static GuardianRename DecodeRename(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        var guardianUid = r.ReadUInt64();
        var name = r.ReadString();
        return new GuardianRename(guardianUid, name, EquipStatusUpgradeMessages.DecodeRequest(body[r.Position..]));
    }

    public static byte[] EncodeCardSave(GuardianCardSave save)
    {
        ArgumentNullException.ThrowIfNull(save);
        var w = new PacketWriter(32 + save.Cards.Count * 9);
        w.WriteUInt64(save.GuardianUid);
        w.WriteInt32(save.SkillPoint);
        w.WriteInt32(save.UpdateCount);
        w.WriteByte((byte)save.Cards.Count);
        foreach (var card in save.Cards)
        {
            w.WriteInt32(card.SkillId);
            w.WriteByte(card.FType);
            w.WriteInt16(card.Level);
            w.WriteByte(card.Slot);
        }
        w.WriteBytes(EquipStatusUpgradeMessages.EncodeRequest(save.Items));
        return w.ToArray();
    }

    public static GuardianCardSave DecodeCardSave(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        var guardianUid = r.ReadUInt64();
        var skillPoint = r.ReadInt32();
        var updateCount = r.ReadInt32();
        var cards = new GuardianSkill[r.ReadByte()];
        for (var i = 0; i < cards.Length; i++)
        {
            cards[i] = new GuardianSkill(r.ReadInt32(), GuardianSkillType.Card, r.ReadByte(), r.ReadInt16(), r.ReadByte());
        }
        return new GuardianCardSave(guardianUid, skillPoint, updateCount, cards, EquipStatusUpgradeMessages.DecodeRequest(body[r.Position..]));
    }

    public static byte[] EncodeUid(ulong guardianUid)
    {
        var w = new PacketWriter(sizeof(ulong));
        w.WriteUInt64(guardianUid);
        return w.ToArray();
    }

    public static ulong DecodeUid(ReadOnlySpan<byte> body) => new PacketReader(body).ReadUInt64();

    public static byte[] EncodeOrder(GuardianOrderChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        var w = new PacketWriter(16);
        w.WriteUInt64(change.GuardianUid);
        w.WriteInt32(change.SkillId);
        w.WriteByte(change.Slot);
        w.WriteByte(change.Remove ? (byte)1 : (byte)0);
        return w.ToArray();
    }

    public static GuardianOrderChange DecodeOrder(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        return new GuardianOrderChange(r.ReadUInt64(), r.ReadInt32(), r.ReadByte(), r.ReadByte() != 0);
    }

    public static byte[] EncodeStatus(GuardianStatusUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);
        var w = new PacketWriter(40);
        w.WriteUInt64(update.GuardianUid);
        w.WriteInt16(update.Level);
        w.WriteInt32(update.SkillPoint);
        w.WriteByte((byte)update.StatPoints.Count);
        foreach (var point in update.StatPoints) w.WriteInt16(point);
        return w.ToArray();
    }

    public static GuardianStatusUpdate DecodeStatus(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        var uid = r.ReadUInt64();
        var level = r.ReadInt16();
        var skillPoint = r.ReadInt32();
        var points = new short[r.ReadByte()];
        for (var i = 0; i < points.Length; i++) points[i] = r.ReadInt16();
        return new GuardianStatusUpdate(uid, level, skillPoint, points);
    }

    public static byte[] EncodeStateUpdate(GuardianStateUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);
        var w = new PacketWriter(16);
        w.WriteUInt64(update.GuardianUid);
        w.WriteInt32(update.Exp);
        w.WriteInt32(update.Gauge);
        return w.ToArray();
    }

    public static GuardianStateUpdate DecodeStateUpdate(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        return new GuardianStateUpdate(r.ReadUInt64(), r.ReadInt32(), r.ReadInt32());
    }

    private static void WriteOptionalState(PacketWriter w, GuardianState? state)
    {
        w.WriteByte(state is null ? (byte)0 : (byte)1);
        if (state is null) return;
        w.WriteUInt64(state.GuardianUid);
        w.WriteByte(state.SlotNum);
        w.WriteInt16(state.GuardianType);
        w.WriteString(state.Name);
        w.WriteInt16(state.Level);
        w.WriteByte(state.FType);
        w.WriteByte(state.RareType);
        w.WriteByte(state.TitleIndex);
        w.WriteByte(state.PtsP);
        w.WriteByte(state.PtsM);
        w.WriteByte(state.PtsS);
        w.WriteByte(state.PtsC);
        w.WriteByte((byte)state.StatPoints.Count);
        foreach (var point in state.StatPoints) w.WriteInt16(point);
        w.WriteInt32(state.Exp);
        w.WriteInt32(state.Gauge);
        w.WriteInt32(state.SkillPoint);
        w.WriteUInt16((ushort)state.Skills.Count);
        foreach (var skill in state.Skills)
        {
            w.WriteInt32(skill.SkillId);
            w.WriteByte((byte)skill.Type);
            w.WriteByte(skill.FType);
            w.WriteInt16(skill.Level);
            w.WriteByte(skill.Slot);
        }
    }

    private static GuardianState? ReadOptionalState(ref PacketReader r)
    {
        if (r.ReadByte() == 0) return null;
        var uid = r.ReadUInt64();
        var slot = r.ReadByte();
        var type = r.ReadInt16();
        var name = r.ReadString();
        var level = r.ReadInt16();
        var fType = r.ReadByte();
        var rare = r.ReadByte();
        var title = r.ReadByte();
        var p = r.ReadByte();
        var m = r.ReadByte();
        var s = r.ReadByte();
        var c = r.ReadByte();
        var points = new short[r.ReadByte()];
        for (var i = 0; i < points.Length; i++) points[i] = r.ReadInt16();
        var exp = r.ReadInt32();
        var gauge = r.ReadInt32();
        var skillPoint = r.ReadInt32();
        var skills = new GuardianSkill[r.ReadUInt16()];
        for (var i = 0; i < skills.Length; i++)
        {
            skills[i] = new GuardianSkill(r.ReadInt32(), (GuardianSkillType)r.ReadByte(), r.ReadByte(), r.ReadInt16(), r.ReadByte());
        }
        return new GuardianState(uid, slot, type, name, level, fType, rare, title, p, m, s, c, points, exp, gauge, skillPoint, skills);
    }
}
