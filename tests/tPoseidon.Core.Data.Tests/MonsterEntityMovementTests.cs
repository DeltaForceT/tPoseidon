namespace tPoseidon.Core.Data.Tests;

public class MonsterEntityMovementTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    private static MonsterEntity NewMob(ushort x, ushort y)
    {
        var mob = new MonsterEntity
        {
            EntityId = 0x2001,
            MapId = 10,
            MonsterParamId = 1,
            Name = "Walker",
            SpawnX = x,
            SpawnY = y,
            MaxHp = 60,
            Level = 1,
        };
        mob.InitPosition(x, y);
        return mob;
    }

    [Fact]
    public void MoveAlong_PositionFollowsTheWaypointsOverTime()
    {
        var mob = NewMob(100, 100);

        mob.MoveAlong([(200, 100), (200, 200)], Start, 2000);

        Assert.Equal(((ushort)150, (ushort)100), mob.GetInterpolatedPosition(Start.AddMilliseconds(500)));
        Assert.Equal(((ushort)200, (ushort)100), mob.GetInterpolatedPosition(Start.AddMilliseconds(1000)));
        Assert.Equal(((ushort)200, (ushort)150), mob.GetInterpolatedPosition(Start.AddMilliseconds(1500)));
        Assert.Equal(((ushort)200, (ushort)200), mob.GetInterpolatedPosition(Start.AddMilliseconds(2500)));
    }

    [Fact]
    public void MoveAlong_DuringAMove_StartsFromWhereTheMonsterIsNow()
    {
        var mob = NewMob(100, 100);
        mob.MoveAlong([(300, 100)], Start, 2000);

        var (fromX, fromY) = mob.MoveAlong([(200, 300)], Start.AddMilliseconds(1000), 1000);

        Assert.Equal(((ushort)200, (ushort)100), (fromX, fromY));
        Assert.Equal(((ushort)200, (ushort)200), mob.GetInterpolatedPosition(Start.AddMilliseconds(1500)));
    }

    [Fact]
    public void MoveTo_AfterArrival_StartsFromTheDestination()
    {
        var mob = NewMob(100, 100);
        mob.MoveTo(300, 100, Start, 1000);

        var from = mob.MoveTo(300, 300, Start.AddMilliseconds(1500), 1000);

        Assert.Equal(((ushort)300, (ushort)100), from);
    }
}
