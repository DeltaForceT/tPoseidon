using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Shop;

public static class ShopPrices
{
    public const int MaxLines = 20;
    public const int BuyLineSize = 6;
    public const int BuyHeaderSize = 4;
    public const long MoneyLimit = 2_100_000_000;
    public const int GoodsLimit = 128;
    public const float DefaultRate = 1.0f;
    public const float DefaultShopSellingRate = 0.5f;
    public const int DrillType = 6;
    public const int NotSellableOption = 0x200;

    public static float PriceRateOf(float shopPriceRate) => shopPriceRate <= 0f ? DefaultRate : shopPriceRate;

    public static float PurchaseRateOf(float shopPurchaseRate) => shopPurchaseRate <= 0f ? DefaultRate : shopPurchaseRate;

    public static float SellingRateOf(float shopSellingRate) => shopSellingRate <= 0f ? DefaultShopSellingRate : shopSellingRate;

    public static int BuyUnitPrice(float shopPriceRate, int value, ShopGoodsEntry? goods, float shopPurchaseRate)
    {
        var rate = goods is { PurchaseRate: > 0f } ? goods.PurchaseRate : PurchaseRateOf(shopPurchaseRate);
        return (int)(PriceRateOf(shopPriceRate) * (double)value * rate + 0.5);
    }

    public static int SkillCardBuyUnitPrice(float shopPriceRate, int value, ShopGoodsEntry? goods, float shopPurchaseRate)
    {
        var rate = goods is { PurchaseRate: > 0f } ? goods.PurchaseRate : PurchaseRateOf(shopPurchaseRate);
        return (int)(PriceRateOf(shopPriceRate) * (double)value * rate);
    }

    public static int SellUnitPrice(float shopPriceRate, int value, ShopGoodsEntry? goods, float shopSellingRate)
    {
        var rate = goods is { SellingRate: > 0f } ? goods.SellingRate : SellingRateOf(shopSellingRate);
        var unit = (int)(PriceRateOf(shopPriceRate) * (double)value * rate);
        return unit <= 0 ? 1 : unit;
    }
}
