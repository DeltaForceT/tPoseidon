using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Items;

public sealed class ItemPropertyAndRefineRulesTests
{
    private static readonly int[] SwordBonuses = { 100, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
    private static readonly int[] MiddleCurve = { 1800, 1900, 1975, 1700, 1400, 700, 300, 140, 60, 20, 5 };

    private static readonly ItemParamEntry Sword = new()
    {
        Id = 500014,
        Class = ItemParamEntry.RareClass,
        Value = 1000,
        MinLevel = 50,
        RefineType = 1,
        RefineIndex = 11,
        ExtendedOptions = ItemParamEntry.RandomStatsExtendedOption,
        StatBonuses = SwordBonuses,
    };

    private static ItemPropertyTables Tables() => new()
    {
        Curves = new Dictionary<string, IReadOnlyList<int>> { ["wp_middle"] = MiddleCurve },
        RandomStats = new Dictionary<int, RandomStatRow>
        {
            [500014] = new()
            {
                ItemId = 500014,
                Stats = new RandomStatRange?[] { new(100, 140, "wp_middle"), new(1, 3, "wp_middle") },
            },
        },
        Attributes = new Dictionary<int, IReadOnlyList<(int Index, int Value)>> { [500014] = new[] { (0, 5), (20, 7) } },
    };

    [Fact]
    public void DefaultsHoldThePlusColumnsAndTheAttributeRow()
    {
        var defaults = new ItemPropertyRules(Tables()).Defaults(Sword);

        Assert.Equal(new[]
        {
            new ItemProperty(ItemPropertyTypes.Stat, 10000, 100, 0),
            new ItemProperty(ItemPropertyTypes.Stat, 20000, 5, 0),
            new ItemProperty(ItemPropertyTypes.Stat, 21008, 7, 0),
        }, defaults);
    }

    [Fact]
    public void RandomStatsOverrideTheBaseAndAppendNewStatsWithinTheRange()
    {
        var created = new ItemPropertyRules(Tables()).Create(Sword, rollRandom: true, new Random(1));

        var ap = Assert.Single(created, p => p.Id == 10000);
        var ac = Assert.Single(created, p => p.Id == 10001);
        Assert.InRange(ap.Value, 100, 140);
        Assert.InRange(ac.Value, 1, 2);
    }

    [Fact]
    public void TheCurveTableIsTenThousandRollsOfBinsUpToOneHundred()
    {
        var curve = ItemPropertyRules.Curve(MiddleCurve);

        Assert.Equal(ItemPropertyRules.CurveRolls, curve.Length);
        Assert.Equal(0, curve[0]);
        Assert.Equal(ItemPropertyRules.CurveBins - 1, curve[^1]);
        Assert.Equal(2, ItemPropertyRules.Values(1, 3)[^1]);
    }

    [Fact]
    public void TheClientListOmitsBaseStatsEqualToTheTable()
    {
        var list = new[]
        {
            new ItemProperty(ItemPropertyTypes.Stat, 10000, 100, 0),
            new ItemProperty(ItemPropertyTypes.Stat, 10001, 2, 0),
            new ItemProperty(ItemPropertyTypes.RefineBonus, 10000, 10, 0),
        };

        Assert.Equal(new[] { list[1], list[2] }, ItemPropertyRules.ForClient(list, Sword));
    }

    [Fact]
    public void RefineFeeChanceAndBonusFollowTheServer()
    {
        Assert.Equal(3000, RefineRules.Fee(Sword, 2, 0));
        Assert.Equal(15000, RefineRules.Fee(Sword with { Options = RefineRules.CashItemOption }, 2, 0));
        Assert.Equal(9500, RefineRules.Chance(90f, 0, 100f, 5f));
        Assert.Equal(10000, RefineRules.Chance(100f, 0, 100f, 5f));
        Assert.Equal(20, RefineRules.Bonus(100, 20));
        Assert.Equal(10010, RefineRules.PropertyIdFor(2));
        Assert.Null(RefineRules.PropertyIdFor(8));
    }

    [Fact]
    public void BrokenOrMaxedItemsCannotBeRefined()
    {
        var item = new Item { ItemUid = 1, ItemIndex = 500014, ItemClass = 2, RefineLevel = 3, CharacterId = 1, SlotKind = SlotKind.Inventory, SlotPosition = 0 };

        Assert.True(RefineRules.CanRefine(Sword, item));
        Assert.False(RefineRules.CanRefine(Sword, item with { RefineState = RefineRules.Broken }));
        Assert.False(RefineRules.CanRefine(Sword, item with { RefineLevel = RefineRules.MaxLevel }));
    }
}

public sealed class ItemPropertyFullListTests
{
    private static readonly int[] Bonuses = { 100, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

    [Fact]
    public void TheFullListOverlaysStoredStatsOnTheDefaultsAndKeepsOtherProperties()
    {
        var rules = new ItemPropertyRules(new ItemPropertyTables());
        var sword = new ItemParamEntry { Id = 1, Class = ItemParamEntry.RareClass, StatBonuses = Bonuses };
        var stored = new[]
        {
            new ItemProperty(ItemPropertyTypes.Stat, 10000, 120, 0),
            new ItemProperty(ItemPropertyTypes.RefineBonus, 10000, 12, 0),
        };

        Assert.Equal(stored, rules.Full(sword, stored));
        Assert.Equal(new[] { new ItemProperty(ItemPropertyTypes.Stat, 10000, 100, 0) }, rules.Full(sword, System.Array.Empty<ItemProperty>()));
    }
}

public sealed class MixSlotRollTests
{
    private static readonly int[] SlotCurve = { 1000, 900, 800, 700, 600, 500, 400, 300, 200, 100, 50 };

