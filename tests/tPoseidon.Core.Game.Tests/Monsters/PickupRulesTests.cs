using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Monsters;

public sealed class PickupRulesTests
{
    private const int QuestItem = 11908;
    private const int Potion = 3030;
    private const int Sword = 50000;
    private const int ShadowMonster = 16500;
    private const int FieldMonster = 2285;

    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly InMemoryItemParamLookup Catalog = new(new[]
    {
        new ItemParamEntry { Id = QuestItem, Class = 1, Type = 14, Weight = 10 },
        new ItemParamEntry { Id = Potion, Class = 1, Type = 15, Weight = 5 },
        new ItemParamEntry { Id = Sword, Class = 3, Type = 1, Weight = 100 },
        new ItemParamEntry { Id = 200, Class = 1, Type = 30 },
        new ItemParamEntry { Id = 201, Class = 1, Type = 30 },
        new ItemParamEntry { Id = 202, Class = 1, Type = 30 },
        new ItemParamEntry { Id = 203, Class = 1, Type = 30 },
    });

    private static ZoneGeometry Field() => new()
    {
        MinX = 0,
        MinY = 0,
        MaxX = 639,
        MaxY = 639,
        CellsX = 40,
        CellsY = 40,
        CellAttributes = new byte[40 * 40],
        Regions = Array.Empty<ZoneRegion>(),
    };

    private static DropSource Source(int monsterTypeId = FieldMonster) =>
        new(0x0201, monsterTypeId, 153, 168, 168, 0x0301, Start);

    private static Func<ushort> Ids()
    {
        ushort next = 0x0400;
        return () => ++next;
    }

    private static FieldItem OnTheGround(ushort owner) =>
        new(0x0401, 153, 168, 168, QuestItem, 1, 1, false, 0x0201, owner, Start);

    [Fact]
    public void EveryItemOfAKillCarriesTheCountOfTheFirstOne()
    {
        var drops = new[] { new RolledDrop(Potion, 3), new RolledDrop(QuestItem, 1) };

        var batch = MonsterDropBatch.Items(Source(), drops, Catalog, Ids(), Field(), new Random(1));

        Assert.Equal(new[] { (Potion, 3), (QuestItem, 3) }, batch.Select(i => (i.ItemId, i.Count)).ToArray());
        Assert.Equal(new ushort[] { 0x0401, 0x0402 }, batch.Select(i => i.EntityId).ToArray());
        Assert.All(batch, item =>
        {
            Assert.Equal((ushort)0x0201, item.DropperEntityId);
            Assert.Equal((ushort)0x0301, item.OwnerEntityId);
            Assert.Equal(Start, item.DroppedAt);
            Assert.False(item.IsMoney);
            Assert.False(item.IsHidden);
        });
        Assert.Equal(2, batch.Select(i => (i.X, i.Y)).Distinct().Count());
    }

    [Fact]
    public void AnItemOfTheThirdTableClassIsARareItemOnTheGround()
    {
        var batch = MonsterDropBatch.Items(
            Source(), new[] { new RolledDrop(Sword, 1) }, Catalog, Ids(), Field(), new Random(1));

        Assert.Equal(ItemParamEntry.RareClass, Assert.Single(batch).ItemClass);
    }

    [Fact]
    public void ItemsOfAShadowMonsterAreHiddenAndItsMoneyIsNot()
    {
        var items = MonsterDropBatch.Items(
            Source(ShadowMonster), new[] { new RolledDrop(QuestItem, 1) }, Catalog, Ids(), Field(), new Random(1));
        var money = MonsterDropBatch.Money(Source(ShadowMonster), 30, Catalog, Ids(), Field(), new Random(1));

        Assert.True(Assert.Single(items).IsHidden);
        Assert.False(Assert.Single(money).IsHidden);
    }

    [Theory]
    [InlineData(15999, false)]
    [InlineData(16000, true)]
    [InlineData(16999, true)]
    [InlineData(17000, false)]
    public void ShadowMonstersAreTheSixteenThousands(int monsterTypeId, bool hidden)
    {
        Assert.Equal(hidden, MonsterDropBatch.DropsHidden(monsterTypeId));
    }

    [Fact]
    public void AnItemTheTableDoesNotKnowEndsTheBatch()
    {
        var drops = new[] { new RolledDrop(Potion, 1), new RolledDrop(999999, 1), new RolledDrop(QuestItem, 1) };

        var batch = MonsterDropBatch.Items(Source(), drops, Catalog, Ids(), Field(), new Random(1));

        Assert.Equal(Potion, Assert.Single(batch).ItemId);
    }

