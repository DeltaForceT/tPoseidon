using tPoseidon.Core.Common;
using tPoseidon.Core.Game.Skills;
using tPoseidon.Core.Game.Skills.Effects;

namespace tPoseidon.Core.Game.Guardian;

public static class GuardianRunes
{
    public const int StormyWind = 1449;
    public const int StormyWindEndMs = 5000;
    public const string RatioColumn = "Ratio";
    public const string MaxTargetColumn = "maxTarget";
    public const int ElementSlotCount = 9;
    public const int WindSlot = ElementSlots.Wind;
    public const int WindLevelBase = 10;
    private const double LevelShare = 0.5;
    private const double Hundredth = 0.01f;

    public static readonly AreaShape StormyWindShape = new(AreaCentre.Target, int.MaxValue, AreaTail.CountThenIds, AreaTm.None)
    {
        LimitColumn = MaxTargetColumn,
    };

    public static readonly SkillEffectKind StormyWindObject = new(
        StormyWind, SkillEffectKinds.NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, RatioColumn, OnTarget: true)
    {
        FixedDurationMs = StormyWindEndMs, GrantsTm = false,
    };

    public static long StormyWindPower(int masterLevel, float ratio) =>
        WireLimits.Saturate(Math.Truncate(masterLevel * (double)ratio));

    public static int StormyWindElement(int guardianLevel) =>
        (int)Math.Truncate(guardianLevel * LevelShare + LevelShare) + WindLevelBase;

    public static int ElementOfSlot(int slot) => 1 << slot;

    public static (long Damage, long Healed, int Element) WithElements(
        long damage, IReadOnlyList<int> attackSlots, IReadOnlyList<int> victimResists, int victimPlainResist)
    {
        ArgumentNullException.ThrowIfNull(attackSlots);
        ArgumentNullException.ThrowIfNull(victimResists);
        long added = 0;
        var element = 0;
        for (var slot = 0; slot < ElementSlotCount && slot < attackSlots.Count; slot++)
        {
            if (attackSlots[slot] == 0) continue;
            var raised = (long)Math.Truncate(damage * attackSlots[slot] * Hundredth);
            var resisted = (long)Math.Truncate(raised * (slot < victimResists.Count ? victimResists[slot] : 0) * Hundredth);
            added += raised - resisted;
            element |= ElementOfSlot(slot);
        }
        added -= (long)Math.Truncate(damage * victimPlainResist * Hundredth);
        var total = damage + added;
        return total < 0 ? (0, -total, element) : (total, 0, element);
    }

    public static SkillHit StormyWindHit(
        SkillAttacker attacker, long power, SkillTarget target, IReadOnlyList<int> attackSlots, IReadOnlyList<int> victimResists, int victimPlainResist, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        var raw = SkillDamage.Physical(attacker with { AlwaysHits = true }, power, target with { Resist = 0, DamageShare = 1.0 }, 1f, SkillActions.NoElement, rng);
        if ((raw.Kind & SkillDamage.KindMask) == SkillDamage.KindMiss) return raw;
        var (damage, healed, element) = WithElements(raw.Damage, attackSlots, victimResists, victimPlainResist);
        if (damage > 0 && target.DamageShare < 1.0) damage = (long)(damage * target.DamageShare);
        return raw with { Damage = damage, Healed = healed, Element = element };
    }
}
