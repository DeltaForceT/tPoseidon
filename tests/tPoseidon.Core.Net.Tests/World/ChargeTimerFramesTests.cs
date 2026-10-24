using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class ChargeTimerFramesTests
{
    [Fact]
    public void TheRequestCarriesBothItemsTheCountAndTheOption()
    {
        var payload = Convert.FromHexString("0100000000000080" + "0200000000000080" + "28000000" + "F1030000" + "A0050000" + "3C000000");
        Assert.True(ChargeTimerFrames.TryReadRequest(payload, out var request));
        Assert.Equal(new RechargeRequest(0x8000000000000001, 0x8000000000000002, 40, 1009, 1440, 60), request);
        Assert.False(ChargeTimerFrames.TryReadRequest(payload.AsSpan(0, 31), out _));
    }

    [Fact]
    public void TheAnswerNamesTheItemItsNewTimeAndTheCouponChange()
    {
        var expiry = new DateTimeOffset(2026, 12, 24, 12, 0, 0, TimeSpan.Zero);
        var coupon = new BagOperation(QuestResultFrames.Removed, 1, 19648, 0x8000000000000002, 0);
        var frame = ChargeTimerFrames.Done(0x8000000000000001, 1956, expiry, coupon);
        Assert.Equal(Convert.FromHexString("0100000000000080A4070000"), frame[..12]);
        Assert.Equal(BitConverter.GetBytes(ClientClock.FileTime(expiry)), frame[12..20]);
        Assert.Equal(QuestResultFrames.ItemOperation(coupon), frame[20..]);
    }
}
