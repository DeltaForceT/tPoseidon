using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Core.Net.Tests.Social;

public sealed class ItemPriceFramesTests
{
    [Fact]
    public void ReportAndQueryCarryTheItemAndThePrice()
    {
        Assert.True(ItemPriceFrames.TryReadReport([5, 0, 0, 0, 0x2C, 1, 0, 0], out var item, out var price));
        Assert.Equal((5u, 300), (item, price));
        Assert.False(ItemPriceFrames.TryReadReport([5, 0, 0, 0], out _, out _));
        Assert.True(ItemPriceFrames.TryReadQuery([9, 0, 0, 0], out var queried));
        Assert.Equal(9u, queried);
    }

    [Fact]
    public void ReplyIsItemMinimumMaximumAverageOrZeros()
    {
        Assert.Equal(new byte[] { 5, 0, 0, 0, 1, 0, 0, 0, 3, 0, 0, 0, 2, 0, 0, 0 }, ItemPriceFrames.Reply(5, new ItemPriceStatistics(1, 3, 2)));
        Assert.Equal(new byte[] { 5, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, ItemPriceFrames.Reply(5, null));
    }
}
