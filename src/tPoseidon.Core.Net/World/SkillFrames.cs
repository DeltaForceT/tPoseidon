using tPoseidon.Core.Common;
using tPoseidon.Core.Game.Combat;
using tPoseidon.Core.Game.Skills;

namespace tPoseidon.Core.Net.World;

public readonly record struct SkillRequest(ushort SkillId, byte Level);

public readonly record struct SkillTargetRequest(
    byte Kind, ushort SkillId, byte Level, ushort TargetType, ReadOnlyMemory<byte> Data)
{
    public ushort? TargetEntityId =>
        Data.Length >= 2 ? (ushort)(Data.Span[0] | (Data.Span[1] << 8)) : null;

    public (ushort X, ushort Y)? TargetPoint =>
        TargetType is SkillFrames.PositionTargetType or SkillFrames.RegionTargetType or SkillFrames.PlacementTargetType && Data.Length >= 4
            ? ((ushort)(Data.Span[0] | (Data.Span[1] << 8)), (ushort)(Data.Span[2] | (Data.Span[3] << 8)))
            : null;
}

public static class SkillFrames
{
    public const int UseRequestSize = 3;
    public const int TargetRequestHeaderSize = 8;
    public const int LongestTargetData = 0x80;
    public const byte SkillKind = 2;
    public const ushort ObjectTargetType = 1;
    public const ushort PositionTargetType = 3;
    public const ushort ThrowTargetType = 7;
    public const ushort GalderTargetType = 8;
    public const ushort RegionTargetType = 6;
    public const ushort PlacementTargetType = 10;
    public const byte PointTarget = 2;

    public const byte NoTarget = 0;
    public const byte EntityTarget = 1;
    public const byte SkillSource = 2;
    public const ushort NoEffectObject = 0;

    public const int RefusedSize = 2;
    public const int NoticeSize = 5;
    public const int TargetAskedSize = 4;
    public const int CompletedSize = 3;
    public const int EffectHeaderSize = 14;
    public const int HitResultSize = 30;
    public const int PositionTailSize = 10;
    public const int MonsterHpSize = 9;
    public const int DeathSize = 3;

    public const int SelectionDroppedSize = 4;

    public static byte? ReadSelectionDroppedKind(ReadOnlySpan<byte> payload) =>
        payload.Length == SelectionDroppedSize ? payload[0] : null;

    public static SkillRequest? ReadUse(ReadOnlySpan<byte> payload) =>
        payload.Length == UseRequestSize
            ? new SkillRequest((ushort)(payload[0] | (payload[1] << 8)), payload[2])
            : null;

    public static SkillTargetRequest? ReadTarget(ReadOnlyMemory<byte> payload)
    {
        if (payload.Length < TargetRequestHeaderSize) return null;
        var bytes = payload.Span;
        var dataLength = bytes[6] | (bytes[7] << 8);
        var data = payload[TargetRequestHeaderSize..];
        if (data.Length > LongestTargetData) return null;
        return new SkillTargetRequest(
            bytes[0],
            (ushort)(bytes[1] | (bytes[2] << 8)),
            bytes[3],
            (ushort)(bytes[4] | (bytes[5] << 8)),
            dataLength < data.Length ? data[..dataLength] : data);
    }

    public const int SkillRecordSize = 5;

    public static bool TryReadRecord(ReadOnlySpan<byte> payload, out ushort skillId, out byte level, out ushort points)
    {
        skillId = 0;
        level = 0;
        points = 0;
        if (payload.Length < SkillRecordSize) return false;
        skillId = (ushort)(payload[0] | (payload[1] << 8));
        level = payload[2];
        points = (ushort)(payload[3] | (payload[4] << 8));
        return true;
    }

    public static byte[] Record(ushort skillId, byte level, ushort points)
    {
        var w = new PacketWriter(SkillRecordSize);
        w.WriteUInt16(skillId);
        w.WriteByte(level);
        w.WriteUInt16(points);
        return w.ToArray();
    }

