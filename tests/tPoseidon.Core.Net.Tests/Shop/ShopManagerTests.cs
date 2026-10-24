using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Shop;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Net.Tests.Shop;

public class ShopManagerTests
{
    private const ushort CoralTownMapId = 148;
    private const string CoralTownName = "Coral_Town";

    private static ShopManager MakeManager(
        IEnumerable<ShopZoneEntry>? entries = null,
        IEnumerable<((ushort MapId, ushort PosId) Key, NpcPoint Point)>? points = null)
    {
        var lookup = new InMemoryShopZoneLookup(entries ?? Array.Empty<ShopZoneEntry>());
        var pointProvider = new InMemoryNpcPointProvider(points ?? Array.Empty<((ushort, ushort), NpcPoint)>());
        var ids = new IdAllocator();
        return new ShopManager(lookup, pointProvider, ids);
    }

    [Fact]
    public void CreateShop_WithKnownPoint_PopulatesAllFields()
    {
        var row = new ShopZoneEntry
        {
            MapName = CoralTownName,
            NpcType = 5001,
            PosId = 3,
            PurchaseRate = 1.0f,
            SellingRate = 0.5f,
            ItemTable = "ShopItem_Square",
            Script = "hello",
        };
        var mgr = MakeManager(
            new[] { row },
            new[] { ((CoralTownMapId, (ushort)3), NpcPoint.Xy(1000, 2000)) });

        var shop = mgr.CreateShop(CoralTownMapId, CoralTownName, row);

        Assert.NotNull(shop);
        Assert.Equal(CoralTownMapId, shop!.MapId);
        Assert.Equal(5001, shop.NpcType);
        Assert.Equal(3, shop.PosId);
        Assert.Equal(1.0f, shop.PurchaseRate);
        Assert.Equal(0.5f, shop.SellingRate);
        Assert.Equal("ShopItem_Square", shop.ItemTable);
        Assert.Equal("hello", shop.Script);
        Assert.Equal(1000, shop.SpawnX);
        Assert.Equal(2000, shop.SpawnY);
        Assert.NotEqual((ushort)0, shop.EntityId);
    }

    [Fact]
    public void CreateShop_MissingPoint_FallsBackToOrigin()
    {
        var row = new ShopZoneEntry
        {
            MapName = CoralTownName,
            NpcType = 5001,
            PosId = 99,
            ItemTable = "ShopItem_Square",
        };
        var mgr = MakeManager(new[] { row });

        var shop = mgr.CreateShop(CoralTownMapId, CoralTownName, row);

        Assert.NotNull(shop);
        Assert.Equal(600, shop!.SpawnX);
        Assert.Equal(600, shop.SpawnY);
    }

    [Fact]
    public void PreloadMap_CreatesAllRowsForThatMap()
    {
        var rows = new[]
        {
            new ShopZoneEntry { MapName = CoralTownName, NpcType = 5001, PosId = 1, ItemTable = "A" },
            new ShopZoneEntry { MapName = CoralTownName, NpcType = 5002, PosId = 2, ItemTable = "B" },
            new ShopZoneEntry { MapName = "Other_Map", NpcType = 5003, PosId = 1, ItemTable = "C" },
        };
        var points = new[]
        {
            ((CoralTownMapId, (ushort)1), NpcPoint.Xy(100, 100)),
            ((CoralTownMapId, (ushort)2), NpcPoint.Xy(200, 200)),
        };
        var mgr = MakeManager(rows, points);

        var created = mgr.PreloadMap(CoralTownMapId, CoralTownName);

        Assert.Equal(2, created.Count);
        Assert.Equal(2, mgr.Count);
        Assert.Contains(created, s => s.NpcType == 5001 && s.SpawnX == 100);
        Assert.Contains(created, s => s.NpcType == 5002 && s.SpawnX == 200);
    }

