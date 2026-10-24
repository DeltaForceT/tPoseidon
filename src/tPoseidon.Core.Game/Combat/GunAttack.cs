using tPoseidon.Core.Common;
using tPoseidon.Core.Game.Skills;

namespace tPoseidon.Core.Game.Combat;

public static class GunAttack
{
    public const int AccuracyFloor = 49;
    public const double AccuracyWeight = 20.0;
    public const double PowerFloor = 20.0;
    public const float CriticalScale = 1.2f;
    public const double PlayerDefenseWeight = 12.0;
    public const double MonsterDefenseWeight = 30.0;
    public const int LuckOffset = 0x50;
    public const int Percent = 100;

    public static (long Damage, int Kind) Shot(
        long attackerAc, long attackerLk, long power, float criticalMultiplier, bool critical, SkillTarget target, bool targetIsPlayer, Func<int, int> randomBelow)
    {
        ArgumentNullException.ThrowIfNull(randomBelow);
        var defense = Math.Max(0, target.Hv) * (targetIsPlayer ? PlayerDefenseWeight : MonsterDefenseWeight);
        var raw = (attackerAc - AccuracyFloor) * AccuracyWeight + power + PowerFloor;
        long damage;
        int kind;
        if (critical)
        {
            damage = Math.Max(1, WireLimits.Saturate(raw * CriticalScale * criticalMultiplier - defense));
            kind = SkillDamage.KindCritical;
        }
        else
        {
            if (attackerLk - target.Lk + LuckOffset < randomBelow(Percent)) return (0, SkillDamage.KindMiss);
            damage = Math.Max(0, WireLimits.Saturate(raw - defense));
            kind = damage == 0 ? SkillDamage.KindMiss : SkillDamage.KindHit;
        }
        if (target.Resist > Percent) damage = 0;
        else damage -= WireLimits.Saturate(damage * target.Resist / (double)Percent);
        if (damage > 0 && damage >= target.Hp) kind |= SkillDamage.KindFinishingFlag;
        return (damage, kind);
    }

    public static long CustomizedPower(long power, float ratio) => WireLimits.Saturate(power * (double)ratio);
}
