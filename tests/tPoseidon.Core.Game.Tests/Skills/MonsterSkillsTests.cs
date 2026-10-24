using tPoseidon.Core.Game.Skills;

namespace tPoseidon.Core.Game.Tests.Skills;

public sealed class MonsterSkillsTests
{
    [Fact]
    public void HealAddsTheRatioOfMaximumAndReportsWhetherItHealed()
    {
        Assert.Equal(((int)(1000 + 2000 * (double)0.03f), true), MonsterSkills.Healing(1000, 2000, 0.03f));
        Assert.Equal((2000, false), MonsterSkills.Healing(2000, 2000, 0.06f));
    }

    [Fact]
    public void SirenAndRingFollowTheServer()
    {
        Assert.Equal((int)((5000 * (double)0.1f + 300 * 0.5) * 2.4f), MonsterSkills.SirenPower(5000, 300, 2.4f));
        Assert.Equal((int)(1000 * (double)(1.0f - 60f / 120f)), MonsterSkills.RingFalloff(1000, 60f, 120f));
    }

    [Fact]
    public void OnlyLevelsOneToTenOfBuiltSkillsAreCastable()
    {
        Assert.True(MonsterSkills.Castable(MonsterSkills.Heal, 10));
        Assert.False(MonsterSkills.Castable(MonsterSkills.Heal, 11));
        Assert.True(MonsterSkills.Castable(MonsterSkills.Charging, 1));
        Assert.True(MonsterSkills.Castable(MonsterSkills.Warp, 1));
        Assert.False(MonsterSkills.Castable(MonsterSkills.ChargingDo, 1));
        Assert.True(MonsterSkills.Castable(MonsterSkills.Poison, 1));
        Assert.Equal((SkillActions.ManaArrow, 2001), MonsterSkills.ImitationOf(MonsterSkills.ChainArrow));
    }

    [Fact]
    public void ChargingPowerAndConditionGatesFollowTheServer()
    {
        Assert.Equal((int)((100 + 40) * 5000 * (double)1.5f / 800.0), MonsterSkills.ChargingPower(100, 5000, 1.5f));
        Assert.True(MonsterSkills.BlocksSkill(MonsterSkills.StunnedCondition, 1001));
        Assert.False(MonsterSkills.BlocksSkill(MonsterSkills.StunnedCondition, MonsterSkills.StunCancelSkill));
        Assert.False(MonsterSkills.BlocksSkill(MonsterSkills.FrozenCondition, 1001));
        Assert.True(MonsterSkills.BlocksItems(MonsterSkills.StunnedCondition));
        Assert.False(MonsterSkills.BlocksItems(MonsterSkills.ParalysedCondition));
    }

    [Fact]
    public void ObjectSkillFormulasFollowTheServer()
    {
        Assert.Equal((int)(1000 * (double)0.3f), MonsterSkills.DotDamage(1000, 0.3f));
        Assert.Equal((float)(200 * (double)3.8f / 100.0), MonsterSkills.WaterNetDexterity(200, 3.8f));
        Assert.Equal(20000 * 10, MonsterSkills.DestructionPower(20000, 10f));
        Assert.Equal(2000, MonsterSkills.PoisonDamage(2000, 5000));
        Assert.Equal(499, MonsterSkills.PoisonDamage(2000, 500));
        Assert.Equal(0, MonsterSkills.PoisonDamage(2000, 1));
        Assert.False(MonsterSkills.PoisonStrikeDue(600, 0, 5000));
        Assert.True(MonsterSkills.PoisonStrikeDue(700, 0, 5000));
        Assert.True(MonsterSkills.PoisonStrikeDue(5700, 1, 5000));
        Assert.Equal(SkillActions.DarkElement, MonsterSkills.DotElementOf(MonsterSkills.CruelFlame));
        Assert.True(tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.Find(MonsterSkills.PoisonDo) is { Periodic: tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.MonsterPoison });
    }
}
