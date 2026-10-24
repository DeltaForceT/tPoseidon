using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Combat;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.World;
using Xunit;

namespace tPoseidon.Core.Net.Tests.World;

public class ZoneInitializerTests
{
    private sealed class StubItemStore : IItemStore
    {
        public Item GrantToCharacter(ulong characterId, int itemIndex, byte itemClass, byte slotKind, byte slotPosition, ushort stack)
            => throw new NotImplementedException();
        public IReadOnlyList<Item> GetAllForCharacter(ulong characterId) => Array.Empty<Item>();
        public IReadOnlyList<Item> GetForCharacterBySlotKind(ulong characterId, byte slotKind) => Array.Empty<Item>();
        public Item? FindByUid(ulong itemUid) => null;
        public Item? FindBySlot(ulong characterId, byte slotKind, byte slotPosition) => null;
        public bool MoveToSlot(ulong itemUid, byte newSlotKind, byte newSlotPosition) => false;
        public bool UnassignFromCharacter(ulong itemUid) => false;
        public bool AssignToCharacter(ulong itemUid, ulong characterId, byte slotKind, byte slotPosition) => false;
        public bool Delete(ulong itemUid) => false;
        public ushort AdjustStack(ulong itemUid, int delta) => 0;
        public bool SetStack(ulong itemUid, ushort stack) => false;
    }

    private sealed class StubItemParamLookup : IItemParamLookup
    {
        public int Count => 0;
        public ItemParamEntry? GetById(int itemId) => null;
    }

    private sealed class StubMonsterParamLookup : IMonsterParamLookup
    {
        public int Count => 0;
        public MonsterParamEntry? GetById(int id) => null;
        public IReadOnlyCollection<MonsterParamEntry> All => Array.Empty<MonsterParamEntry>();
    }

    private sealed class StubNpcTalkLookup : INpcTalkLookup
    {
        public int Count => 0;
        public IReadOnlyList<NpcTalkEntry> GetAllByMap(string mapName) => Array.Empty<NpcTalkEntry>();
        public NpcTalkEntry? GetByTypeOnMap(int npcType, string mapName) => null;
        public NpcTalkEntry? GetByTypeAnyMap(int npcType) => null;
    }

    private sealed class StubNpcPointProvider : INpcPointProvider
    {
        public bool TryGet(ushort mapId, ushort posId, out NpcPoint point)
        {
            point = NpcPoint.Origin();
            return false;
        }
    }

    [Fact]
    public void Initialize_PopulatesEveryPerZoneMgr()
    {
        var entry = new MapInfoEntry
        {
            Id = 148,
            Name = "Coral_Town",
            Md3FileName = "coral_town.md3",
            Group = MapGroup.World,
        };
        var zone = new WorldZone { MapId = entry.Id, MapInfo = entry };

        var ids = new IdAllocator();
        var monsterMgr = new MonsterManager(new StubMonsterParamLookup(), ids);
        var itemMgr = new ItemManager(new StubItemStore(), new StubItemParamLookup());
        var world = new WorldEntityStore();
        var npcMgr = new NpcManager(new StubNpcTalkLookup(), new StubNpcPointProvider(), world, ids);
        var actionMgr = new ActionManager();

        var initializer = new ZoneInitializer(itemMgr, monsterMgr, npcMgr, actionMgr);
        initializer.Initialize(zone);

        Assert.True(zone.IsInitialized);
        Assert.NotNull(zone.Stage);
        Assert.NotNull(zone.ShellMsgCaster);
        Assert.NotNull(zone.ZoneMapAttr);
        Assert.NotNull(zone.PathFinder);
        Assert.NotNull(zone.ItemMgr);
        Assert.NotNull(zone.MonsterMgr);
        Assert.NotNull(zone.NpcMgr);
        Assert.NotNull(zone.ShopMgr);
        Assert.NotNull(zone.SkillShopMgr);
        Assert.NotNull(zone.ActionMgr);
        Assert.NotNull(zone.TrapMgr);
        Assert.NotNull(zone.SystemAgency);

        Assert.True(zone.ItemMgr!.IsInitialized);
        Assert.True(zone.MonsterMgr!.IsInitialized);
        Assert.True(zone.NpcMgr!.IsInitialized);
        Assert.True(zone.ShopMgr!.IsInitialized);
        Assert.True(zone.SkillShopMgr!.IsInitialized);
        Assert.True(zone.ActionMgr!.IsInitialized);
        Assert.True(zone.TrapMgr!.IsInitialized);
        Assert.True(zone.SystemAgency!.IsInitialized);

        Assert.True(zone.ShopMgr.IsCreated);
        Assert.True(zone.SkillShopMgr.IsCreated);
        Assert.True(zone.Stage!.IsLoaded);
    }

    [Fact]
    public void Initialize_IdempotentForSameZone()
    {
        var entry = new MapInfoEntry
        {
            Id = 148,
            Name = "Coral_Town",
            Md3FileName = "coral_town.md3",
            Group = MapGroup.World,
        };
        var zone = new WorldZone { MapId = entry.Id, MapInfo = entry };

        var ids = new IdAllocator();
        var monsterMgr = new MonsterManager(new StubMonsterParamLookup(), ids);
        var itemMgr = new ItemManager(new StubItemStore(), new StubItemParamLookup());
        var world = new WorldEntityStore();
        var npcMgr = new NpcManager(new StubNpcTalkLookup(), new StubNpcPointProvider(), world, ids);
        var actionMgr = new ActionManager();

        var initializer = new ZoneInitializer(itemMgr, monsterMgr, npcMgr, actionMgr);
        initializer.Initialize(zone);
        var firstStage = zone.Stage;

        initializer.Initialize(zone);
        Assert.Same(firstStage, zone.Stage);
    }
}
