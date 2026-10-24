namespace tPoseidon.Core.LibConfig;

public sealed record VipShopOffer(int Id, int Category, int ItemId, int ItemCount, int EggCost)
{
    public const int EggItemId = 0xF633;
    public const int MaxListed = 0x40;
}
