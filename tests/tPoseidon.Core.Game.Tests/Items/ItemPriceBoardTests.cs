using tPoseidon.Core.Game.Items;

namespace tPoseidon.Core.Game.Tests.Items;

public sealed class ItemPriceBoardTests
{
    [Fact]
    public void AnUnknownItemHasNoStatistics()
    {
        Assert.Null(new ItemPriceBoard().Find(7));
    }

    [Fact]
    public void ReportsKeepMinimumMaximumAndTruncatedAverage()
    {
        var board = new ItemPriceBoard();
        board.Record(7, 100);
        Assert.Equal(new ItemPriceStatistics(100, 100, 100), board.Find(7));
        board.Record(7, 50);
        board.Record(7, 251);
        Assert.Equal(new ItemPriceStatistics(50, 251, 133), board.Find(7));
        Assert.Null(board.Find(8));
    }
}