    public static byte[] LearnSeen(ushort playerEntityId)
    {
        var w = new PacketWriter(2);
        w.WriteUInt16(playerEntityId);
        return w.ToArray();
    }

    public static byte[] LevelSeen(ushort playerEntityId, ushort typeIndex, ushort level)
    {
        var w = new PacketWriter(6);
        w.WriteUInt16(playerEntityId);
        w.WriteUInt16(typeIndex);
        w.WriteUInt16(level);
        return w.ToArray();
    }

    public static byte[] Refused(ushort code)
    {
        var w = new PacketWriter(RefusedSize);
        w.WriteUInt16(code);
        return w.ToArray();
    }

    public static byte[] Notice(byte kind, ushort first, ushort second)
    {
        var w = new PacketWriter(NoticeSize);
        w.WriteByte(kind);
        w.WriteUInt16(first);
        w.WriteUInt16(second);
        return w.ToArray();
    }

    public static byte[] TargetAsked(ushort skillId, byte level)
    {
        var w = new PacketWriter(TargetAskedSize);
        w.WriteByte(SkillKind);
        w.WriteUInt16(skillId);
        w.WriteByte(level);
        return w.ToArray();
    }

    public static byte[] CastStarted(ushort casterEntityId, ushort skillId, byte level, ushort targetEntityId)
    {
        var w = new PacketWriter(9);
        w.WriteUInt16(casterEntityId);
        w.WriteByte(SkillKind);
        w.WriteUInt16(skillId);
        w.WriteByte(level);
        w.WriteByte(EntityTarget);
        w.WriteUInt16(targetEntityId);
        return w.ToArray();
    }

    public static byte[] CastStartedAt(ushort casterEntityId, ushort skillId, byte level, (ushort X, ushort Y) point)
    {
        var w = new PacketWriter(11);
        w.WriteUInt16(casterEntityId);
        w.WriteByte(SkillKind);
        w.WriteUInt16(skillId);
        w.WriteByte(level);
        w.WriteByte(PointTarget);
        w.WriteUInt16(point.X);
        w.WriteUInt16(point.Y);
        return w.ToArray();
    }

    public static byte[] PointTail((ushort X, ushort Y) point)
    {
        var w = new PacketWriter(4);
        w.WriteUInt16(point.X);
        w.WriteUInt16(point.Y);
        return w.ToArray();
    }

    public static byte[] ObjectVictims(ushort objectId, IReadOnlyList<ushort> victims)
    {
        ArgumentNullException.ThrowIfNull(victims);
        var w = new PacketWriter(3 + 2 * victims.Count);
        w.WriteUInt16(objectId);
        w.WriteByte((byte)victims.Count);
        foreach (var victim in victims) w.WriteUInt16(victim);
        return w.ToArray();
    }

    public static byte[] VisibilityMask(ushort entityId, uint mask)
    {
        var w = new PacketWriter(6);
        w.WriteUInt16(entityId);
        w.WriteUInt32(mask);
        return w.ToArray();
    }

    public static byte[] Revealed(bool added, IReadOnlyList<ushort> entityIds)
    {
        ArgumentNullException.ThrowIfNull(entityIds);
        var w = new PacketWriter(3 + 2 * entityIds.Count);
        w.WriteByte(added ? (byte)1 : (byte)0);
        w.WriteUInt16((ushort)entityIds.Count);
        foreach (var entityId in entityIds) w.WriteUInt16(entityId);
        return w.ToArray();
    }

    public static byte[] DetectorTail(bool found)
    {
        var w = new PacketWriter(3);
        w.WriteByte(found ? (byte)1 : (byte)0);
        if (found) w.WriteUInt16(0);
        return w.ToArray();
    }