    [Fact]
    public void MoneyKeepsItsOwnCountsAndLandsThreeCellsOrMoreAway()
    {
        var batch = MonsterDropBatch.Money(Source(), 20030, Catalog, Ids(), Field(), new Random(1));

        Assert.Equal(
            new[] { (203, 10000), (203, 10000), (200, 30) },
            batch.Select(i => (i.ItemId, i.Count)).ToArray());
        Assert.All(batch, item =>
        {
            Assert.True(item.IsMoney);
            var cells = Math.Max(
                Math.Abs(ZoneGeometry.CellOf(item.X) - ZoneGeometry.CellOf(168)),
                Math.Abs(ZoneGeometry.CellOf(item.Y) - ZoneGeometry.CellOf(168)));
            Assert.InRange(cells, 3, 5);
        });
    }

    [Fact]
    public void MoneyThatSplitsToNothingDropsNothing()
    {
        Assert.Empty(MonsterDropBatch.Money(Source(), 10000, Catalog, Ids(), Field(), new Random(1)));
    }

    [Theory]
    [InlineData(232, 168, true)]
    [InlineData(248, 168, false)]
    [InlineData(216, 216, false)]
    [InlineData(200, 216, true)]
    public void AnItemIsWithinReachAtFourCells(int itemX, int itemY, bool within)
    {
        Assert.Equal(within, PickupRules.IsWithinReach(168, 168, itemX, itemY));
    }

    [Fact]
    public void OnlyTheOwnerMayTakeAnItemForThirtySeconds()
    {
        var item = OnTheGround(owner: 0x0301);

        Assert.True(PickupRules.MayTake(item, 0x0301, Start.AddSeconds(1)));
        Assert.False(PickupRules.MayTake(item, 0x0302, Start.AddSeconds(30)));
        Assert.True(PickupRules.MayTake(item, 0x0302, Start.AddMilliseconds(30001)));
        Assert.True(PickupRules.MayTake(OnTheGround(FieldItem.NoOwner), 0x0302, Start));
    }

    [Theory]
    [InlineData(100, 95, 10, 1, 0)]
    [InlineData(100, 100, 0, 5, 0)]
    [InlineData(100, 50, 10, 3, 3)]
    [InlineData(100, 50, 10, 9, 5)]
    [InlineData(100, 50, 0, 9, 9)]
    [InlineData(100, 50, -1, 9, 0)]
    public void TheCountABagAcceptsFollowsItsFreeWeight(int maxWeight, int bagWeight, int itemWeight, int count, int accepted)
    {
        Assert.Equal(accepted, PickupRules.AcceptableCount(maxWeight, bagWeight, itemWeight, count));
    }

    [Fact]
    public void AStackIsOnlyTakenWholeAndANewItemNeedsAFreeSlot()
    {
        Assert.True(PickupRules.Fits(100, 50, 10, joinsStack: false, itemWeight: 10, count: 5));
        Assert.False(PickupRules.Fits(100, 50, 10, joinsStack: false, itemWeight: 10, count: 6));
        Assert.False(PickupRules.Fits(100, 50, PickupRules.BagCapacity, joinsStack: false, itemWeight: 10, count: 1));
        Assert.True(PickupRules.Fits(100, 50, PickupRules.BagCapacity, joinsStack: true, itemWeight: 10, count: 1));
        Assert.True(PickupRules.Fits(100, 50, 10, joinsStack: false, itemWeight: 0, count: 500));
        Assert.False(PickupRules.Fits(100, 100, 10, joinsStack: false, itemWeight: 0, count: 1));
    }

    [Fact]
    public void MoneyStopsAtItsLimit()
    {
        const long limit = 2_100_000_000;
        Assert.True(PickupRules.MoneyFits(limit - 30, 30, limit));
        Assert.False(PickupRules.MoneyFits(limit - 29, 30, limit));
    }

    [Fact]
    public void TheWalkEndsOnACellNextToTheItem()
    {
        var item = OnTheGround(owner: 0x0301);

        var (x, y) = PickupRules.WalkEnd(Field(), item, new Random(1));

        Assert.NotEqual((item.X, item.Y), (x, y));
        Assert.True(PickupRules.IsWithinReach(x, y, item.X, item.Y));
        Assert.Equal((item.X, item.Y), PickupRules.WalkEnd(null, item, new Random(1)));
    }
}
