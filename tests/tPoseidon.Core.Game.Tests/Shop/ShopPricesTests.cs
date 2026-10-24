using tPoseidon.Core.Game.Shop;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Shop;

public sealed class ShopPricesTests
{
    private static ShopGoodsEntry Goods(float purchaseRate, float sellingRate) =>
        new() { Table = "ShopItem_Test", ItemId = 2000, PurchaseRate = purchaseRate, SellingRate = sellingRate };

    [Fact]
    public void TheGoodsPurchaseRateWinsOverTheShopRate()
    {
        Assert.Equal(50, ShopPrices.BuyUnitPrice(1.0f, 100, Goods(0.5f, 0f), 1.0f));
    }

    [Fact]
    public void WithoutAGoodsPurchaseRateTheShopRateIsUsed()
    {
        Assert.Equal(200, ShopPrices.BuyUnitPrice(1.0f, 100, Goods(0f, 0f), 2.0f));
        Assert.Equal(200, ShopPrices.BuyUnitPrice(1.0f, 100, null, 2.0f));
    }

    [Fact]
    public void TheBuyPriceIsRoundedHalfUp()
    {
        Assert.Equal(2, ShopPrices.BuyUnitPrice(1.0f, 3, Goods(0.5f, 0f), 1.0f));
        Assert.Equal(1, ShopPrices.BuyUnitPrice(1.0f, 5, Goods(0.25f, 0f), 1.0f));
    }

    [Fact]
    public void TheMapPriceRateScalesBothPrices()
    {
        Assert.Equal(150, ShopPrices.BuyUnitPrice(1.5f, 100, null, 1.0f));
        Assert.Equal(75, ShopPrices.SellUnitPrice(1.5f, 100, null, 0.5f));
    }

    [Fact]
    public void AMapWithoutAPriceRateUsesOne()
    {
        Assert.Equal(100, ShopPrices.BuyUnitPrice(0f, 100, null, 1.0f));
        Assert.Equal(50, ShopPrices.SellUnitPrice(0f, 100, null, 0.5f));
    }

    [Fact]
    public void TheSellPriceIsTruncated()
    {
        Assert.Equal(1, ShopPrices.SellUnitPrice(1.0f, 3, null, 0.5f));
    }

    [Fact]
    public void TheGoodsSellingRateWinsOverTheShopRate()
    {
        Assert.Equal(25, ShopPrices.SellUnitPrice(1.0f, 100, Goods(1.0f, 0.25f), 0.5f));
    }

    [Fact]
    public void AShopWithoutASellingRateBuysAtHalf()
    {
        Assert.Equal(50, ShopPrices.SellUnitPrice(1.0f, 100, null, 0f));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    public void ASaleIsWorthAtLeastOne(int value)
    {
        Assert.Equal(1, ShopPrices.SellUnitPrice(1.0f, value, null, 0.5f));
    }

    [Fact]
    public void SkillCardPricesAreTruncatedWithoutTheHalf()
    {
        Assert.Equal(300, ShopPrices.SkillCardBuyUnitPrice(0f, 300, null, 1.0f));
        Assert.Equal(1, ShopPrices.SkillCardBuyUnitPrice(1.0f, 3, Goods(0.5f, 0f), 1.0f));
        Assert.Equal(30, ShopPrices.SellUnitPrice(0f, 300, null, 0.1f));
    }
}
