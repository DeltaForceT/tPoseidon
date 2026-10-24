using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Items;

public sealed class EquipMergeRulesTests
{
    private static ItemParamEntry Equipment(int id = 45001, int type = 2, int refineIndex = 35) =>
        new() { Id = id, Class = 2, Type = type, RefineIndex = refineIndex, Value = 1000 };

    private static ItemProperty Stat(int id, short value) => new(ItemPropertyTypes.Stat, id, value, 0);

    [Fact]
    public void OnlyTheSameChaosEquipmentFuses()
    {
        Assert.Equal(EquipMergeRules.Ok, EquipMergeRules.Check(Equipment(), Equipment()));
        Assert.Equal(EquipMergeRules.NotEquipment, EquipMergeRules.Check(Equipment(type: 5), Equipment(type: 5)));
        Assert.Equal(EquipMergeRules.NotSame, EquipMergeRules.Check(Equipment(), Equipment(id: 45002)));
        Assert.Equal(EquipMergeRules.NotMergeable, EquipMergeRules.Check(Equipment(refineIndex: 11), Equipment(refineIndex: 11)));
        Assert.Equal(EquipMergeMode.Better, EquipMergeRules.ModeOf(64));
        Assert.Equal(2000, EquipMergeRules.Cost(Equipment()));
    }

    [Fact]
    public void TheChanceIsTheBaseRowPlusTheSupportRow()
    {
        var supports = new Dictionary<int, int> { [0] = 70, [19818] = 30 };
        Assert.Equal(70, EquipMergeRules.Chance(supports, null));
        Assert.Equal(100, EquipMergeRules.Chance(supports, 19818));
        Assert.True(EquipMergeRules.Succeeds(70, 6999));
        Assert.True(EquipMergeRules.Succeeds(70, 7000));
        Assert.False(EquipMergeRules.Succeeds(70, 7001));
    }

    [Fact]
    public void StatsMergeByModeAndTheLookStaysWithTheMain()
    {
        var look = new ItemProperty(12, 26000, 0, 87010);
        var main = new[] { Stat(10000, 10), Stat(10002, -5), look };
        var sub = new[] { Stat(10000, 20), Stat(10002, -9), Stat(10004, 6) };
        var better = EquipMergeRules.MergeStats(main, sub, EquipMergeMode.Better, (low, _) => low);
        Assert.Contains(look, better);
        Assert.Contains(Stat(10000, 20), better);
        Assert.Contains(Stat(10002, -9), better);
        Assert.Contains(Stat(10004, 6), better);
        var random = EquipMergeRules.MergeStats(main, sub, EquipMergeMode.Random, (low, high) => high - 1);
        Assert.Contains(Stat(10000, 19), random);
        Assert.Contains(Stat(10002, -6), random);
        Assert.Contains(Stat(10004, 5), random);
        Assert.Equal(3, EquipMergeRules.MergeMixSlots(1, 3, EquipMergeMode.Better, (low, _) => low));
        Assert.Equal(1, EquipMergeRules.MergeMixSlots(1, 3, EquipMergeMode.Random, (low, _) => low));
    }
}
