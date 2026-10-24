using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class RecycleFramesTests
{
    private static readonly byte[] OpenedCoupons = [1, 0, 2, 0];
    private static readonly byte[] CloseRequest = [1, 0];
    private static readonly byte[] ExchangeRequest =
    [
        0xFC, 0xF8, 0, 0,
        0x55, 0, 0, 0, 0, 0, 0, 0,
        2, 0,
        0xA2, 0x90, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        1, 0,
    ];

    [Fact]
    public void OpenAndCloseAreOkThenKind()
    {
        Assert.Equal(OpenedCoupons, RecycleFrames.Opened(RecycleKind.CouponExchange));
        Assert.Equal(OpenedCoupons, RecycleFrames.Closed(RecycleKind.CouponExchange));
        Assert.True(RecycleFrames.TryReadClose(CloseRequest, out var kind));
        Assert.Equal(1, kind);
    }

    [Fact]
    public void ExchangeRequestIsRead()
    {
        Assert.True(RecycleFrames.TryReadCouponExchange(ExchangeRequest, out var request));
        Assert.Equal(new CouponExchangeRequest(63740, 0x55, 2, 37026, 0, 1), request);
        Assert.False(RecycleFrames.TryReadCouponExchange(ExchangeRequest.AsSpan(0, 27), out _));
    }

    [Fact]
    public void ExchangedIsOkCountThenOperations()
    {
        var take = new BagOperation(QuestResultFrames.Removed, 1, 63740, 0x55, 0);
        var give = new BagOperation(QuestResultFrames.Created, 1, 37026, 0x66, 1);
        var frame = RecycleFrames.Exchanged(new[] { take, give });
        Assert.Equal(1, frame[0]);
        Assert.Equal(0, frame[1]);
        Assert.Equal(2, frame[2]);
        Assert.Equal(QuestResultFrames.ItemOperation(take).Concat(QuestResultFrames.ItemOperation(give)), frame[3..]);
    }

    [Fact]
    public void ItemRequestIsHeaderThenFourteenByteEntries()
    {
        var body = new byte[] { 9, 0, 0, 0, 13, 0, 2, 0 }
            .Concat(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0x34, 0x12, 0, 0, 7, 0 })
            .Concat(new byte[] { 2, 0, 0, 0, 0, 0, 0, 0, 0x35, 0x12, 0, 0, 7, 0 })
            .ToArray();

        Assert.True(RecycleFrames.TryReadItems(body, out var request));
        Assert.Equal((9u, (ushort)13), (request.Reserved, request.CouponTotal));
        Assert.Equal(new[] { new ItemRecycleEntry(1, 0x1234), new ItemRecycleEntry(2, 0x1235) }, request.Entries);
        Assert.False(RecycleFrames.TryReadItems(body.AsSpan(0, body.Length - 1), out _));
    }

    [Fact]
    public void ItemRefusalIsZeroOneZero()
    {
        Assert.Equal(new byte[] { 0, 0, 1, 0, 0, 0 }, RecycleFrames.ItemsRefused());
    }
    [Fact]
    public void MyCampRequestIsCouponThenTwelveByteEntries()
    {
        var body = new byte[] { 0xFC, 0xF8, 0, 0, 2, 0, 2, 0 }
            .Concat(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0x34, 0x12, 0, 0 })
            .Concat(new byte[] { 2, 0, 0, 0, 0, 0, 0, 0, 0x35, 0x12, 1, 0 })
            .ToArray();

        Assert.True(RecycleFrames.TryReadMyCampItems(body, out var request));
        Assert.Equal((63740u, (ushort)2), (request.CouponItemId, request.CouponCount));
        Assert.Equal(new[] { new MyCampRecycleEntry(1, 0x1234), new MyCampRecycleEntry(2, 0x1235) }, request.Entries);
        Assert.False(RecycleFrames.TryReadMyCampItems(body.AsSpan(0, body.Length - 1), out _));
    }

    [Fact]
    public void MyCampRefusalCarriesCodeThenKindOne()
    {
        Assert.Equal(new byte[] { 0, 0, 2, 0, 1, 0 }, RecycleFrames.MyCampRefused(RecycleRules.MyCampItemRefused));
    }

    [Fact]
    public void MyCampListTimeIsLocalFileTime()
    {
        var now = new DateTime(2014, 1, 28, 12, 0, 0, DateTimeKind.Local);
        var frame = RecycleFrames.MyCampListTime(now);
        Assert.Equal(8, frame.Length);
        Assert.Equal(new DateTime(2014, 1, 28, 12, 0, 0, DateTimeKind.Utc).ToFileTimeUtc(), BitConverter.ToInt64(frame));
    }

    [Fact]
    public void MyCampListReadyIsModeThreeStringsTimeAndNoSeats()
    {
        var frame = RecycleFrames.MyCampListReady("Ab", System.Text.Encoding.ASCII);
        Assert.Equal(new byte[] { 0, 0, 0, (byte)'A', (byte)'b', 0 }.Concat(new byte[16]).Concat(new byte[] { 0, 0 }), frame);
    }
}
