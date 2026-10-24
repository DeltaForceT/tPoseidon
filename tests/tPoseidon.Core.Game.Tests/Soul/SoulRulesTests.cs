using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Soul;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Soul;

public sealed class SoulRulesTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly SoulSystemInfoEntry Info = new(114, 100, 15, 1, 10, 60, 2);
    private static readonly SoulPotionCountEntry[] Counts =
    [
        new(1, 40, 1), new(41, 80, 2), new(81, 120, 3), new(121, 160, 4), new(161, 200, 5),
    ];
    private static readonly SoulSourceEntry[] Sources =
    [
        new(66324, 15000, 78), new(66297, 15000, 55), new(66325, 15000, 27), new(66324, 15001, 78),
    ];

    private static GuardianSoulRow Registered(int gauge, int count) => new(7, gauge, 0x55, count, 0, Start);

    [Fact]
    public void ATickTakesOneSourceAndRaisesTheGauge()
    {
        var tick = SoulRules.Tick(Registered(10, 3), Info, 55, 10, 1);
        Assert.Equal(1, tick.SourcesConsumed);
        Assert.Equal(65, tick.State.Gauge);
        Assert.Equal(2, tick.State.SourceCount);
        Assert.Equal(Start.AddMinutes(15), tick.State.LastTick);
        Assert.Equal(0, tick.PotionsMade);
    }

    [Fact]
    public void AFullGaugeMakesPotionsAndKeepsTheOverflow()
    {
        var tick = SoulRules.Tick(Registered(78, 2), Info, 55, 10, 5);
        Assert.Equal(33, tick.State.Gauge);
        Assert.Equal(5, tick.PotionsMade);
        Assert.Equal(5, tick.State.PotionsReady);
    }

    [Fact]
    public void WithoutASourceTheGaugeFallsAndTheLastSourceClearsTheRegistration()
    {
        var empty = SoulRules.Tick(new GuardianSoulRow(7, 5, 0, 0, 0, Start), Info, null, 0, 1);
        Assert.Equal(0, empty.State.Gauge);
        Assert.Equal(0, empty.SourcesConsumed);
        var last = SoulRules.Tick(Registered(0, 1), Info, 27, 1, 1);
        Assert.Equal((0UL, 0, 27), (last.State.SourceItemUid, last.State.SourceCount, last.State.Gauge));
        var gone = SoulRules.Tick(Registered(40, 4), Info, 27, 0, 1);
        Assert.Equal((0UL, 30), (gone.State.SourceItemUid, gone.State.Gauge));
    }

    [Fact]
    public void TickIsDueEveryGaugeTime()
    {
        Assert.False(SoulRules.TickDue(Registered(0, 1), Info, Start.AddMinutes(14)));
        Assert.True(SoulRules.TickDue(Registered(0, 1), Info, Start.AddMinutes(15)));
    }

    [Fact]
    public void PotionsPerGaugeFollowTheGuardianLevelOnlineAndOneOffline()
    {
        Assert.Equal(1, SoulRules.PotionsPerGauge(Counts, 40, true));
        Assert.Equal(2, SoulRules.PotionsPerGauge(Counts, 41, true));
        Assert.Equal(5, SoulRules.PotionsPerGauge(Counts, 200, true));
        Assert.Equal(5, SoulRules.PotionsPerGauge(Counts, 350, true));
        Assert.Equal(1, SoulRules.PotionsPerGauge(Counts, 200, false));
    }

    [Fact]
    public void SourcesBelongToTheGuardianType()
    {
        Assert.Equal(78, SoulRules.GaugeUpOf(Sources, 15000, 66324));
        Assert.Null(SoulRules.GaugeUpOf(Sources, 15001, 66297));
    }

    [Fact]
    public void RegisterChecksComeInOrder()
    {
        Assert.Equal(SoulRules.NotifyBadGuardian, SoulRules.CheckRegister(false, false, false, 0, 0, true));
        Assert.Equal(SoulRules.NotifyBadItem, SoulRules.CheckRegister(true, false, true, 1, 5, false));
        Assert.Equal(SoulRules.NotifyBadItem, SoulRules.CheckRegister(true, true, false, 1, 5, false));
        Assert.Equal(SoulRules.NotifyNotEnoughSource, SoulRules.CheckRegister(true, true, true, 0, 5, false));
        Assert.Equal(SoulRules.NotifyNotEnoughSource, SoulRules.CheckRegister(true, true, true, 6, 5, false));
        Assert.Equal(SoulRules.NotifyBadItem, SoulRules.CheckRegister(true, true, true, 5, 5, true));
        Assert.Equal(SoulRules.Ok, SoulRules.CheckRegister(true, true, true, 5, 5, false));
        Assert.Equal(SoulRules.NotifyBadPotionCount, SoulRules.CheckTakePotions(0, 3));
        Assert.Equal(SoulRules.NotifyBadPotionCount, SoulRules.CheckTakePotions(4, 3));
        Assert.Equal(SoulRules.Ok, SoulRules.CheckTakePotions(3, 3));
    }

    [Fact]
    public void ChargeStopsAtTheLevelCap()
    {
        Assert.Equal(300, SoulRules.ChargeCap(0));
        Assert.Equal(1300, SoulRules.ChargeCap(10));
        Assert.Equal(new SoulCharge(200, 300), SoulRules.Charge(0, 100, 500));
        Assert.Equal(new SoulCharge(50, 150), SoulRules.Charge(0, 100, 50));
        Assert.Equal(new SoulCharge(0, 300), SoulRules.Charge(0, 300, 10));
        Assert.Equal(SoulRules.ChargeState, SoulRules.CheckCharge(false, true, 1, 10));
        Assert.Equal(SoulRules.ChargeImpossibleItem, SoulRules.CheckCharge(true, false, 1, 10));
        Assert.Equal(SoulRules.ChargeSoulShort, SoulRules.CheckCharge(true, true, 0, 10));
        Assert.Equal(SoulRules.ChargeSoulShort, SoulRules.CheckCharge(true, true, 11, 10));
        Assert.Equal(SoulRules.Ok, SoulRules.CheckCharge(true, true, 10, 10));
    }

    [Fact]
    public void MaterialsAreMatchedByItemIdAcrossTheOfferedStacks()
    {
        SoulMaterial[] required = [new(64816, 50), new(50026, 1)];
        var offered = new[] { Stack(1, 64816, 30), Stack(2, 64816, 30), Stack(3, 50026, 1) };
        Assert.True(SoulRules.HasMaterials(required, offered));
        Assert.False(SoulRules.HasMaterials(required, offered[..2]));
        var takes = SoulRules.Consumption(required, offered);
        Assert.Equal([(1UL, 30), (2UL, 20), (3UL, 1)], takes.Select(take => (take.Item.ItemUid, take.Count)));
    }

    [Fact]
    public void CraftAndReinforceChecksComeInOrder()
    {
        var recipe = new SoulCraftEntry(1, 50029, 1, [new SoulMaterial(50026, 1)], 1f, 155000);
        Assert.Equal(SoulRules.WindowState, SoulRules.CheckCraft(false, recipe, true, 155000));
        Assert.Equal(SoulRules.ImpossibleItem, SoulRules.CheckCraft(true, null, true, 155000));
        Assert.Equal(SoulRules.MaterialMissing, SoulRules.CheckCraft(true, recipe, false, 155000));
        Assert.Equal(SoulRules.SoulShort, SoulRules.CheckCraft(true, recipe, true, 154999));
        Assert.Equal(SoulRules.Unknown, SoulRules.CheckCraft(true, recipe with { Probability = 0.5f }, true, 155000));
        Assert.Equal(SoulRules.Ok, SoulRules.CheckCraft(true, recipe, true, 155000));

        var row = new SoulIntensifyEntry(1, 1, 320, 22200, [new SoulMaterial(64834, 7)], 2);
        Assert.Same(row, SoulRules.IntensifyRow([row, row with { WeaponLevel = 380 }], 1, 320));
        Assert.Null(SoulRules.IntensifyRow([row], 2, 320));
        Assert.Equal(SoulRules.ImpossibleItem, SoulRules.CheckIntensify(true, false, 0, row, true, 22200));
        Assert.Equal(SoulRules.ImpossibleItem, SoulRules.CheckIntensify(true, true, 10, row, true, 22200));
        Assert.Equal(SoulRules.MaterialMissing, SoulRules.CheckIntensify(true, true, 0, row, false, 22200));
        Assert.Equal(SoulRules.SoulShort, SoulRules.CheckIntensify(true, true, 0, row, true, 22199));
        Assert.Equal(SoulRules.Ok, SoulRules.CheckIntensify(true, true, 0, row, true, 22200));
    }

    [Fact]
    public void PropertiesAreReadAndReplacedById()
    {
        var stored = new[] { new ItemProperty(10, 10000, 50, 0), new ItemProperty(0x11, 28000, 120, 0) };
        Assert.Equal(120, SoulRules.PropertyValue(stored, 0x11, 28000));
        Assert.Equal(0, SoulRules.PropertyValue(stored, 0x10, 28001));
        var replaced = SoulRules.WithProperty(stored, 0x11, 28000, 110);
        Assert.Equal([new ItemProperty(10, 10000, 50, 0), new ItemProperty(0x11, 28000, 110, 0)], replaced);
        Assert.Equal(0, SoulRules.Drain(5, 10));
        Assert.Equal(110, SoulRules.Drain(120, 10));
    }

    private static Item Stack(ulong uid, int itemIndex, ushort stack) =>
        new() { ItemUid = uid, ItemIndex = itemIndex, ItemClass = 1, Stack = stack, CharacterId = 1, SlotKind = SlotKind.Inventory, SlotPosition = 0 };
}
