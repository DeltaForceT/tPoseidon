using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Items;

public sealed class GrowthRulesTests
{
    private static readonly MaturingCompoundRow Material = new(60001, 10000, 5, 15, 1, 0, "compound1");
    private static readonly GrowthAid[] NoAids = Array.Empty<GrowthAid>();

    [Fact]
    public void AnItemWithoutPropertiesOrSlotsHasNoFreeSlot()
    {
        Assert.Equal(GrowthRules.NoSlot, GrowthRules.FreeSlot(Array.Empty<ItemProperty>(), 2));
        Assert.Equal(GrowthRules.NoSlot, GrowthRules.FreeSlot(new[] { new ItemProperty(ItemPropertyTypes.RefineBonus, 10000, 10, 0) }, 0));
    }

    [Fact]
    public void TheLowestUnusedSlotIsChosenUntilTheCountIsReached()
    {
        var list = new[] { new ItemProperty(ItemPropertyTypes.RefineBonus, 10000, 10, 0), new ItemProperty(0, 10000, 7, 60001) };

        Assert.Equal(1, GrowthRules.FreeSlot(list, 2));
        Assert.Equal(GrowthRules.NoSlot, GrowthRules.FreeSlot(list, 1));
    }

    [Fact]
    public void FeeIsFiveTimesTheValue()
    {
        Assert.Equal(5000, GrowthRules.Fee(new ItemParamEntry { Id = 1, Value = 1000 }, 0));
    }

    [Fact]
    public void ChanceFollowsLuckAndTheItemLevel()
    {
        Assert.Equal(0, GrowthRules.Chance(1f, NoAids, 0, 100, 0));
        Assert.Equal(147, GrowthRules.Chance(1f, NoAids, 100, 100, 0));
        Assert.Equal(177, GrowthRules.Chance(1f, NoAids, 100, 100, 1.2f));
    }

    [Fact]
    public void AidsShiftTheRangeByAtLeastOne()
    {
        var aids = new[] { new GrowthAid(0, 0.01f, 0.01f) };

        Assert.Equal((1, 1), GrowthRules.Deltas(Material, aids));
        Assert.Equal((0, 0), GrowthRules.Deltas(Material, NoAids));
        Assert.Equal(16, GrowthRules.Ability(Material, (1, 1), 100));
        Assert.Equal(6, GrowthRules.FailedAbility(Material, (1, 1)));
        Assert.True(GrowthRules.AidsClash(new GrowthAid(1, 0, 0), new GrowthAid(2, 0, 0)));
        Assert.False(GrowthRules.AidsClash(new GrowthAid(1, 0, 0), new GrowthAid(0, 1, 0)));
    }
}
