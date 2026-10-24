using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Items;

public sealed class NewDrillLifeTests
{
    private static ItemManager Manager() =>
        new(new InMemoryItemStore(), new InMemoryItemParamLookup(new[] { new ItemParamEntry { Id = 8000, Class = ItemParamEntry.RareClassInTable } }))
        {
            DrillLifeOf = itemId => itemId == 8000 ? 300 : null,
        };

    [Fact]
    public void ANewDrillStartsWithItsFullDrillLife()
    {
        var manager = Manager();

        var drill = manager.GrantRewardItem(1, 8000, 1);

        Assert.Equal(300, drill.Stack);
    }

    [Fact]
    public void ANewDrillIsStoredAsARareItem()
    {
        var drill = Manager().GrantRewardItem(1, 8000, 1);

        Assert.Equal(ItemParamEntry.RareClass, drill.ItemClass);
    }

    [Fact]
    public void OtherItemsKeepTheirCount()
    {
        Assert.Equal(5, Manager().NewStack(1001, 5));
    }
}
