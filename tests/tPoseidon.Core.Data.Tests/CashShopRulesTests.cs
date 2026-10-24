namespace tPoseidon.Core.Data.Tests;

public class CashShopRulesTests
{
    private static readonly ShopGoodsOption ThirtyDays = new(LimitCode: 3, Price: 500, LimitFlag: 1, UseHours: 720, DefaultDisplay: true);

    private static readonly ShopGoods Goods = new(
        GoodsCode: 10,
        Name: "Test goods",
        Description: "",
        Category: 0,
        SetCount: 1,
        OnSale: true,
        Items: new[] { new ShopGoodsItem(2, 600001, 1) },
        Options: new[] { ThirtyDays });

    private static CashShopPolicy Policy(CashMode mode) => new(mode, WorldCode: 1, StartingBalance: 0);

    [Fact]
    public void BalanceModeChargesTheListedPrice()
    {
        Assert.Equal(CashShopResult.Ok, CashShopRules.Check(Policy(CashMode.Balance), 1, Goods, ThirtyDays, 500, 500, true));
        Assert.Equal(CashShopResult.NotEnoughCash, CashShopRules.Check(Policy(CashMode.Balance), 1, Goods, ThirtyDays, 500, 499, true));
    }

    [Fact]
    public void FreeModeChargesNothing()
    {
        Assert.Equal(0, Policy(CashMode.Free).PriceOf(ThirtyDays));
        Assert.Equal(CashShopResult.Ok, CashShopRules.Check(Policy(CashMode.Free), 1, Goods, ThirtyDays, 0, 0, true));
        Assert.Equal(CashShopResult.PriceMismatch, CashShopRules.Check(Policy(CashMode.Free), 1, Goods, ThirtyDays, 500, 0, true));
    }

    [Fact]
    public void DisabledModeRefusesEverything()
    {
        Assert.Equal(CashShopResult.ShopClosed, CashShopRules.Check(Policy(CashMode.Disabled), 1, Goods, ThirtyDays, 0, 0, true));
    }

    [Fact]
    public void ChecksRunInTheOriginalOrder()
    {
        var balance = Policy(CashMode.Balance);
        Assert.Equal(CashShopResult.WrongWorld, CashShopRules.Check(balance, 2, null, null, 1, 0, false));
        Assert.Equal(CashShopResult.UnknownGoods, CashShopRules.Check(balance, 1, null, null, 1, 0, false));
        Assert.Equal(CashShopResult.UnknownGoods, CashShopRules.Check(balance, 1, Goods with { OnSale = false }, ThirtyDays, 500, 1000, true));
        Assert.Equal(CashShopResult.PriceMismatch, CashShopRules.Check(balance, 1, Goods, ThirtyDays, 400, 0, false));
        Assert.Equal(CashShopResult.NotEnoughCash, CashShopRules.Check(balance, 1, Goods, ThirtyDays, 500, 0, false));
        Assert.Equal(CashShopResult.UnknownAccount, CashShopRules.Check(balance, 1, Goods, ThirtyDays, 500, 500, false));
    }
}
