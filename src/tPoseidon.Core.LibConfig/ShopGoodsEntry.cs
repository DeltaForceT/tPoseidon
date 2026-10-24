namespace tPoseidon.Core.LibConfig;

public sealed record ShopGoodsEntry
{
    public required string Table { get; init; }
    public required int ItemId { get; init; }
    public float PurchaseRate { get; init; }
    public float SellingRate { get; init; }
    public bool OutOfStock { get; init; }
}
