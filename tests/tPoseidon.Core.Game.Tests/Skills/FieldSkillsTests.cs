using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Skills;
using tPoseidon.Core.Game.Skills.Effects;

namespace tPoseidon.Core.Game.Tests.Skills;

public sealed class FieldSkillsTests
{
    [Fact]
    public void ElementAreasLowerAResistByTheCastersMagicCappedAtHalf()
    {
        Assert.Equal((int)(40 * ((300 / 400.0 + 0.3) * 0.13f)), SkillEffectKinds.ResistDrop(300, 0.13f, 40));
        Assert.Equal(20, SkillEffectKinds.ResistDrop(5000, 0.18f, 40));
        Assert.Equal(1, SkillEffectKinds.ResistDrop(0, 0.08f, 2));
    }

    [Fact]
    public void AMonsterRemembersAndRestoresItsResistLoss()
    {
        var monster = new MonsterEntity { EntityId = 1, MapId = 1, MonsterParamId = 1, Name = "m", SpawnX = 0, SpawnY = 0, MaxHp = 10, Level = 1 };

        monster.AddResistLoss(SkillActions.FireElement, 12);
        monster.AddResistLoss(SkillActions.FireElement, 3);

        Assert.Equal(15, monster.ResistLoss(SkillActions.FireElement));
        monster.AddResistLoss(SkillActions.FireElement, -15);
        Assert.Equal(0, monster.ResistLoss(SkillActions.FireElement));
    }

    [Theory]
    [InlineData(SkillEffectKinds.WaterArea, SkillActions.ElectricElement)]
    [InlineData(SkillEffectKinds.ThunderArea, SkillActions.FireElement)]
    [InlineData(SkillEffectKinds.FireArea, SkillActions.EarthElement)]
    [InlineData(SkillEffectKinds.EarthArea, SkillActions.WindElement)]
    [InlineData(SkillEffectKinds.WindArea, SkillActions.WaterElement)]
    public void EachElementAreaWeakensTheNextElement(int area, int lowered)
    {
        var field = SkillEffectKinds.Find(area)!;
        var child = SkillEffectKinds.Find(field.FieldChild)!;

        Assert.Equal(area + 1, field.FieldChild);
        Assert.Equal(5, field.FieldLimitCap);
        Assert.True(field.FieldTracks);
        Assert.Equal(lowered, child.LowersResistElement);
        Assert.True(child.RollsChance);
        Assert.True(GroundSkills.IsGround(area));
    }

    [Fact]
    public void FireFieldBurnsTrackedMonstersEveryTwoSeconds()
    {
        var field = SkillEffectKinds.Find(SkillEffectKinds.FireField)!;
        var burn = SkillEffectKinds.Find(SkillEffectKinds.FireFieldOnMonster)!;

        Assert.Equal(SkillEffectTail.ModeRadiusPoint, field.Tail);
        Assert.Equal(7, field.FieldLimitCap);
        Assert.Equal(SkillPeriodicHit.RepeatingStrike, burn.Periodic);
        Assert.Equal(2000, burn.PeriodMs);
        Assert.Equal(1f, burn.FixedCoefficient);
        Assert.Equal((ushort)1, burn.HeaderState);
    }

    [Fact]
    public void WhirlWindFollowsTheCasterAndHitsOnCreation()
    {
        var wind = SkillEffectKinds.Find(SkillEffectKinds.WhirlWind)!;
        var hit = SkillEffectKinds.Find(SkillEffectKinds.WhirlWindOnMonster)!;

        Assert.True(wind.FollowsCaster);
        Assert.False(GroundSkills.IsGround(SkillEffectKinds.WhirlWind));
        Assert.True(hit.StrikesOnStart);
        Assert.False(hit.StrikeGatesMode);
        Assert.Equal(300, hit.FixedDurationMs);
    }
}
