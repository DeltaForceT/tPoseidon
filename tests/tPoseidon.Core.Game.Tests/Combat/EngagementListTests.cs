using tPoseidon.Core.Game.Combat;

namespace tPoseidon.Core.Game.Tests.Combat;

public sealed class EngagementListTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ATargetLeavesFiveSecondsAfterItsLastRefresh()
    {
        var list = new EngagementList();
        list.Refresh(0x0301, Start);

        Assert.Empty(list.TakeExpired(Start.AddMilliseconds(4999)));
        list.Refresh(0x0301, Start.AddMilliseconds(4000));
        Assert.Empty(list.TakeExpired(Start.AddMilliseconds(8999)));
        Assert.Equal(new ushort[] { 0x0301 }, list.TakeExpired(Start.AddMilliseconds(9000)));
        Assert.False(list.Contains(0x0301));
    }

    [Fact]
    public void AFullListDropsItsOldestTargetForANewOne()
    {
        var list = new EngagementList(capacity: 2);

        Assert.Null(list.Refresh(1, Start));
        Assert.Null(list.Refresh(2, Start.AddMilliseconds(10)));
        Assert.Null(list.Refresh(1, Start.AddMilliseconds(20)));
        Assert.Equal((ushort)1, list.Refresh(3, Start.AddMilliseconds(30)));
        Assert.True(list.Contains(2));
        Assert.True(list.Contains(3));
    }

    [Fact]
    public void TakeAllEmptiesTheListInOrder()
    {
        var list = new EngagementList();
        list.Refresh(7, Start);
        list.Refresh(5, Start);

        Assert.Equal(new ushort[] { 7, 5 }, list.TakeAll());
        Assert.Empty(list.TakeAll());
    }
}