    [Fact]
    public void SlotsAreRolledWithinTheRowRange()
    {
        var rules = new ItemPropertyRules(new ItemPropertyTables
        {
            Curves = new Dictionary<string, IReadOnlyList<int>> { ["slot"] = SlotCurve },
            RandomStats = new Dictionary<int, RandomStatRow> { [500014] = new() { ItemId = 500014, MixSlot = new RandomStatRange(1, 4, "slot") } },
        });
        var rng = new Random(7);
        var rolls = Enumerable.Range(0, 300).Select(_ => rules.MixSlots(500014, 0, rng)).ToList();

        Assert.All(rolls, slots => Assert.InRange(slots, 1, 4));
        Assert.Contains(1, rolls);
        Assert.Equal(2, rules.MixSlots(1, 2, rng));
    }
}

public sealed class ItemStatSumTests
{
    private static readonly int[] VestBonuses = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 21, 0 };

    [Fact]
    public void TheSumAddsTheBaseStatAndTheMixedAbility()
    {
        var rules = new ItemPropertyRules(new ItemPropertyTables());
        var vest = new ItemParamEntry { Id = 5012, Class = ItemParamEntry.RareClass, StatBonuses = VestBonuses };
        var mixed = new[] { new ItemProperty(0, 10010, 20, 10009) };

        Assert.Equal(21, rules.StatSum(vest, System.Array.Empty<ItemProperty>(), 10010));
        Assert.Equal(41, rules.StatSum(vest, mixed, 10010));
        Assert.Equal(ItemStatCheckEntry.NoRequirement, rules.StatSum(vest, mixed, 10000));
    }

    [Fact]
    public void ARepairNeedsABrokenItemAndThePowderOfItsKind()
    {
        var neo = new ItemParamEntry { Id = 40153, Class = 2, RefineIndex = RepairRules.NeoRefineIndex, MinLevel = 50 };
        var highNeo = neo with { Id = 40999, MinLevel = 215 };
        var sword = new ItemParamEntry { Id = 40001, Class = 2, RefineIndex = 11, MinLevel = 50 };
        var powder = new ItemParamEntry { Id = 19629, Class = 1, SubType = RepairRules.MaterialSubType };
        var phantom = new ItemParamEntry { Id = RepairRules.PhantomPowderId, Class = 1, SubType = RepairRules.MaterialSubType };
        var broken = new Item { ItemUid = 1, ItemIndex = 40001, ItemClass = 2, CharacterId = 1, SlotKind = SlotKind.Inventory, SlotPosition = 0, RefineLevel = 4, RefineState = RefineRules.Broken };

        Assert.Equal(RepairRules.NoError, RepairRules.Check(sword, broken, false));
        Assert.Equal(RepairRules.ImpossibleItem, RepairRules.Check(sword, broken with { RefineState = 0 }, false));
        Assert.Equal(RepairRules.ImpossibleItem, RepairRules.Check(neo, broken, false));
        Assert.Equal(RepairRules.NoError, RepairRules.Check(neo, broken, true));
        Assert.Equal(RepairRules.ImpossibleItem, RepairRules.Check(highNeo, broken, true));
        Assert.Equal(4, RepairRules.MaterialCount(sword, broken));
        Assert.Equal(1, RepairRules.MaterialCount(sword, broken with { RefineLevel = 0 }));
        Assert.Equal(1, RepairRules.MaterialCount(neo, broken));
        Assert.True(RepairRules.IsMaterialFor(sword, powder));
        Assert.False(RepairRules.IsMaterialFor(sword, phantom));
        Assert.True(RepairRules.IsMaterialFor(neo, phantom));
        Assert.False(RepairRules.IsMaterialFor(neo, powder));
    }
}
