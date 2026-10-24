using tPoseidon.Core.Game.Skills;
using tPoseidon.Core.Game.Skills.Effects;

namespace tPoseidon.Core.Game.Tests.Skills;

public sealed class SkillRidersTests
{
    [Fact]
    public void RidersOnlyFollowAConnectingHitOnALivingVictim()
    {
        var hit = new SkillHit(100, SkillDamage.PhysicalType, SkillDamage.KindHit, 0, 0);

        Assert.True(SkillRiders.Gates(hit, 50));
        Assert.False(SkillRiders.Gates(hit, 0));
        Assert.False(SkillRiders.Gates(hit with { Kind = SkillDamage.KindMiss }, 50));
        Assert.False(SkillRiders.Gates(hit with { Kind = 8 }, 50));
        Assert.True(SkillRiders.Gates(hit with { Kind = SkillDamage.KindCritical | SkillDamage.KindFinishingFlag }, 50));
    }

    [Fact]
    public void ShockwaveCarriesBothStunPassives()
    {
        var riders = SkillRiders.Of(SkillRiders.Shockwave);

        Assert.Equal(new[] { SkillRiders.PowerStun, SkillRiders.DefenseParalysis }, riders.Select(rider => rider.EffectAction));
        Assert.True(SkillEffectKinds.Find(SkillRiders.PowerStun)!.Holds);
        Assert.True(SkillEffectKinds.Find(SkillRiders.DefenseParalysis)!.PreventsEvade);
        Assert.True(SkillEffectKinds.Find(SkillRiders.DefenseParalysis)!.SkipsRoll);
    }

    [Fact]
    public void CounterPunchScalesWithAccuracyAboveThirtyNine()
    {
        Assert.Equal((float)((239 - 39) * 0.01f), SkillRiders.CounterPunchCoefficient(239));
    }

    [Fact]
    public void ElectricSwordStunsWhenTheRollIsWithinItsRatio()
    {
        Assert.True(SkillRiders.Stuns(0.5f, 50));
        Assert.False(SkillRiders.Stuns(0.5f, 51));
    }

    [Fact]
    public void ADefenseParalysedMonsterCannotEvadeNormalAttacks()
    {
        int Misses(bool paralysed) => Enumerable.Range(0, 300).Count(seed =>
            tPoseidon.Core.Game.Combat.MeleeResolution.PlayerAgainstMonster(300, 500, 0, 0, 0, 500, 0, 100000, new Random(seed), 0, targetCannotEvade: paralysed).Result
            == tPoseidon.Core.Game.Combat.MeleeResolution.ResultMiss);

        Assert.True(Misses(paralysed: true) < Misses(paralysed: false));
        Assert.True(Misses(paralysed: true) <= 300 / 50);
    }
}
