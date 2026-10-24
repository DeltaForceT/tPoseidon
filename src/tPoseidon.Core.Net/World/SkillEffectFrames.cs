using tPoseidon.Core.Common;
using tPoseidon.Core.Game.Skills.Effects;

namespace tPoseidon.Core.Net.World;

public static class SkillEffectFrames
{
    public const byte EvictedNotice = 4;
    public const int StatChangeSize = 6;
    public const int EndingSize = 2;
    public const int EndedSize = 4;
    private const byte OneStat = 1;
    private const byte VisibleFlag = 0;
    private const byte FailedFlag = 1;
    private const int LongestTailSize = 9;

    public static byte[] Started(SkillEffect effect, ushort targetEntityId)
    {
        ArgumentNullException.ThrowIfNull(effect);
        var header = SkillFrames.EffectHeader(
            effect.CasterEntityId, effect.OwnerEntityId, (ushort)effect.Kind.ActionId, effect.EsClass,
            (ushort)effect.SkillId, effect.Kind.HeaderState, effect.ObjectId);
        var tail = new PacketWriter(LongestTailSize);
        switch (effect.Kind.Tail)
        {
            case SkillEffectTail.SecondsThenMode:
                tail.WriteUInt16(unchecked((ushort)(effect.DurationMs / SkillEffect.MillisecondsPerSecond)));
                break;
            case SkillEffectTail.TargetThenMode:
                tail.WriteUInt16(targetEntityId);
                break;
            case SkillEffectTail.Duration:
                tail.WriteInt32(effect.DurationMs);
                break;
            case SkillEffectTail.FlagThenDuration:
                tail.WriteByte(effect.IsApplied ? VisibleFlag : FailedFlag);
                tail.WriteInt32(effect.DurationMs);
                break;
            case SkillEffectTail.ModeThenThreshold:
                tail.WriteByte((byte)effect.Mode);
                tail.WriteUInt64(BitConverter.DoubleToUInt64Bits(effect.Threshold));
                break;
            case SkillEffectTail.ModeRadiusPoint:
                tail.WriteByte((byte)effect.Mode);
                tail.WriteUInt16Field(effect.Magnitude);
                tail.WriteUInt16(effect.Point?.X ?? 0);
                tail.WriteUInt16(effect.Point?.Y ?? 0);
                break;
            case SkillEffectTail.RadiusOnly:
                tail.WriteInt32Field(effect.Magnitude);
                break;
            case SkillEffectTail.ModeThenStrings:
                tail.WriteByte((byte)effect.Mode);
                foreach (var text in effect.TailStrings)
                {
                    tail.WriteBytes(text);
                    tail.WriteByte(0);
                }
                break;
            case SkillEffectTail.ModeThenPoint:
                tail.WriteByte((byte)effect.Mode);
                tail.WriteUInt16(effect.Point?.X ?? 0);
                tail.WriteUInt16(effect.Point?.Y ?? 0);
                break;
            case SkillEffectTail.ModeThenDuration:
                tail.WriteByte((byte)effect.Mode);
                tail.WriteInt32(effect.DurationMs);
                break;
            case SkillEffectTail.IntervalThenHeal:
                tail.WriteInt32(effect.TickMs);
                tail.WriteInt32Field(effect.Magnitude);
                break;
            case SkillEffectTail.ModeThenTarget:
                tail.WriteByte((byte)effect.Mode);
                tail.WriteUInt16(targetEntityId);
                break;
            case SkillEffectTail.CurrentThenMax:
                tail.WriteInt32Field(effect.Charges);
                tail.WriteInt32Field(effect.Magnitude);
                break;
            case SkillEffectTail.ChargeCount:
                tail.WriteUInt16Field(effect.Charges);
                break;
            case SkillEffectTail.RadiusPercent:
                tail.WriteUInt32(BitConverter.SingleToUInt32Bits(effect.TailFloat));
                break;
            case SkillEffectTail.ModeThenAmount:
                tail.WriteByte((byte)effect.Mode);
                tail.WriteInt32Field(effect.Magnitude);
                break;
        }
        if (effect.Kind.ModeLast) tail.WriteByte((byte)effect.Mode);
        return [.. header, .. tail.ToArray()];
    }

    public const int ShieldGaugeSize = 10;
    public const int ResistRecordSize = 3;

    public static byte[] MorphStats(IReadOnlyList<(int Stat, int Value)> stats)
    {
        ArgumentNullException.ThrowIfNull(stats);
        var w = new PacketWriter(1 + (StatChangeSize - 1) * stats.Count);
        w.WriteByte((byte)stats.Count);
        foreach (var (stat, value) in stats)
        {
            w.WriteByte((byte)stat);
            w.WriteInt32(value);
        }
        return w.ToArray();
    }

    public static byte[] StatsChanged(IReadOnlyList<(int Stat, int Delta)> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var w = new PacketWriter(1 + (StatChangeSize - 1) * changes.Count);
        w.WriteByte((byte)changes.Count);
        foreach (var (stat, delta) in changes)
        {
            w.WriteByte((byte)stat);
            w.WriteInt32(delta);
        }
        return w.ToArray();
    }

    public static IReadOnlyList<(int Stat, int Delta)> ReadStatsChanged(ReadOnlySpan<byte> payload)
    {
        var r = new PacketReader(payload);
        var count = r.ReadByte();
        var changes = new List<(int Stat, int Delta)>(count);
        for (var index = 0; index < count; index++) changes.Add((r.ReadByte(), r.ReadInt32()));
        if (r.Remaining != 0)
        {
            throw new InvalidPacketException($"The stat change payload has {r.Remaining} bytes after its {count} changes.");
        }
        return changes;
    }

    public static byte[] ShieldGauge(ushort objectId, long current, long max)
    {
        var w = new PacketWriter(ShieldGaugeSize);
        w.WriteUInt16(objectId);
        w.WriteInt32Field(current);
        w.WriteInt32Field(max);
        return w.ToArray();
    }

    public static byte[] ResistChanged(IReadOnlyList<(byte Kind, short Value)> resists)
    {
        ArgumentNullException.ThrowIfNull(resists);
        var w = new PacketWriter(1 + ResistRecordSize * resists.Count);
        w.WriteByte((byte)resists.Count);
        foreach (var (kind, value) in resists)
        {
            w.WriteByte(kind);
            w.WriteInt16(value);
        }
        return w.ToArray();
    }

    public static byte[] StatChanged(int stat, int delta)
    {
        var w = new PacketWriter(StatChangeSize);
        w.WriteByte(OneStat);
        w.WriteByte((byte)stat);
        w.WriteInt32(delta);
        return w.ToArray();
    }

    public static byte[] Ending(int actionId)
    {
        var w = new PacketWriter(EndingSize);
        w.WriteUInt16((ushort)actionId);
        return w.ToArray();
    }

    public static byte[] Ended(ushort ownerEntityId, ushort objectId)
    {
        var w = new PacketWriter(EndedSize);
        w.WriteUInt16(ownerEntityId);
        w.WriteUInt16(objectId);
        return w.ToArray();
    }

    public static byte[] Evicted(int skillId) => SkillFrames.Notice(EvictedNotice, (ushort)skillId, 0);
}
