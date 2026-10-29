namespace tPoseidon.Core.LibConfig;

public sealed record GuildShopGood(int Index, int Type, int Price);

public sealed record GuildShopTables(IReadOnlyDictionary<int, GuildShopGood> Goods)
{
    public const string Table = "GuildShopGoodsTable";

    public static GuildShopTables Empty { get; } = new(new Dictionary<int, GuildShopGood>());
}
