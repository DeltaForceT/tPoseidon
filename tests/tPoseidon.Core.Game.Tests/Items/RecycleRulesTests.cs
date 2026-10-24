using tPoseidon.Core.Game.Items;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Items;

public sealed class RecycleRulesTests
{
    private static readonly RecycleChangeEntry Row = new(37026, 1, 63740, 2);

    private static CouponExchangeRequest Request(uint source, ushort count) => new(source, 0x55, count, 37026, 0, 1);

    [Fact]
    public void ExchangeNeedsTheRowSourceAndCount()
    {
        Assert.Equal(CouponExchangeRefusal.NoRow, RecycleRules.Check(Request(63740, 2), null, 63740, 5, true));
        Assert.Equal(CouponExchangeRefusal.SourceMismatch, RecycleRules.Check(Request(63740, 1), Row, 63740, 5, true));
        Assert.Equal(CouponExchangeRefusal.SourceMismatch, RecycleRules.Check(Request(63741, 2), Row, 63741, 5, true));
        Assert.Equal(CouponExchangeRefusal.SourceNotInBag, RecycleRules.Check(Request(63740, 2), Row, null, 0, true));
        Assert.Equal(CouponExchangeRefusal.SourceCountTooLow, RecycleRules.Check(Request(63740, 2), Row, 63740, 1, true));
        Assert.Equal(CouponExchangeRefusal.NoBagSpace, RecycleRules.Check(Request(63740, 2), Row, 63740, 2, false));
        Assert.Null(RecycleRules.Check(Request(63740, 2), Row, 63740, 2, true));
    }

    [Theory]
    [InlineData(2, 0x40001, 0)]
    [InlineData(0xD, 0x40001, 3)]
    [InlineData(0x22, 0x40001, 9)]
    [InlineData(2, 0x4040001, 0x14)]
    [InlineData(0xD, 0x4040001, 0x17)]
    [InlineData(0x1B, 0x4040001, 0x27)]
    public void RecycleTypeFollowsTheItemTypeAndTheMarkedOption(int itemType, int options, int expected)
    {
        Assert.Equal(expected, RecycleRules.RecycleTypeOf(itemType, options));
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(2, 0x1)]
    [InlineData(2, 0x40000)]
    [InlineData(2, 0x4000001)]
    [InlineData(9, 0x40001)]
    public void ItemsWithoutBothOptionsOrAMappedTypeHaveNoRecycleType(int itemType, int options)
    {
        Assert.Null(RecycleRules.RecycleTypeOf(itemType, options));
    }
}