    [Fact]
    public void GetByKey_RoundTripsCreatedShop()
    {
        var row = new ShopZoneEntry
        {
            MapName = CoralTownName,
            NpcType = 5001,
            PosId = 3,
            ItemTable = "ShopItem_Square",
        };
        var mgr = MakeManager(
            new[] { row },
            new[] { ((CoralTownMapId, (ushort)3), NpcPoint.Xy(0, 0)) });

        var created = mgr.CreateShop(CoralTownMapId, CoralTownName, row);
        Assert.NotNull(created);

        var fetched = mgr.GetByKey(CoralTownMapId, 5001, 3);
        Assert.Same(created, fetched);
    }

    [Fact]
    public void Remove_FreesEntityIdAndKey()
    {
        var row = new ShopZoneEntry
        {
            MapName = CoralTownName,
            NpcType = 5001,
            PosId = 3,
            ItemTable = "x",
        };
        var mgr = MakeManager(
            new[] { row },
            new[] { ((CoralTownMapId, (ushort)3), NpcPoint.Xy(0, 0)) });

        var shop = mgr.CreateShop(CoralTownMapId, CoralTownName, row)!;
        Assert.Equal(1, mgr.Count);
        Assert.True(mgr.Remove(shop.EntityId));
        Assert.Equal(0, mgr.Count);
        Assert.Null(mgr.GetByKey(CoralTownMapId, 5001, 3));
    }

    [Fact]
    public void GetByMap_FiltersByMapId()
    {
        var rows = new[]
        {
            new ShopZoneEntry { MapName = CoralTownName, NpcType = 5001, PosId = 1, ItemTable = "A" },
            new ShopZoneEntry { MapName = CoralTownName, NpcType = 5002, PosId = 2, ItemTable = "B" },
        };
        var points = new[]
        {
            ((CoralTownMapId, (ushort)1), NpcPoint.Xy(0, 0)),
            ((CoralTownMapId, (ushort)2), NpcPoint.Xy(0, 0)),
        };
        var mgr = MakeManager(rows, points);
        mgr.PreloadMap(CoralTownMapId, CoralTownName);

        var inMap = mgr.GetByMap(CoralTownMapId);
        Assert.Equal(2, inMap.Count);

        var inOtherMap = mgr.GetByMap(999);
        Assert.Empty(inOtherMap);
    }

    [Fact]
    public void PreloadShopTable_UsesTheMapsShopTableName()
    {
        var rows = new[]
        {
            new ShopZoneEntry { MapName = "Coral_Town", NpcType = 5001, PosId = 1, ItemTable = "A" },
            new ShopZoneEntry { MapName = "Square", NpcType = 5002, PosId = 1, ItemTable = "B" },
        };
        var mgr = MakeManager(rows, new[] { ((CoralTownMapId, (ushort)1), NpcPoint.Xy(1330, 886)) });

        var created = mgr.PreloadShopTable(CoralTownMapId, "Shop_Coral_Town");

        var shop = Assert.Single(created);
        Assert.Equal(5001, shop.NpcType);
        Assert.Equal(CoralTownMapId, shop.MapId);
        Assert.Equal(1330, shop.SpawnX);
        Assert.Equal(886, shop.SpawnY);
    }

    [Fact]
    public void PreloadShopTable_SharedTableGivesEachMapItsOwnShops()
    {
        var rows = new[] { new ShopZoneEntry { MapName = "Chaos1", NpcType = 5018, PosId = 1, ItemTable = "A" } };
        var mgr = MakeManager(rows);

        var first = Assert.Single(mgr.PreloadShopTable(10, "Shop_Chaos1"));
        var second = Assert.Single(mgr.PreloadShopTable(11, "Shop_Chaos1"));

        Assert.NotEqual(first.EntityId, second.EntityId);
        Assert.Single(mgr.GetByMap(10));
        Assert.Single(mgr.GetByMap(11));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Shop_")]
    [InlineData("NpcTalk_Coral_Town")]
    public void PreloadShopTable_WithoutAShopTableCreatesNothing(string shopTable)
    {
        var rows = new[] { new ShopZoneEntry { MapName = "Coral_Town", NpcType = 5001, PosId = 1, ItemTable = "A" } };
        var mgr = MakeManager(rows);

        Assert.Empty(mgr.PreloadShopTable(CoralTownMapId, shopTable));
        Assert.Equal(0, mgr.Count);
    }
}
