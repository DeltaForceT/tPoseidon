using tPoseidon.Core.Game.Items;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Items;

public sealed class KeyBoxRulesTests
{
    private static readonly KeyboxEntry Row = new(100, [11, 12, 13, 14], true, 500, 600, 700, 30);
    private static readonly KeyBoxItem Box = new(100, KeyBoxRules.BoxEffect, 1);
    private static readonly KeyBoxItem Key = new(12, KeyBoxRules.KeyEffect, 1);

    [Theory]
    [InlineData(KeyBoxRules.BoxEffect, 1, 0, 3, true)]
    [InlineData(KeyBoxRules.BoxEffect, 0, 0, 3, false)]
    [InlineData(KeyBoxRules.KeyEffect, 1, 0, 3, false)]
    [InlineData(KeyBoxRules.BoxEffect, 1, KeyBoxRules.SafeZoneOption, 1, true)]
    [InlineData(KeyBoxRules.BoxEffect, 1, KeyBoxRules.SafeZoneOption, 2, true)]
    [InlineData(KeyBoxRules.BoxEffect, 1, KeyBoxRules.SafeZoneOption, 5, true)]
    [InlineData(KeyBoxRules.BoxEffect, 1, KeyBoxRules.SafeZoneOption, 3, false)]
    public void TheWindowNeedsAHeldBoxAndTheSafeZoneWhenTheItemAsksForIt(int effect, int stack, int options, int zoneType, bool opens)
    {
        Assert.Equal(opens, KeyBoxRules.CanOpen(new KeyBoxItem(100, effect, stack), options, zoneType));
    }

    [Fact]
    public void NoItemNeverOpensTheWindow()
    {
        Assert.False(KeyBoxRules.CanOpen(null, 0, 1));
    }

    [Fact]
    public void TheRollIsOneToNinetyNine()
    {
        Assert.Equal(1, KeyBoxRules.RollPercent(bound => { Assert.Equal(99, bound); return 0; }));
        Assert.Equal(99, KeyBoxRules.RollPercent(_ => 98));
    }

    [Fact]
    public void TheChecksRunInTheServerOrder()
    {
        Assert.Equal(KeyBoxRefusal.NotUsable, KeyBoxRules.Choose(null, Row, true, Key, 0, () => 1).Refusal);
        Assert.Equal(KeyBoxRefusal.NotUsable, KeyBoxRules.Choose(Box with { Stack = 0 }, Row, true, Key, 0, () => 1).Refusal);
        Assert.Equal(KeyBoxRefusal.NotUsable, KeyBoxRules.Choose(Box with { Effect = KeyBoxRules.KeyEffect }, Row, true, Key, 0, () => 1).Refusal);
        Assert.Equal(KeyBoxRefusal.Failed, KeyBoxRules.Choose(Box, null, true, Key, 0, () => 1).Refusal);
        Assert.Equal(KeyBoxRefusal.KeyRequired, KeyBoxRules.Choose(Box, Row, false, null, null, () => 1).Refusal);
        Assert.Equal(KeyBoxRefusal.KeyRejected, KeyBoxRules.Choose(Box, Row, true, null, null, () => 1).Refusal);
        Assert.Equal(KeyBoxRefusal.KeyRejected, KeyBoxRules.Choose(Box, Row, true, Key with { Stack = 0 }, 0, () => 1).Refusal);
        Assert.Equal(KeyBoxRefusal.KeyRejected, KeyBoxRules.Choose(Box, Row, true, Key with { Effect = KeyBoxRules.BoxEffect }, 0, () => 1).Refusal);
        Assert.Equal(KeyBoxRefusal.KeyRejected, KeyBoxRules.Choose(Box, Row, true, Key with { ItemId = 99 }, 0, () => 1).Refusal);
        Assert.Equal(KeyBoxRefusal.Failed, KeyBoxRules.Choose(Box, Row, true, Key, null, () => 1).Refusal);
    }

    [Fact]
    public void OnlyTheFirstThreeKeysOfARowCount()
    {
        Assert.Equal(KeyBoxRefusal.KeyRejected, KeyBoxRules.Choose(Box, Row, true, Key with { ItemId = 14 }, 0, () => 1).Refusal);
        Assert.Equal(KeyBoxRefusal.None, KeyBoxRules.Choose(Box, Row, true, Key with { ItemId = 13 }, 0, () => 1).Refusal);
    }

    [Fact]
    public void ARowWithoutTheKeyOnlyFlagGivesTheNormalPresentWithoutAKey()
    {
        var free = Row with { OnlyKey = false };
        Assert.Equal(new KeyBoxChoice(KeyBoxRefusal.None, 500, false), KeyBoxRules.Choose(Box, free, false, null, null, () => 1));
        Assert.Equal(new KeyBoxChoice(KeyBoxRefusal.None, 600, true), KeyBoxRules.Choose(Box, free, true, Key, 0, () => 99));
    }

    [Theory]
    [InlineData(30, 10, 40, 700)]
    [InlineData(30, 10, 41, 600)]
    [InlineData(30, 0, 30, 700)]
    [InlineData(30, 0, 31, 600)]
    [InlineData(0, 0, 1, 600)]
    public void TheSecondRarePresentComesWhenTheRollIsAtMostTheRateAndTheKeyBonus(int rate, int keyRate, int roll, int present)
    {
        var row = Row with { RareBRatePercent = rate };
        var choice = KeyBoxRules.Choose(Box, row, true, Key, keyRate, () => roll);
        Assert.Equal(new KeyBoxChoice(KeyBoxRefusal.None, present, true), choice);
    }

    [Fact]
    public void WeightSlotsAndResultSizeAreCheckedInThatOrder()
    {
        Assert.Equal(KeyBoxRefusal.None, KeyBoxRules.Fits(100, 50, 151, 1, 1, 3));
        Assert.Equal(KeyBoxRefusal.TooHeavy, KeyBoxRules.Fits(100, 50, 150, 5, 0, 9));
        Assert.Equal(KeyBoxRefusal.BagFull, KeyBoxRules.Fits(100, 50, 200, 2, 1, 9));
        Assert.Equal(KeyBoxRefusal.Failed, KeyBoxRules.Fits(100, 50, 200, 2, 2, 4));
    }
}
