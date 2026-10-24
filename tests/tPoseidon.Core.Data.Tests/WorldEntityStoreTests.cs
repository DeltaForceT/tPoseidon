using tPoseidon.Core.Common;

namespace tPoseidon.Core.Data.Tests;

public class WorldEntityStoreTests
{
    private static NpcEntity Heidi(ushort entityId = 0x0153, ushort mapId = 148) => new()
    {
        EntityId = entityId,
        MapId = mapId,
        ChrType = 1311,
        PosId = 2,
        Spawn = NpcPoint.Xy(1062, 2076),
        HeadMarkType = 0,
    };

    private static NpcEntity BunnyMaid(ushort entityId = 0x014C, ushort mapId = 148) => new()
    {
        EntityId = entityId,
        MapId = mapId,
        ChrType = 1011,
        PosId = 5,
        Spawn = NpcPoint.Xy(762, 1500),
        HeadMarkType = 0,
    };

    [Fact]
    public void Install_AddsToStore()
    {
        var store = new WorldEntityStore();
        store.Install(Heidi());
        Assert.Equal(1, store.Count);
        Assert.NotNull(store.Get(0x0153));
    }

    [Fact]
    public void Install_DuplicateEntityId_Throws()
    {
        var store = new WorldEntityStore();
        store.Install(Heidi());
        Assert.Throws<InvalidOperationException>(() => store.Install(Heidi()));
    }

    [Fact]
    public void Get_NonexistentEntity_ReturnsNull()
    {
        var store = new WorldEntityStore();
        Assert.Null(store.Get(0x9999));
    }

    [Fact]
    public void InMap_ReturnsNpcsForThatMap()
    {
        var store = new WorldEntityStore();
        store.Install(Heidi());
        store.Install(BunnyMaid());
        store.Install(Heidi(0x0250, mapId: 153));

        var coral = store.InMap(148);
        var beach = store.InMap(153);
        Assert.Equal(2, coral.Count);
        Assert.Single(beach);
        Assert.Equal((ushort)0x0153, coral[^1].EntityId);
        Assert.Equal((ushort)0x014C, coral[0].EntityId);
        Assert.Equal((ushort)0x0250, beach[0].EntityId);
    }

    [Fact]
    public void InMap_OrdersByEntityIdAscending()
    {
        var store = new WorldEntityStore();
        store.Install(Heidi(0x0153));
        store.Install(BunnyMaid(0x014C));
        var inMap = store.InMap(148);
        Assert.Equal((ushort)0x014C, inMap[0].EntityId);
        Assert.Equal((ushort)0x0153, inMap[1].EntityId);
    }

    [Fact]
    public void FindByTypeOnMap_FiltersByMapAndType()
    {
        var store = new WorldEntityStore();
        store.Install(Heidi(0x0153, mapId: 148));
        store.Install(Heidi(0x0250, mapId: 153));
        store.Install(BunnyMaid(0x014C, mapId: 148));

        Assert.Equal((ushort)0x0153,
            store.FindByTypeOnMap(chrType: 1311, mapId: 148)?.EntityId);
        Assert.Equal((ushort)0x0250,
            store.FindByTypeOnMap(chrType: 1311, mapId: 153)?.EntityId);
        Assert.Null(store.FindByTypeOnMap(chrType: 1311, mapId: 999));
        Assert.Null(store.FindByTypeOnMap(chrType: 9999, mapId: 148));
    }

    [Fact]
    public void Remove_DeletesEntity()
    {
        var store = new WorldEntityStore();
        store.Install(Heidi());
        Assert.True(store.Remove(0x0153));
        Assert.Null(store.Get(0x0153));
        Assert.Equal(0, store.Count);
        Assert.False(store.Remove(0x0153));
    }
}
