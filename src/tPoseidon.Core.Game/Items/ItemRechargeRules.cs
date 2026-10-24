using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Items;

public readonly record struct RechargeRequest(ulong ItemWireUid, ulong CouponWireUid, int CouponCount, int LimitCode, int LimitUseHours, int BonusHours);

public enum RechargeCheck
{
    Ok,
    Busy,
    NoItem,
    NotTimeLimited,
    NoCoupon,
    NotEnoughCoupons,
    NotACoupon,
    NoSuchOption,
    TooLong,
}

public static class ItemRechargeRules
{
    public const int TimeLimitedExistType = 1;
    public const int CouponEffect = 30;
    public const int MaxRemainingMinutes = 259200;
    public const int MinutesPerHour = 60;

    public static RechargeCheck Check(
        RechargeRequest request,
        Item? item,
        ItemParamEntry? itemDefinition,
        Item? coupon,
        ItemParamEntry? couponDefinition,
        IReadOnlyList<RechargeOption> options,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (item is null || itemDefinition is null) return RechargeCheck.NoItem;
        if (itemDefinition.ExistType != TimeLimitedExistType) return RechargeCheck.NotTimeLimited;
        if (coupon is null || couponDefinition is null) return RechargeCheck.NoCoupon;
        if (request.CouponCount < 1 || coupon.Stack < request.CouponCount) return RechargeCheck.NotEnoughCoupons;
        if (couponDefinition.Effect != CouponEffect) return RechargeCheck.NotACoupon;
        if (!options.Contains(new RechargeOption(request.LimitCode, request.LimitUseHours, request.BonusHours, request.CouponCount))) return RechargeCheck.NoSuchOption;
        if (RemainingMinutes(item, now) + (long)(request.LimitUseHours + request.BonusHours) * MinutesPerHour > MaxRemainingMinutes) return RechargeCheck.TooLong;
        return RechargeCheck.Ok;
    }

    public static long RemainingMinutes(Item item, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.BreakedAt > now ? (long)Math.Ceiling((item.BreakedAt - now).TotalMinutes) : 0;
    }

    public static DateTimeOffset NewExpiry(DateTimeOffset breakedAt, DateTimeOffset now, int hours) =>
        (breakedAt > now ? breakedAt : now).AddHours(hours);
}
