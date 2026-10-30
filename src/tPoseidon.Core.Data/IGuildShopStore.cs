namespace tPoseidon.Core.Data;

public enum GuildShopGoodsType
{
    MemberExpansion = 1,
    Depot = 2,
    DepotExpansion = 3,
    Emblem = 4,
    EmblemPackage = 5,
}

public static class GuildShopErrors
{
    public const int None = 0;
    public const int NotEnoughCoin = 1;
    public const int NotAuthority = 2;
    public const int PurchasePending = 3;
    public const int NotFoundGoods = 4;
    public const int MemberMaxOver = 5;
    public const int EmblemCountOver = 6;
    public const int EnabledDepot = 7;
    public const int DepotMaxOver = 8;
    public const int NotYetBuyDepot = 9;
    public const int EmblemPackageOwned = 102;
}

public readonly record struct GuildShopState(int Coins, int MaxMembers, int DepotTabs, byte EmblemPurchase = GuildEmblemPurchase.None);

public readonly record struct GuildShopDecision(int Error, GuildShopState After);

public static class GuildShopRules
{
    public const ushort ShopRight = 0x20;
    public const int ShopRightIndex = 5;
    public const int MemberStep = 16;
    public const int MostMembers = 128;
    public const int HistoryShown = 10;

    public static bool MayBuy(ushort rights) => (rights & ShopRight) != 0;

    public static GuildShopGoodsType? TypeOf(int goodsType) =>
        Enum.IsDefined(typeof(GuildShopGoodsType), goodsType) ? (GuildShopGoodsType)goodsType : null;

    public static GuildShopDecision Decide(GuildShopGoodsType type, int price, GuildShopState state)
    {
        var (error, effect) = type switch
        {
            GuildShopGoodsType.MemberExpansion => state.MaxMembers >= MostMembers
                ? (GuildShopErrors.MemberMaxOver, state)
                : (GuildShopErrors.None, state with { MaxMembers = Math.Min(state.MaxMembers + MemberStep, MostMembers) }),
            GuildShopGoodsType.Depot => state.DepotTabs > 0
                ? (GuildShopErrors.EnabledDepot, state)
                : (GuildShopErrors.None, state with { DepotTabs = 1 }),
            GuildShopGoodsType.DepotExpansion => state.DepotTabs == 0
                ? (GuildShopErrors.NotYetBuyDepot, state)
                : state.DepotTabs >= GuildDepotRules.MaxTabs
                    ? (GuildShopErrors.DepotMaxOver, state)
                    : (GuildShopErrors.None, state with { DepotTabs = state.DepotTabs + 1 }),
            GuildShopGoodsType.Emblem => state.EmblemPurchase != GuildEmblemPurchase.None
                ? (GuildShopErrors.EmblemCountOver, state)
                : (GuildShopErrors.None, state with { EmblemPurchase = GuildEmblemPurchase.OneEmblem }),
            GuildShopGoodsType.EmblemPackage => state.EmblemPurchase == GuildEmblemPurchase.Package
                ? (GuildShopErrors.EmblemPackageOwned, state)
                : (GuildShopErrors.None, state with { EmblemPurchase = GuildEmblemPurchase.Package }),
            _ => (GuildShopErrors.NotFoundGoods, state),
        };
        if (error != GuildShopErrors.None) return new GuildShopDecision(error, state);
        if (state.Coins < price) return new GuildShopDecision(GuildShopErrors.NotEnoughCoin, state);
        return new GuildShopDecision(GuildShopErrors.None, effect with { Coins = state.Coins - price });
    }
}

public sealed record GuildShopOrder(ulong GuildCode, string CharacterName, int GoodsIndex, GuildShopGoodsType Type, int Price);

public sealed record GuildShopReceipt(int Error, GuildShopState After);

public sealed record GuildShopHistoryEntry(DateTimeOffset At, string CharacterName, int GoodsIndex, int Coins);

public interface IGuildShopStore
{
    GuildShopState? StateOf(ulong guildCode);

    GuildShopReceipt Purchase(GuildShopOrder order);

    IReadOnlyList<GuildShopHistoryEntry> History(ulong guildCode);

    int? AddCoins(ulong guildCode, int amount);

    GuildEmblemState? EmblemOf(ulong guildCode);

    GuildEmblemDecision? RegisterEmblem(ulong guildCode, Func<GuildEmblemState, GuildEmblemDecision> decide);
}
