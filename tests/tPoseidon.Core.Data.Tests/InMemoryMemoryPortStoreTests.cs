using tPoseidon.Core.Data;

namespace tPoseidon.Core.Data.Tests;

public sealed class InMemoryMemoryPortStoreTests
{
    [Fact]
    public void InsertRefusesAUsedSlotAndDeleteNeedsTheRow()
    {
        var store = new InMemoryMemoryPortStore();
        var slot = new MemoryPort { CharacterId = 1, PortType = 0, SlotNum = 2, Zone = 12, PositionX = 3, PositionY = 4 };

        Assert.True(store.TryInsert(slot));
        Assert.False(store.TryInsert(slot with { Zone = 13 }));
        Assert.Equal(12, store.Find(1, 0, 2)!.Zone);
        Assert.True(store.Delete(1, 0, 2));
        Assert.False(store.Delete(1, 0, 2));
    }
}
