using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Gacha;

namespace tPoseidon.Core.Game.Tests.Gacha;

public sealed class GachaPickerTests
{
    private static GachaPrize Prize(int item, int weight) => new(item, ChargedItemRules.CommonClass, 1, 0, 0, weight);

    private static readonly GachaPrize[] Pool = [Prize(19001, 1), Prize(19002, 3), Prize(19003, 1)];

    [Theory]
    [InlineData(0, 19001)]
    [InlineData(1, 19002)]
    [InlineData(3, 19002)]
    [InlineData(4, 19003)]
    public void EachRollLandsOnTheEntryItsWeightCovers(int roll, int expected)
    {
        Assert.Equal(expected, GachaPicker.Pick(Pool, _ => roll).ItemIndex);
    }

    [Fact]
    public void EveryEntryGetsExactlyItsWeightOfRolls()
    {
        var total = GachaPicker.TotalWeight(Pool);
        var counts = Enumerable.Range(0, total)
            .GroupBy(roll => GachaPicker.Pick(Pool, _ => roll).ItemIndex)
            .ToDictionary(group => group.Key, group => group.Count());

        Assert.Equal(5, total);
        Assert.Equal(new Dictionary<int, int> { [19001] = 1, [19002] = 3, [19003] = 1 }, counts);
    }

    [Fact]
    public void TheRollIsAskedBelowTheTotalWeight()
    {
        var asked = 0;
        GachaPicker.Pick(Pool, bound => { asked = bound; return 0; });

        Assert.Equal(5, asked);
    }

    [Fact]
    public void RollsOutsideThePoolAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GachaPicker.Pick(Pool, _ => 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => GachaPicker.Pick(Pool, _ => -1));
    }

    [Fact]
    public void EmptyPoolsAndNonPositiveWeightsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => GachaPicker.Pick([], _ => 0));
        Assert.Throws<ArgumentException>(() => GachaPicker.TotalWeight([Prize(19001, 0)]));
    }

    [Fact]
    public void TheDefaultPickerDrawsFromThePool()
    {
        Assert.Contains(GachaPicker.Pick(Pool), Pool);
    }

    [Fact]
    public void DeepDrillsCostTwoCouponsAndOnlyFourAreasExist()
    {
        Assert.Equal(1, GachaRules.CouponsFor(deep: false));
        Assert.Equal(2, GachaRules.CouponsFor(deep: true));
        Assert.False(GachaRules.IsArea(60));
        Assert.True(GachaRules.IsArea(61));
        Assert.True(GachaRules.IsArea(64));
        Assert.False(GachaRules.IsArea(65));
    }
}
