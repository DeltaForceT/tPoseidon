namespace tPoseidon.Core.Data;

public enum CashMode
{
    Free,
    Balance,
    Disabled,
}

public enum CashShopResult
{
    Ok = 0,
    ShopClosed = 1,
    WrongWorld = 101,
    UnknownGoods = 102,
    PriceMismatch = 103,
    NotEnoughCash = 104,
    UnknownAccount = 105,
    UnknownRecipient = 106,
}

public enum CashSellStore : byte
{
    Web = 0,
    Game = 1,
}

public enum CashSellType : byte
{
    Self = 0,
    Gift = 1,
}

public sealed record CashShopPolicy(CashMode Mode, int WorldCode, long StartingBalance)
{
    public int PriceOf(ShopGoodsOption option)
    {
        ArgumentNullException.ThrowIfNull(option);
        return Mode == CashMode.Free ? 0 : option.Price;
    }
}

public sealed record ShopGoodsItem(byte ItemClass, int ItemIndex, short ItemCount);

public sealed record ShopGoodsOption(short LimitCode, int Price, byte LimitFlag, short UseHours, bool DefaultDisplay);

public sealed record ShopGoods(
    int GoodsCode,
    string Name,
    string Description,
    byte Category,
    short SetCount,
    bool OnSale,
    IReadOnlyList<ShopGoodsItem> Items,
    IReadOnlyList<ShopGoodsOption> Options);

public sealed record ShopFriend(ulong CharacterId, string Name, uint AccountId);

public sealed record CashPurchase(uint AccountId, int World, int GoodsCode, short LimitCode, int SettlePrice, CashSellStore Store);

public sealed record CashGift(CashPurchase Purchase, ulong FromCharacterId, ulong ToCharacterId, string Message);

public sealed record CashShopOutcome(CashShopResult Result, int SellCode, long Balance);

public interface ICashShopStore
{
    ShopGoods? FindGoods(int goodsCode);

    short? ShopPeriodHours(int itemIndex);

    long Balance(uint accountId, long startingBalance);

    long Grant(uint accountId, long amount, long startingBalance, string reason);

    IReadOnlyList<ShopFriend> Friends(ulong characterId, uint ownerAccountId);

    CashShopOutcome Buy(CashPurchase purchase, CashShopPolicy policy);

    CashShopOutcome Gift(CashGift gift, CashShopPolicy policy);
}

public static class CashShopRules
{
    public const int MaxGiftMessageLength = 100;

    public static CashShopResult Check(
        CashShopPolicy policy,
        int world,
        ShopGoods? goods,
        ShopGoodsOption? option,
        int settlePrice,
        long balance,
        bool accountExists)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (policy.Mode == CashMode.Disabled) return CashShopResult.ShopClosed;
        if (world != 0 && world != policy.WorldCode) return CashShopResult.WrongWorld;
        if (goods is null || option is null || !goods.OnSale) return CashShopResult.UnknownGoods;
        var price = policy.PriceOf(option);
        if (settlePrice != price) return CashShopResult.PriceMismatch;
        if (policy.Mode == CashMode.Balance && price > balance) return CashShopResult.NotEnoughCash;
        if (!accountExists) return CashShopResult.UnknownAccount;
        return CashShopResult.Ok;
    }

    public static string Describe(CashShopResult result) => result switch
    {
        CashShopResult.Ok => "Done.",
        CashShopResult.ShopClosed => "The item shop is closed on this server.",
        CashShopResult.WrongWorld => "This item shop belongs to another world.",
        CashShopResult.UnknownGoods => "That item is not on sale.",
        CashShopResult.PriceMismatch => "The price has changed. Please open the item again.",
        CashShopResult.NotEnoughCash => "You don't have enough cash for this item.",
        CashShopResult.UnknownAccount => "Your account was not found.",
        CashShopResult.UnknownRecipient => "That character is not on your friend list.",
        _ => "The purchase failed.",
    };
}
