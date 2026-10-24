using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;

namespace tPoseidon.Core.Game.Tests.Items;

public sealed class TimedItemsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    private static Item Held(ulong uid, DateTimeOffset breaked) => new()
    {
        ItemUid = uid, ItemIndex = 4500, ItemClass = 2, CharacterId = 1, SlotKind = SlotKind.Inventory, SlotPosition = 0, BreakedAt = breaked,
    };

    [Fact]
    public void OnlyItemsWithAtLeastAMinuteLeftAreListed()
    {
        var items = new[] { Held(1, Item.NeverBreaks), Held(2, Now.AddSeconds(30)), Held(3, Now.AddHours(5)), Held(4, Now.AddHours(-1)) };

        Assert.Equal(new ulong[] { 3 }, TimedItemRules.Running(items, Now).Select(t => t.ItemUid));
        Assert.True(TimedItemRules.HasExpired(items[3], Now));
        Assert.False(TimedItemRules.HasExpired(items[0], Now));
    }

    [Theory]
    [InlineData(1441, null)]
    [InlineData(1440, 0)]
    [InlineData(61, 0)]
    [InlineData(60, 1)]
    [InlineData(11, 1)]
    [InlineData(10, 2)]
    [InlineData(2, 2)]
    [InlineData(1, 3)]
    [InlineData(0, null)]
    public void NoticesComeInBands(int minutes, int? band)
    {
        Assert.Equal(band, TimedItemRules.NoticeBand(minutes));
    }

    [Fact]
    public void TheWatchWarnsOncePerBandThenExpires()
    {
        var watch = new TimedItemWatch();
        watch.Track(new TimedItem(7, 4500, Now.AddMinutes(90)));

        Assert.Equal(90, Assert.Single(watch.Tick(Now)).MinutesLeft);
        Assert.Empty(watch.Tick(Now.AddMinutes(1)));
        Assert.Equal(60, Assert.Single(watch.Tick(Now.AddMinutes(30))).MinutesLeft);
        Assert.Empty(watch.Tick(Now.AddMinutes(31)));
        Assert.Equal(9, Assert.Single(watch.Tick(Now.AddMinutes(81))).MinutesLeft);
        var last = Assert.Single(watch.Tick(Now.AddMinutes(90)));
        Assert.True(last.Expired);
        Assert.Equal(0, watch.Count);
    }
}
