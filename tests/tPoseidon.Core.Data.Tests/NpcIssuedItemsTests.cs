using tPoseidon.Core.Data;

namespace tPoseidon.Core.Data.Tests;

public sealed class NpcIssuedItemsTests
{
    [Fact]
    public void EachTakeUsesOneOfTheIssuedCount()
    {
        var issued = new NpcIssuedItems();
        Assert.Null(issued.Get(0x1000));
        Assert.False(issued.TryTakeOne(0x1000, 1001));

        issued.Set(0x1000, 1001, 2);
        Assert.False(issued.TryTakeOne(0x1000, 1002));
        Assert.True(issued.TryTakeOne(0x1000, 1001));
        Assert.True(issued.TryTakeOne(0x1000, 1001));
        Assert.False(issued.TryTakeOne(0x1000, 1001));
        Assert.Equal(new NpcIssuedItem(1001, 0), issued.Get(0x1000));
    }
}
