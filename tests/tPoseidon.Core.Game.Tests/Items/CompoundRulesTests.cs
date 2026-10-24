using tPoseidon.Core.Game.Items;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Items;

public sealed class CompoundRulesTests
{
    private static readonly CompoundRecipe Potion = new()
    {
        Id = 101,
        Type = CompoundRecipe.PotionType,
        Results = new[] { 12005 },
        Requirements = new[] { new CompoundRequirement(1000, 1), new CompoundRequirement(105, 1), new CompoundRequirement(11093, 1) },
        Probability = 45,
        Fee = 3,
        WasteItem = 12000,
    };

    private static CompoundMaterial[] Materials(int count) =>
        new[] { new CompoundMaterial(1, 1000, count), new CompoundMaterial(2, 105, count), new CompoundMaterial(3, 11093, count) };

    [Fact]
    public void EveryRequirementCoveredForTheCountMatches()
    {
        Assert.Equal(CompoundRules.Matches, CompoundRules.CheckRecipe(Potion, Materials(3), 3));
    }

    [Fact]
    public void TooFewOfAMaterialIsRefused()
    {
        Assert.Equal(CompoundRules.Refused, CompoundRules.CheckRecipe(Potion, Materials(2), 3));
    }

    [Fact]
    public void AnExtraMaterialIsNotAnExactMatch()
    {
        var materials = Materials(1).Append(new CompoundMaterial(4, 777, 1)).ToArray();

        Assert.Equal(CompoundRules.MatchesWithExtra, CompoundRules.CheckRecipe(Potion, materials, 1));
    }

    [Fact]
    public void RareRecipesTakeACountOfOne()
    {
        var rare = Potion with { Type = CompoundRecipe.RareType };

        Assert.Equal(CompoundRules.Refused, CompoundRules.CheckRecipe(rare, Materials(2), 2));
        Assert.Equal(CompoundRules.Matches, CompoundRules.CheckRecipe(rare, Materials(1), 1));
    }

    [Theory]
    [InlineData(-5, 0, 0)]
    [InlineData(0, 0, 5)]
    [InlineData(0, 99, 0)]
    [InlineData(10, 0, 10)]
    [InlineData(10, 99, 5)]
    [InlineData(20, 0, 20)]
    [InlineData(30, 99, 10)]
    [InlineData(40, 99, 10)]
    public void TheBonusFollowsTheStatBands(int stat, int roll, int bonus)
    {
        Assert.Equal(bonus, CompoundRules.Bonus(stat, () => roll));
    }

    [Fact]
    public void ThePercentIsCappedAtOneHundred()
    {
        Assert.Equal(100, CompoundRules.Percent(Potion with { Probability = 98 }, 40, () => 0));
        Assert.Equal(0, CompoundRules.Percent(null, 40, () => 0));
    }

    [Fact]
    public void ModeTenRollsEachItemAndModeSixHalvesThePercent()
    {
        Assert.Equal(new CompoundRoll(45, 3, 0, true), CompoundRules.Roll(CompoundRules.NormalMode, 45, 3, () => 10));
        Assert.Equal(new CompoundRoll(22, 0, 3, true), CompoundRules.Roll(CompoundRules.HalvedMode, 45, 3, () => 30));
    }

    [Fact]
    public void TheStubModesFailEverythingAndUnknownModesAreRefused()
    {
        Assert.Equal(new CompoundRoll(0, 0, 4, false), CompoundRules.Roll(CompoundRules.FirstStubMode, 45, 4, () => 0));
        Assert.Null(CompoundRules.Roll(7, 45, 4, () => 0));
    }

    [Fact]
    public void LargeCountsRollInStepsThatStillAddUp()
    {
        var roll = CompoundRules.Roll(CompoundRules.NormalMode, 50, 100, () => 0)!.Value;

        Assert.Equal(100, roll.Successes + roll.Failures);
        Assert.Equal(100, roll.Successes);
    }

    [Theory]
    [InlineData(3, 3, 9)]
    [InlineData(1000, 10, 9000)]
    [InlineData(5, 0, -1)]
    [InlineData(-1, 2, -1)]
    public void TheFeeIsDiscountedFromTen(int fee, int count, long expected)
    {
        Assert.Equal(expected, CompoundRules.Fee(fee, count));
    }
}
