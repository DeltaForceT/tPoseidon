using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Items;

public sealed class ItemRechargeRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly ItemParamEntry Timed = new() { Id = 1956, ExistType = ItemRechargeRules.TimeLimitedExistType };
    private static readonly ItemParamEntry Coupon = new() { Id = 19648, Effect = ItemRechargeRules.CouponEffect };
    private static readonly RechargeOption SixtyDays = new(1009, 1440, 60, 40);
    private static readonly RechargeRequest Request = new(1, 2, 40, 1009, 1440, 60);

    private static Item Held(int index, ushort stack, DateTimeOffset breaked) =>
        new() { ItemUid = (ulong)index, ItemIndex = index, ItemClass = 1, Stack = stack, BreakedAt = breaked, CharacterId = 1, SlotKind = SlotKind.Inventory, SlotPosition = 0 };

    [Fact]
    public void ACatalogueOptionWithEnoughCouponsIsAccepted()
    {
        var check = ItemRechargeRules.Check(Request, Held(1956, 1, Now.AddDays(-3)), Timed, Held(19648, 40, Item.NeverBreaks), Coupon, new[] { SixtyDays }, Now);
        Assert.Equal(RechargeCheck.Ok, check);
    }

    [Fact]
    public void AnItemWithoutATimeLimitIsRefused()
    {
        var check = ItemRechargeRules.Check(Request, Held(1956, 1, Now), Timed with { ExistType = 0 }, Held(19648, 40, Item.NeverBreaks), Coupon, new[] { SixtyDays }, Now);
        Assert.Equal(RechargeCheck.NotTimeLimited, check);
    }

    [Fact]
    public void TooFewCouponsOrNoCouponEffectAreRefused()
    {
        Assert.Equal(RechargeCheck.NotEnoughCoupons, ItemRechargeRules.Check(Request, Held(1956, 1, Now), Timed, Held(19648, 39, Item.NeverBreaks), Coupon, new[] { SixtyDays }, Now));
        Assert.Equal(RechargeCheck.NotACoupon, ItemRechargeRules.Check(Request, Held(1956, 1, Now), Timed, Held(19648, 40, Item.NeverBreaks), Coupon with { Effect = 0 }, new[] { SixtyDays }, Now));
    }

    [Fact]
    public void AnOptionNotInTheCatalogueIsRefused()
    {
        var check = ItemRechargeRules.Check(Request with { BonusHours = 90 }, Held(1956, 1, Now), Timed, Held(19648, 40, Item.NeverBreaks), Coupon, new[] { SixtyDays }, Now);
        Assert.Equal(RechargeCheck.NoSuchOption, check);
    }

    [Fact]
    public void TheRemainingTimeMayNotPassOneHundredAndEightyDays()
    {
        var check = ItemRechargeRules.Check(Request, Held(1956, 1, Now.AddDays(120)), Timed, Held(19648, 40, Item.NeverBreaks), Coupon, new[] { SixtyDays }, Now);
        Assert.Equal(RechargeCheck.TooLong, check);
        Assert.Equal(RechargeCheck.Ok, ItemRechargeRules.Check(Request, Held(1956, 1, Now.AddDays(117)), Timed, Held(19648, 40, Item.NeverBreaks), Coupon, new[] { SixtyDays }, Now));
    }

    [Fact]
    public void TheNewTimeStartsFromNowForAnExpiredItem()
    {
        Assert.Equal(Now.AddHours(1500), ItemRechargeRules.NewExpiry(Now.AddDays(-1), Now, 1500));
        Assert.Equal(Now.AddDays(2).AddHours(75), ItemRechargeRules.NewExpiry(Now.AddDays(2), Now, 75));
    }
}
