using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Skills.Effects;

namespace tPoseidon.Core.Game.Tests.Skills;

public sealed class PolarBearStunTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    private static MonsterEntity Monster()
    {
        var monster = new MonsterEntity
        {
            EntityId = 0x5000, MapId = 3, MonsterParamId = 1, Name = "m", SpawnX = 100, SpawnY = 100, MaxHp = 100, Level = 1,
        };
        monster.InitPosition(100, 100);
        return monster;
    }

    [Fact]
    public void AStunnedMonsterStopsWhereItIsUntilTheStunEnds()
    {
        var monster = Monster();
        monster.MoveTo(200, 100, Now, 1000);

        var (x, _) = monster.HoldUntil(Now.AddMilliseconds(3500), Now.AddMilliseconds(500));

        Assert.Equal(150, x);
        Assert.Equal((150, 100), ((int)monster.GetInterpolatedPosition(Now.AddSeconds(2)).x, (int)monster.GetInterpolatedPosition(Now.AddSeconds(2)).y));
        Assert.True(monster.IsHeld(Now.AddSeconds(3)));
        Assert.False(monster.IsHeld(Now.AddMilliseconds(3500)));
    }

    [Fact]
    public void MentalBreakRollsItsRatioAndHoldsTheTarget()
    {
        var kind = SkillEffectKinds.Find(1526)!;

        Assert.True(kind.RollsChance);
        Assert.Equal(SkillEffectKinds.RatioColumn, kind.ChanceColumn);
        Assert.True(kind.Holds);
        Assert.Equal(SkillEffectTail.FlagThenDuration, kind.Tail);
    }
}
