namespace tPoseidon.Core.Data;

public sealed record ItemRecharge(ulong CharacterId, ulong ItemUid, int Hours, ulong CouponUid, int CouponCount);

public sealed record ItemRechargeResult(int Error, DateTimeOffset BreakedAt, int CouponLeft)
{
    public bool Succeeded => Error == ItemRechargeErrors.Ok;

    public static ItemRechargeResult Refused(int error) => new(error, DateTimeOffset.MinValue, 0);
}

public static class ItemRechargeErrors
{
    public const int Ok = 0;
    public const int ItemNotHeld = 101;
    public const int CouponNotHeld = 102;
    public const int NotEnoughCoupons = 103;
    public const int StoreFailed = 104;
}

public interface IItemRechargeStore
{
    ItemRechargeResult Recharge(ItemRecharge recharge, DateTimeOffset now);
}