    public static byte[] SelfCastStarted(ushort casterEntityId, ushort skillId, byte level)
    {
        var w = new PacketWriter(7);
        w.WriteUInt16(casterEntityId);
        w.WriteByte(SkillKind);
        w.WriteUInt16(skillId);
        w.WriteByte(level);
        w.WriteByte(NoTarget);
        return w.ToArray();
    }

    public static byte[] Completed(ushort skillId, byte level)
    {
        var w = new PacketWriter(CompletedSize);
        w.WriteUInt16(skillId);
        w.WriteByte(level);
        return w.ToArray();
    }

    public static byte[] EffectHeader(
        ushort casterEntityId, ushort targetEntityId, ushort actionId, sbyte effectClass, ushort skillId, ushort effectState,
        ushort effectObjectId = NoEffectObject)
    {
        var w = new PacketWriter(EffectHeaderSize);
        w.WriteUInt16(casterEntityId);
        w.WriteUInt16(targetEntityId);
        w.WriteUInt16(effectObjectId);
        w.WriteUInt16(actionId);
        w.WriteByte(SkillSource);
        w.WriteByte(unchecked((byte)effectClass));
        w.WriteUInt16(skillId);
        w.WriteUInt16(effectState);
        return w.ToArray();
    }

    public static byte[] HitResult(ushort targetEntityId, SkillHit hit)
    {
        var w = new PacketWriter(HitResultSize);
        w.WriteUInt16(targetEntityId);
        w.WriteInt32(hit.Damage);
        w.WriteInt32(hit.Type);
        w.WriteInt32(hit.Kind);
        w.WriteInt32(hit.Element);
        w.WriteInt32(hit.Flags);
        w.WriteInt32(0);
        w.WriteInt32(0);
        return w.ToArray();
    }

    public static IReadOnlyList<SwingFrame> EncodeHit(
        PlayerStrike strike,
        SkillDefinition skill,
        SkillHit hit,
        ushort blockId,
        SwingAdditions additions,
        IReadOnlyList<(Opcode Opcode, byte[] Payload)> afterBlock,
        bool casterTwice = false,
        byte[]? tail = null)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(additions);
        ArgumentNullException.ThrowIfNull(afterBlock);
        if (blockId == MeleeSwingFrames.NoBlock)
        {
            throw new ArgumentOutOfRangeException(nameof(blockId), "A skill effect needs a non-zero block id.");
        }

