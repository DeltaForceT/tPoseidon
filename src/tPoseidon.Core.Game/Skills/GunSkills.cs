namespace tPoseidon.Core.Game.Skills;

public static class GunSkills
{
    public const int PowerShot = 1148;
    public const int ShootingSpree = 1174;
    public const int DoubleShot = 1249;
    public const int Reckless = 1277;
    public const int BreakBarrier = 1324;

    public const int GunType = 4;
    public const int GunSubType = 0x36;
    public const int BulletItemType = 29;
    public const int ShootingSpreeVictims = 9;
    public const int RecklessVictims = 10;
    public const int DoubleShotHits = 2;
    public const int BarrierCap = 80;

    public const string RatioColumn = "Ratio";
    public const string ApplyRatioColumn = "ApplyRatio";
    public const string RadiusColumn = "Radius";
    public const string MonNumColumn = "MonNum";

    private const int AccuracyFloor = 49;
    private const double AccuracyWeight = 20.0;
    private const double PowerFloor = 20.0;
    private const double CriticalShare = 1.2;
    private const double PlayerAvoidWeight = 12.0;
    private const double MonsterAvoidWeight = 30.0;
    private const int LuckOffset = 90;
    private const double HitScale = 1.2;
    private const int LowestHit = 20;
    private const int HighestHit = 100;
    private const double CriticalDivisor = 4.0;
    private const int LowestCritical = 5;
    private const int HighestCritical = 40;
    private const int Percent = 100;
    private const double BarrierFloor = 20.0;
    private const double BarrierDivisor = 3.0;

    public static bool NeedsGun(int actionId) => actionId is PowerShot or ShootingSpree or DoubleShot or Reckless or BreakBarrier;

    public static bool HitsAround(int actionId) => actionId is ShootingSpree or Reckless;

    public static SkillHit Hit(
        SkillAttacker attacker, int weaponPower, SkillTarget target, bool targetIsPlayer, float ratio, float hitCoefficient, float criticalMultiplier,
        Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        var hitChance = Math.Clamp((int)((attacker.Lk - target.Lk + LuckOffset) * HitScale * hitCoefficient), LowestHit, HighestHit);
        if (rng.Next(Percent) >= hitChance) return new SkillHit(0, GunType, SkillDamage.KindMiss, SkillActions.GunElement, 0);
        var criticalChance = Math.Clamp(
            (int)((attacker.Ac + attacker.Lk * CriticalShare - (target.Hv + target.Lk)) / CriticalDivisor), LowestCritical, HighestCritical);
        var critical = rng.Next(Percent) < criticalChance;
        var avoid = Math.Max(0, target.Hv) * (targetIsPlayer ? PlayerAvoidWeight : MonsterAvoidWeight);
        var raw = (attacker.Ac - AccuracyFloor) * AccuracyWeight + weaponPower + PowerFloor;
        var damage = critical
            ? Math.Max(1, (int)(raw * CriticalShare * (ratio * criticalMultiplier) - avoid))
            : Math.Max(0, (int)((float)raw * (double)ratio) - (int)avoid);
        if (target.Resist > Percent) damage = 0;
        else damage -= (int)(damage * target.Resist * 0.01);
        var kind = critical ? SkillDamage.KindCritical : SkillDamage.KindHit;
        if (damage >= target.Hp) kind |= SkillDamage.KindFinishingFlag;
        return new SkillHit(damage, GunType, kind, SkillActions.GunElement, 0);
    }

    public static int BarrierBreak(int ownerDa, float ratio) =>
        Math.Min(BarrierCap, (int)((ownerDa * (double)ratio + BarrierFloor) / BarrierDivisor));
}
