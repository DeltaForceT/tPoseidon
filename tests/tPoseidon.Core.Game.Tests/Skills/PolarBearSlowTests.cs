using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Skills.Effects;

namespace tPoseidon.Core.Game.Tests.Skills;

public sealed class PolarBearSlowTests
{
    private static MonsterEntity Monster() => new()
    {
        EntityId = 0x5000, MapId = 3, MonsterParamId = 1, Name = "m", SpawnX = 100, SpawnY = 100, MaxHp = 100, Level = 1,
    };

    [Fact]
    public void ASlowLowersWalkByOneHundredAndRunByOneHundredSixtyPerRatio()
    {
        var monster = Monster();
        monster.SlowRatio = 0.45f;

        Assert.Equal(300f - 45f, monster.WalkSpeedOf(300f), 3);
        Assert.Equal(500f - 72f, monster.RunSpeedOf(500f), 3);
        Assert.Equal(((300f + 500f) / 2f) - 58.5f, (monster.WalkSpeedOf(300f) + monster.RunSpeedOf(500f)) / 2f, 3);
    }

    [Fact]
    public void AnUnslowedMonsterKeepsItsTableSpeeds()
    {
        var monster = Monster();

        Assert.Equal(300f, monster.WalkSpeedOf(300f));
        Assert.Equal(500f, monster.RunSpeedOf(500f));
    }

    [Fact]
    public void ASlowPastTheTableSpeedLeavesTheMonsterUnableToMove()
    {
        var monster = Monster();
        monster.SlowRatio = 0.45f;

        Assert.True(monster.WalkSpeedOf(40f) <= 0f);
    }

    [Fact]
    public void BearsRaidSlowsByItsRunSpeedColumn()
    {
        var kind = SkillEffectKinds.Find(1543)!;

        Assert.Equal(SkillEffectKinds.SlowRatioColumn, kind.SlowColumn);
        Assert.Equal("RunSpeed", kind.SlowColumn);
        Assert.True(kind.RollsChance);
        Assert.True(kind.OnTarget);
    }
}