        var frames = new List<SwingFrame>();
        if (additions.OwnerTaken is { } ownerTaken)
        {
            frames.Add(new(Opcode.S2C_OP_016D_TBD, ownerTaken, MeleeSwingFrames.NoBlock, SwingAudience.Everyone));
        }
        var header = EffectHeader(
            strike.AttackerEntityId, casterTwice ? strike.AttackerEntityId : strike.TargetEntityId, (ushort)skill.ActionId,
            (sbyte)skill.Row.EsClass, (ushort)skill.Id, (ushort)skill.Row.EState);
        frames.Add(new(
            Opcode.S2C_OP_00E0_TBD,
            tail is null ? header : [.. header, .. tail],
            blockId,
            SwingAudience.Everyone));
        AppendVictim(frames, strike, HitResult(strike.TargetEntityId, hit), blockId, additions);
        AppendBlockEnd(frames, blockId, afterBlock);
        return frames;
    }

    public const int TornadoTailSize = 40;
    public const int TornadoHits = 3;
    public const uint TornadoResultFlag = 0x8000;

    public static IReadOnlyList<SwingFrame> EncodeTornado(
        PlayerStrike strike,
        SkillDefinition skill,
        TornadoOutcome outcome,
        ushort blockId,
        SwingAdditions additions,
        IReadOnlyList<(Opcode Opcode, byte[] Payload)> afterBlock)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(additions);
        ArgumentNullException.ThrowIfNull(afterBlock);
        var frames = new List<SwingFrame>();
        if (additions.OwnerTaken is { } ownerTaken)
        {
            frames.Add(new(Opcode.S2C_OP_016D_TBD, ownerTaken, MeleeSwingFrames.NoBlock, SwingAudience.Everyone));
        }
        var w = new PacketWriter(EffectHeaderSize + TornadoTailSize);
        w.WriteBytes(EffectHeader(strike.AttackerEntityId, strike.AttackerEntityId, (ushort)skill.ActionId, (sbyte)skill.Row.EsClass, (ushort)skill.Id, (ushort)skill.Row.EState));
        w.WriteUInt16(strike.AttackerX);
        w.WriteUInt16(strike.AttackerY);
        w.WriteUInt16(strike.TargetEntityId);
        w.WriteUInt16(strike.TargetX);
        w.WriteUInt16(strike.TargetY);
        foreach (var (damage, kind, third) in outcome.Slots)
        {
            w.WriteInt32(damage);
            w.WriteInt32(kind);
            w.WriteUInt16(third);
        }
        frames.Add(new(Opcode.S2C_OP_00E0_TBD, w.ToArray(), blockId, SwingAudience.Everyone));

        AppendVictim(frames, strike, SummaryResult(strike.TargetEntityId, outcome.Sum, outcome.ResultCount), blockId, additions);
        AppendBlockEnd(frames, blockId, afterBlock);
        return frames;
    }

    public static IReadOnlyList<SwingFrame> EncodePolarBearHits(
        PlayerStrike strike,
        SkillDefinition skill,
        PolarBearOutcome outcome,
        ushort blockId,
        SwingAdditions additions,
        IReadOnlyList<(Opcode Opcode, byte[] Payload)> afterBlock)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(additions);
        ArgumentNullException.ThrowIfNull(afterBlock);
        var frames = new List<SwingFrame>();
        if (additions.OwnerTaken is { } ownerTaken)
        {
            frames.Add(new(Opcode.S2C_OP_016D_TBD, ownerTaken, MeleeSwingFrames.NoBlock, SwingAudience.Everyone));
        }
        var header = EffectHeader(
            strike.AttackerEntityId, strike.AttackerEntityId, (ushort)skill.ActionId,
            (sbyte)skill.Row.EsClass, (ushort)skill.Id, (ushort)skill.Row.EState);
        var tail = PolarBearSkillFrames.HitsTail(strike.TargetEntityId, outcome.Hits.Select(hit => new PolarBearHit(hit.Damage, hit.Kind)).ToList());
        frames.Add(new(Opcode.S2C_OP_00E0_TBD, [.. header, .. tail], blockId, SwingAudience.Everyone));
        AppendVictim(frames, strike, SummaryResult(strike.TargetEntityId, outcome.Sum, outcome.Landed), blockId, additions);
        AppendBlockEnd(frames, blockId, afterBlock);
        return frames;
    }

    public const ushort ChainSlotEnd = 0xFFFF;
    public const int ChainSlotSize = 10;

    public static byte[] ChainTailBytes(
        ChainTail tail, ushort targetEntityId, (int X, int Y) casterAt, (int X, int Y) targetAt, IReadOnlyList<ChainVictim> victims)
    {
        ArgumentNullException.ThrowIfNull(victims);
        var w = new PacketWriter(16 + victims.Sum(victim => 4 + ChainSlotSize * victim.Hits.Count));
        switch (tail)
        {
            case ChainTail.TargetPointsThenSlots:
                w.WriteUInt16(targetEntityId);
                w.WriteUInt16((ushort)casterAt.X);
                w.WriteUInt16((ushort)casterAt.Y);
                w.WriteUInt16((ushort)targetAt.X);
                w.WriteUInt16((ushort)targetAt.Y);
                if (victims.Count > 0) WriteChainSlots(w, victims[0].Hits);
                break;
            case ChainTail.SlotsOnly:
                if (victims.Count > 0) WriteChainSlots(w, victims[0].Hits);
                break;
            case ChainTail.VictimsWithSlots:
                w.WriteByte((byte)victims.Count);
                foreach (var victim in victims)
                {
                    w.WriteUInt16(victim.EntityId);
                    w.WriteByte((byte)victim.Hits.Count);
                    WriteChainSlots(w, victim.Hits);
                    w.WriteByte(0);
                }
                break;
        }
        return w.ToArray();
    }

    public static byte[] ChainSlots(IReadOnlyList<SkillHit> hits)
    {
        ArgumentNullException.ThrowIfNull(hits);
        var w = new PacketWriter(ChainSlotSize * hits.Count);
        WriteChainSlots(w, hits);
        return w.ToArray();
    }

    private static void WriteChainSlots(PacketWriter w, IEnumerable<SkillHit> hits)
    {
        foreach (var hit in hits)
        {
            w.WriteInt32(hit.Damage);
            w.WriteInt32(hit.Kind);
            w.WriteUInt16(ChainSlotEnd);
        }
    }

    public static IReadOnlyList<SwingFrame> EncodeChainStart(
        ushort casterEntityId, ushort targetEntityId, SkillDefinition skill, ChainShape shape, byte[] tail,
        IReadOnlyList<byte[]> ownersTaken, ushort blockId)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(tail);
        ArgumentNullException.ThrowIfNull(ownersTaken);
        var frames = ownersTaken
            .Select(ownerTaken => new SwingFrame(Opcode.S2C_OP_016D_TBD, ownerTaken, MeleeSwingFrames.NoBlock, SwingAudience.Everyone))
            .ToList();
        var header = EffectHeader(
            casterEntityId, shape.TargetInHeader ? targetEntityId : casterEntityId, (ushort)skill.ActionId,
            (sbyte)skill.Row.EsClass, (ushort)skill.Id, (ushort)skill.Row.EState);
        frames.Add(new(Opcode.S2C_OP_00E0_TBD, [.. header, .. tail], blockId, SwingAudience.Everyone));
        return frames;
    }

    public static IReadOnlyList<SwingFrame> EncodeChainVictim(
        PlayerStrike strike, ChainVictim victim, ushort blockId, SwingAdditions additions, int type = SkillDamage.PhysicalType, int element = 0)
    {
        ArgumentNullException.ThrowIfNull(victim);
        ArgumentNullException.ThrowIfNull(additions);
        var frames = new List<SwingFrame>();
        AppendVictim(frames, strike, SummaryResult(strike.TargetEntityId, victim.Sum, victim.Landed, type, element), blockId, additions);
        return frames;
    }

    public static IReadOnlyList<SwingFrame> EncodeStandaloneHit(PlayerStrike strike, SkillHit hit, SwingAdditions additions)
    {
        ArgumentNullException.ThrowIfNull(additions);
        var frames = new List<SwingFrame>();
        if (additions.OwnerTaken is { } ownerTaken)
        {
            frames.Add(new(Opcode.S2C_OP_016D_TBD, ownerTaken, MeleeSwingFrames.NoBlock, SwingAudience.Everyone));
        }
        AppendVictim(frames, strike, HitResult(strike.TargetEntityId, hit), MeleeSwingFrames.NoBlock, additions);
        return frames;
    }

    private static byte[] SummaryResult(ushort targetEntityId, int sum, int landed, int type = SkillDamage.PhysicalType, int element = 0)
    {
        var result = new PacketWriter(HitResultSize);
        result.WriteUInt16(targetEntityId);
        result.WriteInt32(sum);
        result.WriteInt32(type);
        result.WriteInt32(SkillDamage.KindHit);
        result.WriteInt32(element);
        result.WriteUInt32(TornadoResultFlag);
        result.WriteInt32(0);
        result.WriteUInt16((ushort)landed);
        result.WriteUInt16(0);
        return result.ToArray();
    }

    public const int HealTailSize = 11;

    public static IReadOnlyList<SwingFrame> EncodeHeal(
        ushort casterEntityId, (ushort X, ushort Y) casterAt, ushort targetEntityId, (ushort X, ushort Y) targetAt,
        bool healed, SkillDefinition skill, ushort blockId, IReadOnlyList<(Opcode Opcode, byte[] Payload)> afterBlock)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(afterBlock);
        var w = new PacketWriter(EffectHeaderSize + HealTailSize);
        w.WriteBytes(EffectHeader(casterEntityId, casterEntityId, (ushort)skill.ActionId, (sbyte)skill.Row.EsClass, (ushort)skill.Id, (ushort)skill.Row.EState));
        w.WriteUInt16(targetEntityId);
        w.WriteUInt16(casterAt.X);
        w.WriteUInt16(casterAt.Y);
        w.WriteUInt16(targetAt.X);
        w.WriteUInt16(targetAt.Y);
        w.WriteByte(healed ? (byte)1 : (byte)0);
        var frames = new List<SwingFrame> { new(Opcode.S2C_OP_00E0_TBD, w.ToArray(), blockId, SwingAudience.Everyone) };
        AppendBlockEnd(frames, blockId, afterBlock);
        return frames;
    }

    public static IReadOnlyList<SwingFrame> EncodeSupport(
        ushort casterEntityId, ushort ownerEntityId, int actionId, SkillDefinition skill, byte[] tail, ushort blockId,
        IReadOnlyList<(Opcode Opcode, byte[] Payload)> toOwnerInBlock, IReadOnlyList<(Opcode Opcode, byte[] Payload)> afterBlock)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(tail);
        ArgumentNullException.ThrowIfNull(toOwnerInBlock);
        ArgumentNullException.ThrowIfNull(afterBlock);
        var header = EffectHeader(casterEntityId, ownerEntityId, (ushort)actionId, (sbyte)skill.Row.EsClass, (ushort)skill.Id, (ushort)skill.Row.EState);
        var frames = new List<SwingFrame> { new(Opcode.S2C_OP_00E0_TBD, [.. header, .. tail], blockId, SwingAudience.Everyone) };
        frames.AddRange(toOwnerInBlock.Select(frame => new SwingFrame(frame.Opcode, frame.Payload, blockId, SwingAudience.Owner)));
        AppendBlockEnd(frames, blockId, afterBlock);
        return frames;
    }

    public static byte[] FlagTail(bool flag) => new[] { flag ? (byte)1 : (byte)0 };

    public static byte[] FlagAmountTail(bool flag, int amount)
    {
        var w = new PacketWriter(5);
        w.WriteByte(flag ? (byte)1 : (byte)0);
        w.WriteInt32(amount);
        return w.ToArray();
    }

    public static byte[] FlagAmountPointTail(bool flag, int amount, (ushort X, ushort Y) point)
    {
        var w = new PacketWriter(9);
        w.WriteByte(flag ? (byte)1 : (byte)0);
        w.WriteInt32(amount);
        w.WriteUInt16(point.X);
        w.WriteUInt16(point.Y);
        return w.ToArray();
    }

    public static byte[] FlagPointTail(bool flag, (ushort X, ushort Y) point)
    {
        var w = new PacketWriter(5);
        w.WriteByte(flag ? (byte)1 : (byte)0);
        w.WriteUInt16(point.X);
        w.WriteUInt16(point.Y);
        return w.ToArray();
    }

    public static IReadOnlyList<SwingFrame> EncodeAreaStart(
        ushort casterEntityId, ushort targetEntityId, SkillDefinition skill, ushort blockId)
    {
        var target = new PacketWriter(2);
        target.WriteUInt16(targetEntityId);
        return EncodeAreaStart(casterEntityId, skill, blockId, target.ToArray());
    }

    public static IReadOnlyList<SwingFrame> EncodeAreaStart(ushort casterEntityId, SkillDefinition skill, ushort blockId, byte[] tail)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(tail);
        var w = new PacketWriter(EffectHeaderSize + tail.Length);
        w.WriteBytes(EffectHeader(casterEntityId, casterEntityId, (ushort)skill.ActionId, (sbyte)skill.Row.EsClass, (ushort)skill.Id, (ushort)skill.Row.EState));
        w.WriteBytes(tail);
        return new[] { new SwingFrame(Opcode.S2C_OP_00E0_TBD, w.ToArray(), blockId, SwingAudience.Everyone) };
    }

    public static byte[] AreaTailBytes(AreaTail tail, ushort targetEntityId, IReadOnlyList<ushort> victims, int radius)
    {
        ArgumentNullException.ThrowIfNull(victims);
        var w = new PacketWriter(8 + 2 * victims.Count);
        switch (tail)
        {
            case AreaTail.CountThenIds:
                w.WriteUInt16((ushort)victims.Count);
                foreach (var id in victims) w.WriteUInt16(id);
                break;
            case AreaTail.TargetCountThenIds:
                w.WriteUInt16(targetEntityId);
                w.WriteUInt16((ushort)victims.Count);
                foreach (var id in victims) w.WriteUInt16(id);
                break;
            case AreaTail.TargetOnly:
                w.WriteUInt16(targetEntityId);
                break;
            case AreaTail.ByteCountThenIds:
                w.WriteByte((byte)victims.Count);
                foreach (var id in victims) w.WriteUInt16(id);
                break;
            case AreaTail.Radius:
                w.WriteUInt16((ushort)radius);
                break;
            case AreaTail.None:
                break;
            case AreaTail.ExtraFlag:
                var extra = victims.FirstOrDefault(id => id != targetEntityId);
                w.WriteByte(extra != 0 ? (byte)1 : (byte)0);
                w.WriteUInt16(targetEntityId);
                if (extra != 0) w.WriteUInt16(extra);
                break;
        }
        return w.ToArray();
    }

    public static IReadOnlyList<SwingFrame> EncodeAreaVictim(PlayerStrike strike, SkillHit hit, ushort blockId, SwingAdditions additions)
    {
        ArgumentNullException.ThrowIfNull(additions);
        var frames = new List<SwingFrame>();
        if (additions.OwnerTaken is { } ownerTaken)
        {
            frames.Add(new(Opcode.S2C_OP_016D_TBD, ownerTaken, MeleeSwingFrames.NoBlock, SwingAudience.Everyone));
        }
        AppendVictim(frames, strike, HitResult(strike.TargetEntityId, hit), blockId, additions);
        return frames;
    }

    public static IReadOnlyList<SwingFrame> EncodeAreaEnd(ushort blockId, IReadOnlyList<(Opcode Opcode, byte[] Payload)> afterBlock)
    {
        ArgumentNullException.ThrowIfNull(afterBlock);
        var frames = new List<SwingFrame>();
        AppendBlockEnd(frames, blockId, afterBlock);
        return frames;
    }

    private static void AppendVictim(List<SwingFrame> frames, PlayerStrike strike, byte[] hitResult, ushort blockId, SwingAdditions additions)
    {
        frames.Add(new(Opcode.S2C_OP_002A_TBD, hitResult, blockId, SwingAudience.Everyone));
        if (strike.Killed)
        {
            frames.Add(new(Opcode.S2C_OP_002B_TBD, Death(strike.TargetEntityId), blockId, SwingAudience.Everyone));
            foreach (var (opcode, payload) in additions.OwnerRewards)
            {
                frames.Add(new(opcode, payload, blockId, SwingAudience.Owner));
            }
            if (additions.Drops is { } drops)
            {
                foreach (var (opcode, payload) in drops)
                {
                    frames.Add(new(opcode, payload, blockId, SwingAudience.Everyone));
                }
            }
        }
        frames.Add(new(Opcode.S2C_OP_011E_TBD, MonsterHp(strike), blockId, SwingAudience.Engaged));
        foreach (var (opcode, payload, audience) in additions.AttackerRewards)
        {
            frames.Add(new(opcode, payload, blockId, audience));
        }
    }

    private static void AppendBlockEnd(List<SwingFrame> frames, ushort blockId, IReadOnlyList<(Opcode Opcode, byte[] Payload)> afterBlock)
    {
        frames.Add(new(Opcode.S2C_COMMAND_BLOCK_END, Array.Empty<byte>(), blockId, SwingAudience.Everyone));
        foreach (var (opcode, payload) in afterBlock)
        {
            frames.Add(new(opcode, payload, MeleeSwingFrames.NoBlock, SwingAudience.Attacker));
        }
    }

    public static byte[] TailFor(SkillTail tail, PlayerStrike strike, int count, bool flag = false)
    {
        var w = new PacketWriter(PositionTailSize + 1);
        switch (tail)
        {
            case SkillTail.Positions:
                return PositionTail(strike);
            case SkillTail.PositionsAndFlag:
                w.WriteBytes(PositionTail(strike));
                w.WriteByte(flag ? (byte)1 : (byte)0);
                break;
            case SkillTail.Count:
                w.WriteUInt16((ushort)count);
                break;
            case SkillTail.TargetOnly:
                w.WriteUInt16(strike.TargetEntityId);
                break;
        }
        return w.ToArray();
    }

    public static byte[] PositionTail(PlayerStrike strike)
    {
        var w = new PacketWriter(PositionTailSize);
        w.WriteUInt16(strike.TargetEntityId);
        w.WriteUInt16(strike.AttackerX);
        w.WriteUInt16(strike.AttackerY);
        w.WriteUInt16(strike.TargetX);
        w.WriteUInt16(strike.TargetY);
        return w.ToArray();
    }

    public static byte[] DebuffResisted(ushort victimEntityId, uint blockedBits)
    {
        var w = new PacketWriter(6);
        w.WriteUInt16(victimEntityId);
        w.WriteUInt32(blockedBits);
        return w.ToArray();
    }

    public static byte[] MonsterHpUpdate(ushort monsterEntityId, int hp)
    {
        var w = new PacketWriter(MonsterHpSize);
        w.WriteUInt16(monsterEntityId);
        w.WriteInt32(Math.Max(0, hp));
        w.WriteUInt16(0);
        w.WriteByte(0);
        return w.ToArray();
    }

    private static byte[] MonsterHp(PlayerStrike strike)
    {
        var w = new PacketWriter(MonsterHpSize);
        w.WriteUInt16(strike.TargetEntityId);
        w.WriteInt32(Math.Max(0, strike.MobHpAfter));
        w.WriteUInt16(0);
        w.WriteByte(0);
        return w.ToArray();
    }

    public const byte SelfDestructDeath = 2;

    public static IReadOnlyList<SwingFrame> EncodeSelfDestruct(PlayerStrike strike, ushort blockId, SwingAdditions additions)
    {
        ArgumentNullException.ThrowIfNull(additions);
        var frames = new List<SwingFrame> { new(Opcode.S2C_OP_002B_TBD, Death(strike.TargetEntityId, SelfDestructDeath), blockId, SwingAudience.Everyone) };
        foreach (var (opcode, payload) in additions.OwnerRewards) frames.Add(new(opcode, payload, blockId, SwingAudience.Owner));
        if (additions.Drops is { } drops)
        {
            foreach (var (opcode, payload) in drops) frames.Add(new(opcode, payload, blockId, SwingAudience.Everyone));
        }
        AppendBlockEnd(frames, blockId, Array.Empty<(Opcode Opcode, byte[] Payload)>());
        return frames;
    }

    private static byte[] Death(ushort targetEntityId, byte deathType = MeleeSwingFrames.DeathTypeNormal)
    {
        var w = new PacketWriter(DeathSize);
        w.WriteUInt16(targetEntityId);
        w.WriteByte(deathType);
        return w.ToArray();
    }
}
