using tPoseidon.Core.Game.Pets;

namespace tPoseidon.Core.Game.Tests.Pets;

public sealed class OwnerWalkTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TheOwnerIsPartWayAlongTheWalkUntilArrival()
    {
        var walk = OwnerWalk.Start(0, 0, 300, 400, 250f, Now);

        Assert.Equal(Now.AddSeconds(2), walk.ArrivesAt);
        Assert.Equal((150, 200), walk.PositionAt(Now.AddSeconds(1)));
        Assert.Equal((300, 400), walk.PositionAt(Now.AddSeconds(5)));
    }

    [Fact]
    public void WithoutASpeedTheOwnerIsAlreadyThere()
    {
        Assert.Equal((300, 400), OwnerWalk.Start(0, 0, 300, 400, 0f, Now).PositionAt(Now));
    }
    [Fact]
    public void AWalkAlongWaypointsTurnsAtEachOne()
    {
        var walk = OwnerWalk.Along(0, 0, [(300, 0), (300, 400)], 100f, Now);

        Assert.Equal(Now.AddSeconds(7), walk.ArrivesAt);
        Assert.Equal((300, 400), (walk.ToX, walk.ToY));
        Assert.Equal((200, 0), walk.PositionAt(Now.AddSeconds(2)));
        Assert.Equal((300, 100), walk.PositionAt(Now.AddSeconds(4)));
        Assert.Equal((300, 400), walk.PositionAt(Now.AddSeconds(8)));
    }
}
