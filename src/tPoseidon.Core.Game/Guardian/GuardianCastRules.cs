using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Skills;

namespace tPoseidon.Core.Game.Guardian;

public enum GuardianCastTarget
{
    Enemy,
    Owner,
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

    public static bool Supports(int actionId) =>
        SkillHitShapes.Find(actionId) is { NeedsGun: false } || SkillAreaShapes.Find(actionId) is not null;

    public static IEnumerable<GuardianCastCandidate> CardCandidates(
        GuardianState guardian, float ownerHpRatio, Func<int, int> relationOf)
    {
        ArgumentNullException.ThrowIfNull(guardian);
        ArgumentNullException.ThrowIfNull(relationOf);
        var cards = guardian.Skills
            .Where(skill => skill.Type == GuardianSkillType.Card)
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
