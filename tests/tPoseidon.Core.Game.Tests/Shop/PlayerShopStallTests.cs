using tPoseidon.Core.Game.PlayerShop;

namespace tPoseidon.Core.Game.Tests.Shop;

public sealed class PlayerShopStallTests
{
    private static readonly PlayerShopSlot Potions = new(0x55, 101, 300, 10);

    private static PlayerShopPurchase Buy(ushort count, uint price = 300, uint itemId = 101) => new(7, 0x55, itemId, count, price);

    [Fact]
    public void ZonesPricesAndSlotCountsFollowTheServerChecks()
    {
        Assert.True(PlayerShopRules.OpensInZone(1));
        Assert.True(PlayerShopRules.OpensInZone(5));
        Assert.False(PlayerShopRules.OpensInZone(3));
        Assert.True(PlayerShopRules.PriceAllowed(1_000_000_000));
        Assert.False(PlayerShopRules.PriceAllowed(1_000_000_001));
        Assert.False(PlayerShopRules.SlotCountAllowed(0));
        Assert.True(PlayerShopRules.SlotCountAllowed(12));
        Assert.False(PlayerShopRules.SlotCountAllowed(13));
    }

    [Fact]
    public void MoneyMustSettleOnBothSides()
    {
        Assert.True(PlayerShopRules.MoneySettles(3000, 0, 300, 10));
        Assert.False(PlayerShopRules.MoneySettles(2999, 0, 300, 10));
        Assert.False(PlayerShopRules.MoneySettles(3000, 0x7FFFFFFF - 2999, 300, 10));
    }

    [Fact]
    public void ReserveChecksTheSlotThenLocksUntilReleased()
    {
        var stall = new PlayerShopStall(7);
        stall.Register(1, "Potions", new[] { Potions });
        Assert.Equal(PlayerShopBuyRefusal.InvalidItem, stall.Reserve(Buy(11)));
        Assert.Equal(PlayerShopBuyRefusal.InvalidItem, stall.Reserve(Buy(1, price: 299)));
        Assert.Equal(PlayerShopBuyRefusal.InvalidItem, stall.Reserve(Buy(1, itemId: 102)));
        Assert.Null(stall.Reserve(Buy(4)));
        Assert.Equal(PlayerShopBuyRefusal.ItemInBusiness, stall.Reserve(Buy(1)));
        stall.Release(Buy(4), sold: true);
        Assert.Equal(6, stall.Slots.Single().Count);
        Assert.Null(stall.Reserve(Buy(6)));
        stall.Release(Buy(6), sold: true);
        Assert.Empty(stall.Slots);
    }

    [Fact]
    public void VisitorsAreCappedAtEight()
    {
        var stall = new PlayerShopStall(7);
        for (ushort visitor = 1; visitor <= 8; visitor++) Assert.True(stall.AddVisitor(visitor));
        Assert.False(stall.AddVisitor(9));
        stall.PruneVisitors(visitor => visitor != 3);
        Assert.Equal(7, stall.VisitorCount);
        Assert.True(stall.AddVisitor(9));
    }

    [Fact]
    public void VisitsCountEveryVisitorTakenInAndRestartZeroesThem()
    {
        var stall = new PlayerShopStall(7);
        Assert.Equal(0, stall.VisitsSinceStart);
        Assert.True(stall.AddVisitor(1));
        Assert.True(stall.AddVisitor(2));
        stall.RemoveVisitor(1);
        Assert.True(stall.AddVisitor(3));
        Assert.Equal(3, stall.VisitsSinceStart);
        Assert.Equal(2, stall.VisitorCount);
        stall.RestartCounters();
        Assert.Equal(0, stall.VisitsSinceStart);
        Assert.Equal(2, stall.VisitorCount);
    }

    [Fact]
    public void AFullShopTakesNoVisitAndTheSameVisitorIsCountedOnce()
    {
        var stall = new PlayerShopStall(7);
        for (ushort visitor = 1; visitor <= 8; visitor++) Assert.True(stall.AddVisitor(visitor));
        Assert.False(stall.AddVisitor(9));
        Assert.Equal(8, stall.VisitsSinceStart);
        stall.RemoveVisitor(8);
        Assert.True(stall.AddVisitor(7));
        Assert.Equal(8, stall.VisitsSinceStart);
    }
}
