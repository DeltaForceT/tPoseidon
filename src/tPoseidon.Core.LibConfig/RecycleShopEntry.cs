namespace tPoseidon.Core.LibConfig;

public sealed record RecycleShopEntry(int RecycleType, int CouponCount, int CouponItemId)
{
    public const string TableName = "RecycleShop";
    public const string ExceptTableName = "RecycleExceptItem";
    public const string MyCampExceptTableName = "RecyExceptMCItem";
}
