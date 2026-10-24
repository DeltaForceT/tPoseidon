using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Shop;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Net.Tests.Shop;

public class SkillShopManagerTests
{
    private const ushort BeachTownMapId = 152;
    private const string BeachTownName = "Beach_Town";

    private static SkillShopManager MakeManager(
        IEnumerable<SkillShopZoneEntry>? entries = null,
        IEnumerable<((ushort MapId, ushort PosId) Key, NpcPoint Point)>? points = null)
    {
        var lookup = new InMemorySkillShopZoneLookup(entries ?? Array.Empty<SkillShopZoneEntry>());
        var pointProvider = new InMemoryNpcPointProvider(points ?? Array.Empty<((ushort, ushort), NpcPoint)>());
        var ids = new IdAllocator();
        return new SkillShopManager(lookup, pointProvider, ids);
    }

    [Fact]
    public void CreateSkillShop_WithKnownPoint_PopulatesAllFields()
    {
        var row = new SkillShopZoneEntry
        {
            MapName = BeachTownName,
            NpcType = 6300,
            PosId = 11,
            JobQuestId = 1234,
            LimitJobType = 2,
            ChangeType = 1,
            ChangeJob = 5,
            PurchaseRate = 1.0f,
            SellingRate = 0.5f,
            ItemTable = "SkillShopItem_Beach",
            LimitSkillMaster = "MasterA",
        };
        var mgr = MakeManager(
            new[] { row },
            new[] { ((BeachTownMapId, (ushort)11), NpcPoint.Xy(1500, 2500)) });

        var shop = mgr.CreateSkillShop(BeachTownMapId, BeachTownName, row);

        Assert.NotNull(shop);
        Assert.Equal(BeachTownMapId, shop!.MapId);
        Assert.Equal(6300, shop.NpcType);
        Assert.Equal(11, shop.PosId);
        Assert.Equal(1234, shop.JobQuestId);
        Assert.Equal(2, shop.LimitJobType);
        Assert.Equal(1, shop.ChangeType);
        Assert.Equal(5, shop.ChangeJob);
        Assert.Equal(1.0f, shop.PurchaseRate);
        Assert.Equal(0.5f, shop.SellingRate);
        Assert.Equal("SkillShopItem_Beach", shop.ItemTable);
        Assert.Equal("MasterA", shop.LimitSkillMaster);
        Assert.Equal(1500, shop.SpawnX);
        Assert.Equal(2500, shop.SpawnY);
    }

    [Fact]
    public void CreateSkillShop_MissingPoint_FallsBackToOrigin()
    {
        var row = new SkillShopZoneEntry { MapName = BeachTownName, NpcType = 6300, PosId = 99 };
        var mgr = MakeManager(new[] { row });

        var shop = mgr.CreateSkillShop(BeachTownMapId, BeachTownName, row);
        Assert.NotNull(shop);
        Assert.Equal(600, shop!.SpawnX);
        Assert.Equal(600, shop.SpawnY);
    }

    [Fact]
    public void PreloadMap_CreatesAllRowsForThatMap()
    {
        var rows = new[]
        {
            new SkillShopZoneEntry { MapName = BeachTownName, NpcType = 6300, PosId = 1 },
            new SkillShopZoneEntry { MapName = BeachTownName, NpcType = 6301, PosId = 2 },
            new SkillShopZoneEntry { MapName = "Other_Map", NpcType = 6302, PosId = 1 },
        };
        var points = new[]
        {
            ((BeachTownMapId, (ushort)1), NpcPoint.Xy(100, 100)),
            ((BeachTownMapId, (ushort)2), NpcPoint.Xy(200, 200)),
        };
        var mgr = MakeManager(rows, points);

        var created = mgr.PreloadMap(BeachTownMapId, BeachTownName);
        Assert.Equal(2, created.Count);
        Assert.Equal(2, mgr.Count);
    }

    [Fact]
    public void GetByKey_RoundTripsCreatedShop()
    {
        var row = new SkillShopZoneEntry { MapName = BeachTownName, NpcType = 6300, PosId = 11 };
        var mgr = MakeManager(
            new[] { row },
            new[] { ((BeachTownMapId, (ushort)11), NpcPoint.Xy(0, 0)) });

        var created = mgr.CreateSkillShop(BeachTownMapId, BeachTownName, row);
        Assert.NotNull(created);
        Assert.Same(created, mgr.GetByKey(BeachTownMapId, 6300, 11));
    }

    [Fact]
    public void Remove_FreesEntityIdAndKey()
    {
        var row = new SkillShopZoneEntry { MapName = BeachTownName, NpcType = 6300, PosId = 11 };
        var mgr = MakeManager(
            new[] { row },
            new[] { ((BeachTownMapId, (ushort)11), NpcPoint.Xy(0, 0)) });

        var shop = mgr.CreateSkillShop(BeachTownMapId, BeachTownName, row)!;
        Assert.True(mgr.Remove(shop.EntityId));
        Assert.Equal(0, mgr.Count);
        Assert.Null(mgr.GetByKey(BeachTownMapId, 6300, 11));
    }

    [Fact]
    public void PreloadSkillShopTable_UsesTheMapsTableAndKeepsThePages()
    {
        var pages = new[] { (0, "Hello, Exit, SkillShop_Exit"), (0, "Second page") };
        var rows = new[]
        {
            new SkillShopZoneEntry { MapName = BeachTownName, NpcType = 6300, PosId = 11, ItemTable = "SkillShopItem_A", Pages = pages },
            new SkillShopZoneEntry { MapName = "Square", NpcType = 6301, PosId = 1, ItemTable = "SkillShopItem_B" },
        };
        var mgr = MakeManager(rows, new[] { ((BeachTownMapId, (ushort)11), NpcPoint.Xy(1257, 2496)) });

        var shop = Assert.Single(mgr.PreloadSkillShopTable(BeachTownMapId, "SkillShopTalk_Beach_Town"));

        Assert.Equal(6300, shop.NpcType);
        Assert.Equal((1257, 2496), ((int)shop.SpawnX, (int)shop.SpawnY));
        Assert.Equal(pages, shop.Pages);
    }

    [Theory]
    [InlineData("")]
    [InlineData("SkillShopTalk_")]
    [InlineData("Shop_Beach_Town")]
    public void PreloadSkillShopTable_WithoutATableCreatesNothing(string table)
    {
        var rows = new[] { new SkillShopZoneEntry { MapName = BeachTownName, NpcType = 6300, PosId = 11 } };
        var mgr = MakeManager(rows);

        Assert.Empty(mgr.PreloadSkillShopTable(BeachTownMapId, table));
    }
}
