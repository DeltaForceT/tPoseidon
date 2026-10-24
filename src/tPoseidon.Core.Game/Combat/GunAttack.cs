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
    public const byte CriticalChargeFlag = 1;

    public static (int Damage, int Kind) Shot(
        int attackerAc, int attackerLk, int power, float criticalMultiplier, bool critical, SkillTarget target, bool targetIsPlayer, Func<int, int> randomBelow)
    {
        ArgumentNullException.ThrowIfNull(randomBelow);
        var defense = Math.Max(0, target.Hv) * (targetIsPlayer ? PlayerDefenseWeight : MonsterDefenseWeight);
        var raw = (attackerAc - AccuracyFloor) * AccuracyWeight + power + PowerFloor;
        int damage;
        int kind;
        if (critical)
        {
            damage = Math.Max(1, (int)(raw * CriticalScale * criticalMultiplier - defense));
            kind = SkillDamage.KindCritical;
        }
        else
        {
            if (attackerLk - target.Lk + LuckOffset < randomBelow(Percent)) return (0, SkillDamage.KindMiss);
            damage = Math.Max(0, (int)(raw - defense));
            kind = damage == 0 ? SkillDamage.KindMiss : SkillDamage.KindHit;
        }
        if (target.Resist > Percent) damage = 0;
        else damage -= (int)(damage * target.Resist / (double)Percent);
        if (damage > 0 && damage >= target.Hp) kind |= SkillDamage.KindFinishingFlag;
        return (damage, kind);
    }

    public static int CustomizedPower(int power, float ratio) => (int)(power * (double)ratio);
}
