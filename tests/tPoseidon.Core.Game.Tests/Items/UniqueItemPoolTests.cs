using tPoseidon.Core.Game.Items;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Items;

public sealed class UniqueItemPoolTests
{
    private static UniqueItemPool Pool() => new([new LegendItemGroupEntry(2097, [(50001, 0.2f), (54001, 0.3f), (56001, 0.3f), (50017, 0.2f)])]);

    [Fact]
    public void LegendKeyFollowsTheCumulativeRatios()
    {
        var pool = Pool();
        Assert.Equal(50001, pool.RollLegendKey(2097, _ => 0));
        Assert.Equal(50001, pool.RollLegendKey(2097, _ => 19));
        Assert.Equal(54001, pool.RollLegendKey(2097, _ => 20));
        Assert.Equal(56001, pool.RollLegendKey(2097, _ => 79));
        Assert.Equal(50017, pool.RollLegendKey(2097, _ => 99));
        Assert.Null(pool.RollLegendKey(1, _ => 0));
    }

    [Fact]
    public void AddCountSendsWholeUnitsAndPopsIssuedItems()
    {
        var pool = Pool();
        Assert.Equal(new UniqueCountResult(0, null), pool.AddCount(0, 0.4f));
        Assert.Equal(new UniqueCountResult(1, null), pool.AddCount(0, 0.7f));
        pool.Issue(50001, [50001, 50001]);
        Assert.Equal(new UniqueCountResult(1, 50001), pool.AddCount(50001, 1f));
        Assert.Equal(new UniqueCountResult(1, 50001), pool.AddCount(50001, 1f));
        Assert.Equal(new UniqueCountResult(1, null), pool.AddCount(50001, 1f));
    }
}
