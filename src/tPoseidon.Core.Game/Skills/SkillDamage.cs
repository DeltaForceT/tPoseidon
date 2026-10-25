using tPoseidon.Core.Common;

namespace tPoseidon.Core.Game.Skills;

public readonly record struct SkillAttacker(long Ap, long Ac, long Ma, long Lk, int Level, long Da = 0, long Hp = 0, long MaxHp = 0, long Hv = 0, long Dx = 0, long Dp = 0, long WeaponDa = 0, float DarkBonus = 0f, long Weight = 0, long ElementBonus = 0, bool AlwaysHits = false);

public readonly record struct SkillTarget(long Dp, long Hv, long Md, long Lk, long Hp, int Level, int Resist, long MaxHp = 0, long BaseDp = 0);

public readonly record struct SkillHit(long Damage, int Type, int Kind, int Element, long Healed, int Flags = 0);

public static class SkillDamage
{
    public const int PhysicalType = 2;
    public const int MagicType = 3;

    public const int KindHit = 0;
    public const int KindMiss = 1;
    public const int KindCritical = 3;
    public const int KindFinishingFlag = 0x80;
    public const int KindMask = 0x7F;

    private const int Percent = 100;
    private const int LowestHitChance = 20;
    private const int HighestHitChance = 100;
    private const int LowestCriticalChance = 5;
    private const int HighestCriticalChance = 40;
    private const double PlayerHvWeight = 2.5;
    private const double PhysicalHitBase = 40.0;
    private const int MagicHitBase = 90;
    private const float HitScale = 1.2f;
    private const double CriticalDivisor = 4.0;
    private const double CriticalPowerShare = 1.5;
    private const double DefenceShare = 0.75;
    private const double MagicCriticalPowerShare = 1.1;
    private const double MagicAttackScale = 100.0;
    private const double MagicDefenceShare = 0.9;
    private const double ResistScale = 0.01;
    private const int FullResist = 100;

    public static SkillHit Physical(
        SkillAttacker attacker, long power, SkillTarget target, float applyRatio, int element, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        var chance = WireLimits.Saturate((attacker.Ac - target.Hv * PlayerHvWeight + PhysicalHitBase) * HitScale * applyRatio);
        if (!attacker.AlwaysHits && rng.Next(Percent) >= Math.Clamp(chance, LowestHitChance, HighestHitChance))
        {
            return new SkillHit(0, PhysicalType, KindMiss, 0, 0);
        }

        if (IsCritical(attacker, target, rng))
        {
            var critical = Math.Max(1L, WireLimits.Saturate((float)(power * CriticalPowerShare) - (float)(target.Dp * DefenceShare)));
            return AfterResist(critical, PhysicalType, KindCritical, target, element);
        }
        var normal = Math.Max(0L, WireLimits.Saturate((float)(double)power - (float)(target.Dp * DefenceShare)));
        return AfterResist(normal, PhysicalType, KindHit, target, element);
    }

    public static SkillHit Magic(
        SkillAttacker attacker, long power, SkillTarget target, float applyRatio, long magicAttackLoss, int element, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        var chance = WireLimits.Saturate((attacker.Lk - target.Lk + MagicHitBase) * HitScale * applyRatio);
        if (rng.Next(Percent) >= Math.Clamp(chance, LowestHitChance, HighestHitChance))
        {
            return new SkillHit(0, MagicType, KindMiss, 0, 0);
        }

        var magicAttack = attacker.Ma - magicAttackLoss;
        if (IsCritical(attacker, target, rng))
        {
            var critical = MagicDamage(WireLimits.Saturate(power * MagicCriticalPowerShare), magicAttack, target.Md);
            return AfterResist(WithDarkBonus(critical, attacker, element), MagicType, KindCritical, target, element);
        }
        return AfterResist(WithDarkBonus(MagicDamage(power, magicAttack, target.Md), attacker, element), MagicType, KindHit, target, element);
    }

    public static bool IsCritical(SkillAttacker attacker, SkillTarget target, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        var chance = WireLimits.Saturate((attacker.Ac + attacker.Lk * (double)HitScale - (target.Hv + target.Lk)) / CriticalDivisor);
        return rng.Next(Percent) < Math.Clamp(chance, LowestCriticalChance, HighestCriticalChance);
    }

    public static long WithDarkBonus(long damage, SkillAttacker attacker, int element) =>
        element == SkillActions.DarkElement && attacker.DarkBonus > 0f
            ? SaturatingAdd(damage, WireLimits.Saturate(Math.Truncate(damage * (double)attacker.DarkBonus)))
            : damage;

    public static long MagicDamage(long power, long magicAttack, long magicDefence) =>
        Math.Max(0L, WireLimits.Saturate((float)(power * (magicAttack / MagicAttackScale)) - (float)(magicDefence * MagicDefenceShare)));

    private static long SaturatingAdd(long left, long right) =>
        right > 0 && left > long.MaxValue - right ? long.MaxValue : left + right;

    private static SkillHit AfterResist(long damage, int type, int kind, SkillTarget target, int element)
    {
        var resist = target.Resist;
        var remaining = damage;
        if (resist > FullResist)
        {
            resist -= FullResist;
            remaining = 0;
        }
        remaining -= WireLimits.Saturate(damage * resist * ResistScale);
        long healed = 0;
        if (remaining < 0)
        {
            healed = -remaining;
            remaining = 0;
        }
        var finished = kind == KindCritical
            ? (damage < target.Hp ? KindCritical : KindCritical | KindFinishingFlag)
            : (damage >= target.Hp ? KindFinishingFlag : KindHit);
        return new SkillHit(remaining, type, finished, element, healed);
    }
}
