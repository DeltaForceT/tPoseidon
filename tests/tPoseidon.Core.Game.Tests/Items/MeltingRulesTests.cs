using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Items;

public sealed class MeltingRulesTests
{
    private static readonly MeltingInfoEntry Info = new(3.27f, 40.5f, 0.68f, 1.15f, 11.15f, 30);

    private static readonly MeltingDropEntry[] Drops =
    {
        new(1, 433065, 60000, 2101, 32000),
        new(2, 433066, 60000, 1801, 2100),
        new(8, 433072, 60000, 301, 500),
        new(9, 433073, 60000, 0, 300),
    };

    private static ItemParamEntry Equipment(int minLevel = 40, int optionsEx = 0, params int[] stats) =>
        new() { Id = 40001, Class = 2, MinLevel = minLevel, ExtendedOptions = optionsEx, StatBonuses = stats };

    [Fact]
    public void ItemChecksFollowTheErrorOrder()
    {
        Assert.Equal(MeltingRules.NoItem, MeltingRules.CheckItem(false, false, false, null));
        Assert.Equal(MeltingRules.Equipped, MeltingRules.CheckItem(false, true, false, null));
        Assert.Equal(MeltingRules.InvalidItemState, MeltingRules.CheckItem(true, false, true, Equipment()));
        Assert.Equal(MeltingRules.NoItemInfo, MeltingRules.CheckItem(true, false, false, null));
        Assert.Equal(MeltingRules.BelowLevel, MeltingRules.CheckItem(true, false, false, Equipment(minLevel: 29)));
        Assert.Equal(MeltingRules.NotMeltable, MeltingRules.CheckItem(true, false, false, Equipment() with { Class = 1 }));
        Assert.Equal(MeltingRules.Ok, MeltingRules.CheckItem(true, false, false, Equipment(minLevel: 30)));
    }

    [Fact]
    public void StatQualityUsesTheReformRangeAndTheStoredStat()
    {
        var plain = Equipment(40, 0, 100, 0, 8);
        Assert.Equal(41, MeltingRules.StatQuality(plain, Array.Empty<ItemProperty>(), -25, 35));
        Assert.Equal(100, MeltingRules.StatQuality(plain, new[] { new ItemProperty(ItemPropertyTypes.Stat, 10000, 135, 0) }, -25, 35));
        Assert.Equal(0, MeltingRules.StatQuality(Equipment(40, 0, 8), Array.Empty<ItemProperty>(), -25, 35));
        Assert.Equal(0, MeltingRules.StatQuality(Equipment(40, 1, 100), Array.Empty<ItemProperty>(), -25, 35));
    }

    [Fact]
    public void ScoreBandAndFee()
    {
        var item = new Item { ItemUid = 1, ItemIndex = 40001, ItemClass = 2, ItemSlot = 2, RefineLevel = 3, CharacterId = 1, SlotKind = SlotKind.Inventory, SlotPosition = 0 };
        var score = MeltingRules.Score(Info, Equipment(), item, 41);
        Assert.Equal(292, score);
        Assert.Equal(9, MeltingRules.Band(Drops, score)!.Id);
        Assert.Equal(8, MeltingRules.Band(Drops, 301)!.Id);
        Assert.Equal(1, MeltingRules.Band(Drops, 2101)!.Id);
        Assert.Null(MeltingRules.Band(Drops, 32001));
        Assert.Equal(180000, MeltingRules.Fee(Drops[3], 3));
    }
}
