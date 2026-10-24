using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Monsters;

public sealed class MonsterDropsTests
{
    private const int QuestItem = 11908;
    private const int Potion = 3030;
    private const int Sword = 50000;

    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static MonItemDropInfoEntry Row(int id, DropItem[] items, params DropTableRef[] tables) => new()
    {
        Id = id,
        Items = items,
        SubTables = tables,
    };

    private static MonItemTableDropEntry Table(int id, params DropItem[] items)
    {
        var slots = new DropItem[MonItemTableDropEntry.SlotCount];
        for (var i = 0; i < slots.Length; i++) slots[i] = i < items.Length ? items[i] : new DropItem(0, 0f, 1);
        return new MonItemTableDropEntry { Id = id, Slots = slots };
    }

    private static MonsterDropRoller Roller(MonItemDropInfoEntry row, params MonItemTableDropEntry[] tables)
        => new(new[] { row }, tables);

    private static bool Never(int itemId) => false;

    [Theory]
    [InlineData(0, true)]
    [InlineData(2499, true)]
    [InlineData(2500, false)]
    [InlineData(9999, false)]
    public void ARateIsTheChancePerRollOutOfTenThousand(int draw, bool drops)
    {
        var roller = Roller(Row(1, new[] { new DropItem(QuestItem, 0.25f, 1) }));

        var rolled = roller.Roll(1, 1, Never, new ScriptedRandom(draw));

        Assert.Equal(drops ? new[] { new RolledDrop(QuestItem, 1) } : Array.Empty<RolledDrop>(), rolled);
    }

    [Theory]
    [InlineData(2999, QuestItem)]
    [InlineData(3000, Potion)]
    [InlineData(7999, Potion)]
    public void ARollPicksTheFirstItemWhoseShareHoldsTheDraw(int draw, int itemId)
    {
        var roller = Roller(Row(1, new[] { new DropItem(QuestItem, 0.3f, 1), new DropItem(Potion, 0.5f, 3) }));

        var rolled = roller.Roll(1, 1, Never, new ScriptedRandom(draw));

        Assert.Equal(itemId, Assert.Single(rolled).ItemId);
    }

    [Fact]
    public void EachRollGivesAtMostOneItemAndCarriesItsCount()
    {
        var roller = Roller(Row(1, new[] { new DropItem(QuestItem, 0.3f, 1), new DropItem(Potion, 0.5f, 3) }));

        var rolled = roller.Roll(1, 3, Never, new ScriptedRandom(0, 5000, 9000));

        Assert.Equal(new[] { new RolledDrop(QuestItem, 1), new RolledDrop(Potion, 3) }, rolled);
    }

    [Fact]
    public void RatesThatAddUpToMoreThanOneAreDividedByTheirSum()
    {
        var roller = Roller(Row(1, new[] { new DropItem(QuestItem, 1f, 1), new DropItem(Potion, 1f, 1) }));

        Assert.Equal(QuestItem, Assert.Single(roller.Roll(1, 1, Never, new ScriptedRandom(4999))).ItemId);
        Assert.Equal(Potion, Assert.Single(roller.Roll(1, 1, Never, new ScriptedRandom(5000))).ItemId);
    }

    [Fact]
    public void AnItemThatDropsOneOnlyIgnoresTheCountColumn()
    {
        var roller = Roller(Row(1, new[] { new DropItem(Sword, 1f, 5) }));

        var rolled = roller.Roll(1, 1, itemId => itemId == Sword, new ScriptedRandom(0));

        Assert.Equal(new[] { new RolledDrop(Sword, 1) }, rolled);
    }

    [Fact]
    public void ARollThatHitsNoItemTriesTheTablesAndATableFillsTheKill()
    {
        var roller = Roller(
            Row(1, new[] { new DropItem(QuestItem, 0.1f, 1) }, new DropTableRef(7, 0.5f)),
            Table(7, new DropItem(Potion, 0.5f, 2), new DropItem(Sword, 0.25f, 1)));

        var rolled = roller.Roll(1, 2, Never, new ScriptedRandom(1000, 0, 5000, 9999));

        Assert.Equal(new[] { new RolledDrop(Potion, 2), new RolledDrop(Sword, 1) }, rolled);
    }

    [Fact]
    public void AnUnknownRowDropsNothing()
    {
        var roller = Roller(Row(1, new[] { new DropItem(QuestItem, 1f, 1) }));

        Assert.Empty(roller.Roll(2, 3, Never, new ScriptedRandom()));
    }

    [Fact]
    public void MoneyDropsWithItsOwnChanceAndAnAmountBetweenTheTwoColumns()
    {
        Assert.Equal(30, MonsterDropRoller.MoneyAmount(0.1, 0, 51, new ScriptedRandom(999, 30)));
        Assert.Null(MonsterDropRoller.MoneyAmount(0.1, 0, 51, new ScriptedRandom(1000)));
        Assert.Null(MonsterDropRoller.MoneyAmount(1.0, 0, 0, new ScriptedRandom(0)));
        Assert.Equal(15, MonsterDropRoller.MoneyAmount(1.0, 20, 10, new ScriptedRandom(0, 5)));
        Assert.Equal(40, MonsterDropRoller.MoneyAmount(1.0, 40, 40, new ScriptedRandom(0)));
    }

