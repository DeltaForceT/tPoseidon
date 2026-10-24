using tPoseidon.Core.Game.Combat;
using tPoseidon.Core.Game.Skills;

namespace tPoseidon.Core.Game.Tests.Combat;

public sealed class GunAttackTests
{
    private static SkillTarget Target(int hv = 2, int lk = 10, int hp = 5000, int resist = 0) =>
        new(Dp: 0, Hv: hv, Md: 0, Lk: lk, Hp: hp, Level: 1, Resist: resist);

    private static (long Damage, int Kind) Shoot(SkillTarget target, bool critical = false, float multiplier = 1f, bool targetIsPlayer = false, int roll = 0) =>
        GunAttack.Shot(attackerAc: 60, attackerLk: 10, power: 100, multiplier, critical, target, targetIsPlayer, _ => roll);

    [Fact]
    public void NormalShot_IsAccuracyAndPowerLessTheMonsterDefense()
    {
        Assert.Equal((280L, SkillDamage.KindHit), Shoot(Target()));
    }

    [Fact]
    public void PlayerVictim_UsesTheSmallerDefenseWeight()
    {
        Assert.Equal((316, SkillDamage.KindHit), Shoot(Target(), targetIsPlayer: true));
    }

    [Fact]
    public void ChargedShot_IsCriticalAndScaledBySharpSense()
    {
        Assert.Equal((348, SkillDamage.KindCritical), Shoot(Target(), critical: true));
        Assert.Equal((552, SkillDamage.KindCritical), Shoot(Target(), critical: true, multiplier: 1.5f));
    }

    [Fact]
    public void ChargedShot_NeverMissesAndDealsAtLeastOne()
    {
        Assert.Equal((1, SkillDamage.KindCritical), Shoot(Target(hv: 100), critical: true, roll: 99));
    }

    [Fact]
    public void NormalShot_MissesWhenTheRollBeatsTheLuckMargin()
    {
        Assert.Equal((280, SkillDamage.KindHit), Shoot(Target(), roll: GunAttack.LuckOffset));
        Assert.Equal((0, SkillDamage.KindMiss), Shoot(Target(), roll: GunAttack.LuckOffset + 1));
    }

    [Fact]
    public void NormalShot_AgainstTooMuchDefenseIsAMiss()
    {
        Assert.Equal((0, SkillDamage.KindMiss), Shoot(Target(hv: 100)));
    }

    [Fact]
    public void GunResist_TakesItsShareAndAboveAHundredBlocksAll()
    {
        Assert.Equal(140, Shoot(Target(resist: 50)).Damage);
        Assert.Equal(0, Shoot(Target(resist: 101)).Damage);
    }

    [Fact]
    public void LethalShot_CarriesTheFinishingFlag()
    {
        Assert.Equal((280, SkillDamage.KindHit | SkillDamage.KindFinishingFlag), Shoot(Target(hp: 280)));
    }

    [Fact]
    public void CustomizingGun_MultipliesThePower()
    {
        Assert.Equal(150, GunAttack.CustomizedPower(100, 1.5f));
    }
}
