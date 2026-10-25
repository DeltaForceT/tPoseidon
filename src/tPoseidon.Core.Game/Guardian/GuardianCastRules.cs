using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Skills;
using tPoseidon.Core.Game.Skills.Effects;

namespace tPoseidon.Core.Game.Guardian;

public enum GuardianCastTarget
{
    Enemy,
    Owner,
}

public enum GuardianCastKind
{
    None,
    Cure,
    Ground,
    ObjectHit,
    Area,
    Chain,
    PolarBear,
    Tornado,
    ManaRing,
    TargetEffect,
    Hit,
}

public readonly record struct GuardianCastCandidate(GuardianSkill Skill, GuardianCastTarget Target);

public sealed class GuardianCastClock
{
    private int _elapsedMs;

    public bool Due(int elapsedMs)
    {
        _elapsedMs += elapsedMs;
        if (_elapsedMs <= GuardianCastRules.PeriodMs) return false;
        _elapsedMs = 0;
        return true;
    }
}

public sealed class GuardianOrderCycle
{
    private readonly HashSet<byte> _used = [];
    private ushort _target;

    public bool IsUsed(byte slot) => _used.Contains(slot);

    public void MarkUsed(byte slot) => _used.Add(slot);

    public void Begin(ushort targetEntityId, IReadOnlyList<GuardianSkill> order)
    {
        ArgumentNullException.ThrowIfNull(order);
        if (_target != targetEntityId)
        {
            _target = targetEntityId;
            _used.Clear();
            return;
        }
        if (order.Count > 0 && order.All(entry => _used.Contains(entry.Slot))) _used.Clear();
    }
}

public static class GuardianCastRules
{
    public const int PeriodMs = 500;
    public const float LowOwnerHpRatio = 0.2f;
    public const float HeavyHealOwnerHpRatio = 0.3f;
    public const int HeavyHealSkillId = 2005;
    public const int HostileRelation = 0x20;
    public const int SupportRelation = 0x10;
    public const float CooldownFactor = 2f;
    public const float DefaultCooldownSeconds = 2f;
    public const byte CastEmote = 6;
    public const int GroupCount = 4;
    public const int UsableCardOption = 2;

    private static readonly int[][] GroupRotation =
    [
        [0, 2, 3, 1],
        [1, 3, 2, 0],
        [2, 0, 1, 3],
        [3, 1, 0, 2],
    ];

    public static int GroupAt(byte guardianFType, int step) =>
        guardianFType < GroupRotation.Length && step is >= 0 and < GroupCount ? GroupRotation[guardianFType][step] : -1;

    public static float CooldownSeconds(float? coolTime) => coolTime is { } seconds ? seconds * CooldownFactor : DefaultCooldownSeconds;

    public static GuardianCastKind KindOf(int actionId, GuardianCastTarget target)
    {
        if (target == GuardianCastTarget.Owner) return actionId == SkillActions.Cure ? GuardianCastKind.Cure : GuardianCastKind.None;
        if (GroundSkills.IsGround(actionId)) return GuardianCastKind.Ground;
        if (ObjectHits.IsTargeted(actionId)) return GuardianCastKind.ObjectHit;
        if (SkillAreaShapes.Find(actionId) is not null) return GuardianCastKind.Area;
        if (ChainHits.Find(actionId) is not null) return GuardianCastKind.Chain;
        if (PolarBearHits.CountOf(actionId) > 0) return GuardianCastKind.PolarBear;
        if (actionId == SkillActions.TornadoBomb) return GuardianCastKind.Tornado;
        if (actionId == SkillAreaRules.ManaRing) return GuardianCastKind.ManaRing;
        if (SkillEffectKinds.Find(actionId) is { OnTarget: true }) return GuardianCastKind.TargetEffect;
        return SkillHitShapes.Find(actionId) is not null || actionId == SkillActions.ManaArrow ? GuardianCastKind.Hit : GuardianCastKind.None;
    }

    public static IEnumerable<GuardianCastCandidate> CardCandidates(
        GuardianState guardian, float ownerHpRatio, Func<int, int> relationOf, Func<int, bool> usable)
    {
        ArgumentNullException.ThrowIfNull(guardian);
        ArgumentNullException.ThrowIfNull(relationOf);
        ArgumentNullException.ThrowIfNull(usable);
        var cards = guardian.Skills
            .Where(skill => skill.Type == GuardianSkillType.Card && usable(skill.SkillId))
            .OrderBy(skill => skill.FType)
            .ThenBy(skill => skill.Slot)
            .ToArray();
        if (ownerHpRatio < LowOwnerHpRatio)
        {
            foreach (var card in cards)
            {
                if ((relationOf(card.SkillId) & SupportRelation) != 0) yield return new GuardianCastCandidate(card, GuardianCastTarget.Owner);
            }
        }
        for (var step = 0; step < GroupCount; step++)
        {
            var group = GroupAt(guardian.FType, step);
            foreach (var card in cards.Where(card => card.FType == group))
            {
                if ((relationOf(card.SkillId) & HostileRelation) != 0) yield return new GuardianCastCandidate(card, GuardianCastTarget.Enemy);
            }
        }
    }

    public static IEnumerable<GuardianCastCandidate> OrderCandidates(
        GuardianState guardian, GuardianOrderCycle cycle, float ownerHpRatio, Func<int, int> relationOf)
    {
        ArgumentNullException.ThrowIfNull(guardian);
        ArgumentNullException.ThrowIfNull(cycle);
        ArgumentNullException.ThrowIfNull(relationOf);
        foreach (var entry in guardian.Skills.Where(skill => skill.Type == GuardianSkillType.Order).OrderBy(skill => skill.Slot))
        {
            if (cycle.IsUsed(entry.Slot)) continue;
            if (entry.SkillId == HeavyHealSkillId && ownerHpRatio > HeavyHealOwnerHpRatio) continue;
            var relation = relationOf(entry.SkillId);
            if ((relation & HostileRelation) != 0) yield return new GuardianCastCandidate(entry, GuardianCastTarget.Enemy);
            else if ((relation & SupportRelation) != 0) yield return new GuardianCastCandidate(entry, GuardianCastTarget.Owner);
        }
    }

    public static bool HasOrder(GuardianState guardian)
    {
        ArgumentNullException.ThrowIfNull(guardian);
        return guardian.Skills.Any(skill => skill.Type == GuardianSkillType.Order);
    }
}