    [Theory]
    [InlineData(30, 200)]
    [InlineData(50, 200)]
    [InlineData(51, 201)]
    [InlineData(500, 201)]
    [InlineData(501, 202)]
    [InlineData(2000, 202)]
    [InlineData(2001, 203)]
    [InlineData(9999, 203)]
    public void MoneyBecomesOneItemChosenByTheAmount(int amount, int itemId)
    {
        Assert.Equal(new[] { new RolledDrop(itemId, amount) }, MonsterDropRoller.SplitMoney(amount));
    }

    [Fact]
    public void MoneyAboveTenThousandAddsFullStacksAndAnExactMultipleDropsNothing()
    {
        Assert.Equal(
            new[] { new RolledDrop(203, 10000), new RolledDrop(203, 10000), new RolledDrop(200, 30) },
            MonsterDropRoller.SplitMoney(20030));
        Assert.Empty(MonsterDropRoller.SplitMoney(10000));
        Assert.Empty(MonsterDropRoller.SplitMoney(0));
    }

    private static ZoneGeometry Field(params (int X, int Y)[] blocked)
    {
        var cells = new byte[40 * 40];
        foreach (var (x, y) in blocked) cells[y * 40 + x] = ZoneGeometry.CellBlocked;
        return new ZoneGeometry
        {
            MinX = 0,
            MinY = 0,
            MaxX = 639,
            MaxY = 639,
            CellsX = 40,
            CellsY = 40,
            CellAttributes = cells,
            Regions = Array.Empty<ZoneRegion>(),
        };
    }

    [Theory]
    [InlineData(0, 136, 136)]
    [InlineData(1, 136, 152)]
    [InlineData(4, 136, 200)]
    public void AnItemLandsOnTheSpiralTwoToThreeCellsFromTheCorpse(int skipped, int x, int y)
    {
        var placed = DropPlacement.Place(
            Field(), 168, 168, DropPlacement.ItemFirstRing, DropPlacement.ItemReach,
            new HashSet<(int X, int Y)>(), new ScriptedRandom(0, skipped));

        Assert.Equal(((ushort)x, (ushort)y), placed);
    }

    [Fact]
    public void BlockedCellsAndCellsTakenInTheSameBatchArePassedOver()
    {
        var taken = new HashSet<(int X, int Y)>();

        var first = DropPlacement.Place(
            Field((8, 8)), 168, 168, DropPlacement.ItemFirstRing, DropPlacement.ItemReach, taken, new ScriptedRandom(0, 0));
        var second = DropPlacement.Place(
            Field((8, 8)), 168, 168, DropPlacement.ItemFirstRing, DropPlacement.ItemReach, taken, new ScriptedRandom(0, 0));

        Assert.Equal(((ushort)136, (ushort)152), first);
        Assert.Equal(((ushort)136, (ushort)168), second);
    }

    [Fact]
    public void WithoutGeometryTheItemStaysOnTheCorpse()
    {
        var placed = DropPlacement.Place(
            null, 168, 168, DropPlacement.ItemFirstRing, DropPlacement.ItemReach,
            new HashSet<(int X, int Y)>(), new ScriptedRandom());

        Assert.Equal(((ushort)168, (ushort)168), placed);
    }

    private static FieldItem Item(ushort owner, bool money = false) =>
        new(0x0401, 153, 136, 136, money ? 200 : QuestItem, 1, 1, money, 0x0201, owner, Start);

    [Fact]
    public void AnItemBelongsToItsOwnerForThirtySeconds()
    {
        var item = Item(owner: 0x0301);

        Assert.Equal((ushort)0x0301, GroundItemField.OwnerAt(item, Start.AddMilliseconds(30000)));
        Assert.Equal(FieldItem.NoOwner, GroundItemField.OwnerAt(item, Start.AddMilliseconds(30001)));
    }

    [Theory]
    [InlineData(0x0301, false, 120000)]
    [InlineData(0x0301, true, 90000)]
    [InlineData(0, false, 90000)]
    [InlineData(0, true, 60000)]
    public void AnItemIsRemovedAfterItsOwnedAndFreeTimes(int owner, bool money, int lifetimeMs)
    {
        var field = new GroundItemField();
        field.Add(Item((ushort)owner, money));

        Assert.Empty(field.TakeExpired(Start.AddMilliseconds(lifetimeMs)));
        Assert.Single(field.TakeExpired(Start.AddMilliseconds(lifetimeMs + 1)));
        Assert.Null(field.Find(0x0401));
    }

    private sealed class ScriptedRandom : Random
    {
        private readonly Queue<int> _draws;

        public ScriptedRandom(params int[] draws)
        {
            _draws = new Queue<int>(draws);
        }

        public override int Next(int maxValue) => _draws.Dequeue();
    }
}
