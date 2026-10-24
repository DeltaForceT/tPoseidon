using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Items;

public sealed class EquipReformRulesTests
{
    private static readonly int[] ReformSteps = [1, 9, 13, 15, 16, 15, 13, 10, 5, 2, 1];
    private static readonly ItemProperty[] NoProperties = [];

    private static EquipReformTables Tables() => new(
        [new ItemReformEntry(20, 34, 63132, 3), new ItemReformEntry(35, 49, 63133, 3)],
        new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal) { ["reform_wp"] = ReformSteps, ["reform_cl"] = ReformSteps },
        -25,
        35,
        new Dictionary<int, ReformElixirEntry> { [19779] = new(19779, 15, 15) },
        0f);

    private static RefineTables RefineTables() => new()
    {
        LevelRows = new Dictionary<int, RefineLevelRow>
        {
            [1] = new(1, [5, 10, 20, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], new float[13]),
        },
    };

    private static ItemParamEntry Sword(int value = 1000, int reformCount = 5, int options = 0) => new()
    {
        Id = 87003,
        Class = 2,
        Type = 2,
        MinLevel = 30,
        Value = value,
        ReformCount = reformCount,
        RefineType = 1,
        RefineIndex = 1,
        Options = options,
        StatBonuses = [100, 0, 20, 0, 0, 0, 0, 0, 0, 0, 0, 0],
    };

    private static EquipReformRequest Request(ReformStatChoice first, ReformStatChoice second, ulong secondElixir = 0) =>
        new(0x11, 0x22, 0x33, secondElixir, first, second);

    private static readonly ReformStatChoice NoChoice = new(ReformStatChoice.None, 0);

    [Fact]
    public void FeeIsValuePlusThreeQuartersOrLevelTimesHundred()
    {
        Assert.Equal(1750, EquipReformRules.Fee(Sword(), 0f));
        Assert.Equal(3000, EquipReformRules.Fee(Sword(value: 0), 0f));
        Assert.Equal(875, EquipReformRules.Fee(Sword(), 0.5f));
    }

    [Fact]
    public void RangeShiftAndRollFollowTheServerArithmetic()
    {
        Assert.Equal((75, 135), StatReroll.Range(100, -25, 35));
        Assert.Equal((-8, -13), StatReroll.Range(-10, -25, 35));
        Assert.Equal(87, StatReroll.Shift(75, 15));
        Assert.Equal(156, StatReroll.Shift(135, 15));
        Assert.Equal(23, StatReroll.Shift(20, 15));
        Assert.Equal(75, StatReroll.Roll(75, 135, 0));
        Assert.Equal(105, StatReroll.Roll(75, 135, 50));
        Assert.Equal(135, StatReroll.Roll(75, 135, 100));
    }

    [Fact]
    public void CurveEndsOnTheTopBin()
    {
        var curve = StatReroll.Curve(ReformSteps);
        Assert.Equal(ItemPropertyRules.CurveRolls, curve.Length);
        Assert.Equal(100, curve[^1]);
        Assert.Equal(0, curve[0]);
        Assert.All(curve, bin => Assert.InRange(bin, 0, 100));
    }

    [Theory]
    [InlineData(2, "reform_wp")]
    [InlineData(3, "reform_ht")]
    [InlineData(7, "reform_ac")]
    [InlineData(8, "reform_sd")]
    [InlineData(0x18, "reform_hd")]
    [InlineData(0x19, "reform_fc")]
    [InlineData(0x22, "reform_cp")]
    [InlineData(4, "reform_cl")]
    public void ItemTypePicksTheDistribution(int type, string curve)
    {
        Assert.Equal(curve, EquipReformRules.CurveFor(type));
    }

    [Fact]
    public void ReformRollsEveryBaseStatAndBoostsTheElixirStat()
    {
        var outcome = EquipReformRules.Reform(Sword(), NoProperties, false, 2,
            Request(new ReformStatChoice(0, 19779), NoChoice), Tables(), RefineTables(), () => ItemPropertyRules.CurveRolls - 1);
        Assert.Equal([new ItemProperty(10, 10000, 156, 0), new ItemProperty(10, 10002, 27, 0)], outcome.Rolled);
        Assert.Equal(4, outcome.Remaining);
        Assert.Equal(new ReformRefineBonus(10000, 15), outcome.Refine);
        Assert.Contains(new ItemProperty(10, EquipReformRules.RemainingId, 4, 0), outcome.Stored);
        Assert.Contains(new ItemProperty(ItemPropertyTypes.RefineBonus, 10000, 15, 0), outcome.Stored);
    }

    [Fact]
    public void ReformCountsDownFromTheStoredValueAndKeepsOtherProperties()
    {
        ItemProperty[] stored = [new(10, EquipReformRules.RemainingId, 2, 0), new(12, 26000, 0, 87100), new(11, 10000, 99, 0)];
        var outcome = EquipReformRules.Reform(Sword(), stored, false, 0, Request(NoChoice, NoChoice), Tables(), RefineTables(), () => 0);
        Assert.Equal(1, outcome.Remaining);
        Assert.Equal(ReformRefineBonus.None, outcome.Refine);
        Assert.Contains(new ItemProperty(12, 26000, 0, 87100), outcome.Stored);
        Assert.Contains(new ItemProperty(11, 10000, 99, 0), outcome.Stored);
        Assert.Contains(new ItemProperty(10, 10000, 75, 0), outcome.Stored);
    }

    [Fact]
    public void MaterialMustMatchTheLevelRowAndAReformMustRemain()
    {
        var tables = Tables();
        Assert.Equal(0u, EquipReformRules.CheckMaterial(Sword(), NoProperties, tables.MaterialFor(30), 63132, 3));
        Assert.Equal(EquipReformRules.Failed, EquipReformRules.CheckMaterial(Sword(), NoProperties, tables.MaterialFor(30), 63133, 3));
        Assert.Equal(EquipReformRules.Failed, EquipReformRules.CheckMaterial(Sword(), NoProperties, tables.MaterialFor(30), 63132, 2));
        Assert.Equal(EquipReformRules.Failed, EquipReformRules.CheckMaterial(Sword(), NoProperties, tables.MaterialFor(19), 63132, 3));
        Assert.Equal(EquipReformRules.Failed, EquipReformRules.CheckMaterial(Sword(reformCount: 0), NoProperties, tables.MaterialFor(30), 63132, 3));
        Assert.Equal(EquipReformRules.Failed, EquipReformRules.CheckMaterial(Sword(options: 0x1000000), NoProperties, tables.MaterialFor(30), 63132, 3));
        Assert.Equal(EquipReformRules.Failed, EquipReformRules.CheckMaterial(Sword(), [new(10, EquipReformRules.RemainingId, 0, 0)], tables.MaterialFor(30), 63132, 3));
    }

    [Fact]
    public void ResetNeedsAUsedCountAnEffect38MaterialAndAReformableItem()
    {
        var feather = new ItemParamEntry { Id = 19731, Class = 1, Effect = 0x26 };
        var other = new ItemParamEntry { Id = 19000, Class = 1, Effect = 1 };
        ItemProperty[] used = [new(10, EquipReformRules.RemainingId, 1, 0)];
        Assert.Equal(EquipReformRules.AtMaximum, EquipReformRules.CheckReset(Sword(), NoProperties, feather));
        Assert.Equal(EquipReformRules.AtMaximum, EquipReformRules.CheckReset(Sword(), [new(10, EquipReformRules.RemainingId, 5, 0)], feather));
        Assert.Equal(EquipReformRules.BadMaterial, EquipReformRules.CheckReset(Sword(), used, other));
        Assert.Equal(EquipReformRules.NotReformable, EquipReformRules.CheckReset(Sword(options: 0x1000000), used, feather));
        Assert.Equal(0u, EquipReformRules.CheckReset(Sword(), used, feather));
        Assert.Contains(new ItemProperty(10, EquipReformRules.RemainingId, 5, 0), EquipReformRules.Reset(Sword(), used));
    }
}
