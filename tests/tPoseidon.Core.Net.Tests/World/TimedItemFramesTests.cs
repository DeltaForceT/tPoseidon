using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class TimedItemFramesTests
{
    [Fact]
    public void TheLoginFrameStartsWithTheServerClockThenListsEachItem()
    {
        var now = new DateTime(2026, 10, 17, 4, 30, 0, DateTimeKind.Utc);
        var expires = new DateTimeOffset(2026, 10, 20, 4, 30, 0, TimeSpan.Zero);

        var body = TimedItemFrames.Login(now, new[] { new TimedItem(0x11, 4500, expires) });

        Assert.Equal(8 + 2 + 21, body.Length);
        Assert.Equal(ClientClock.FileTime(now), BitConverter.ToUInt64(body, 0));
        Assert.Equal(now.ToLocalTime().Ticks, DateTime.FromFileTimeUtc((long)BitConverter.ToUInt64(body, 0)).Ticks);
        Assert.Equal(1, BitConverter.ToUInt16(body, 8));
        Assert.Equal(BagItemIds.OnWire(0x11), BitConverter.ToUInt64(body, 10));
        Assert.Equal(4500u, BitConverter.ToUInt32(body, 18));
        Assert.Equal(ClientClock.FileTime(expires), BitConverter.ToUInt64(body, 22));
        Assert.Equal(0, body[30]);
    }

    [Fact]
    public void NoticeAndExpiryCarryTheWireUid()
    {
        Assert.Equal(12, TimedItemFrames.Notice(0x11, 59).Length);
        Assert.Equal(59u, BitConverter.ToUInt32(TimedItemFrames.Notice(0x11, 59), 8));
        Assert.Equal(new byte[] { 0x11, 0, 0, 0, 1, 0, 0, 0, 0 }, TimedItemFrames.Expired(0x11));
    }
}
