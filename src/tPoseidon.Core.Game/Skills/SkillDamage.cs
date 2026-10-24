namespace tPoseidon.Core.Game.Skills;

public readonly record struct SkillAttacker(int Ap, int Ac, int Ma, int Lk, int Level, int Da = 0, int Hp = 0, int MaxHp = 0, int Hv = 0, int Dx = 0, int Dp = 0, int WeaponDa = 0, float DarkBonus = 0f, int Weight = 0, int ElementBonus = 0);

public readonly record struct SkillTarget(int Dp, int Hv, int Md, int Lk, int Hp, int Level, int Resist, int MaxHp = 0, int BaseDp = 0);

public readonly record struct SkillHit(int Damage, int Type, int Kind, int Element, int Healed, int Flags = 0);

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
        SkillAttacker attacker, int power, SkillTarget target, float applyRatio, int element, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        var chance = (int)((attacker.Ac - target.Hv * PlayerHvWeight + PhysicalHitBase) * HitScale * applyRatio);
        if (rng.Next(Percent) >= Math.Clamp(chance, LowestHitChance, HighestHitChance))
        {
            return new SkillHit(0, PhysicalType, KindMiss, 0, 0);
        }

        if (IsCritical(attacker, target, rng))
        {
            var critical = Math.Max(1, (int)((float)(power * CriticalPowerShare) - (float)(target.Dp * DefenceShare)));
            return AfterResist(critical, PhysicalType, KindCritical, target, element);
        }
        var normal = Math.Max(0, (int)((float)(double)power - (float)(target.Dp * DefenceShare)));
        return AfterResist(normal, PhysicalType, KindHit, target, element);
    }

    public static SkillHit Magic(
        SkillAttacker attacker, int power, SkillTarget target, float applyRatio, int magicAttackLoss, int element, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        var chance = (int)((attacker.Lk - target.Lk + MagicHitBase) * HitScale * applyRatio);
        if (rng.Next(Percent) >= Math.Clamp(chance, LowestHitChance, HighestHitChance))
        {
            return new SkillHit(0, MagicType, KindMiss, 0, 0);
        }

        var magicAttack = attacker.Ma - magicAttackLoss;
        if (IsCritical(attacker, target, rng))
        {
            var critical = MagicDamage((int)(power * MagicCriticalPowerShare), magicAttack, target.Md);
            return AfterResist(WithDarkBonus(critical, attacker, element), MagicType, KindCritical, target, element);
        }
        return AfterResist(WithDarkBonus(MagicDamage(power, magicAttack, target.Md), attacker, element), MagicType, KindHit, target, element);
    }

    public static bool IsCritical(SkillAttacker attacker, SkillTarget target, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        var chance = (int)((attacker.Ac + attacker.Lk * (double)HitScale - (target.Hv + target.Lk)) / CriticalDivisor);
        return rng.Next(Percent) < Math.Clamp(chance, LowestCriticalChance, HighestCriticalChance);
    }

    public static int WithDarkBonus(int damage, SkillAttacker attacker, int element) =>
        element == SkillActions.DarkElement && attacker.DarkBonus > 0f
            ? damage + (int)Math.Truncate(damage * (double)attacker.DarkBonus)
            : damage;

    public static int MagicDamage(int power, int magicAttack, int magicDefence) =>
        Math.Max(0, (int)((float)(power * (magicAttack / MagicAttackScale)) - (float)(magicDefence * MagicDefenceShare)));

    private static SkillHit AfterResist(int damage, int type, int kind, SkillTarget target, int element)
    {
        var resist = target.Resist;
        var remaining = damage;
        if (resist > FullResist)
        {
            resist -= FullResist;
            remaining = 0;
        }
        remaining -= (int)(damage * resist * ResistScale);
        var healed = 0;
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
