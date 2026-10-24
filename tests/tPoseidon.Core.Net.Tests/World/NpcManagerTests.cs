using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public class NpcManagerTests
{
    private const ushort CoralTownMapId = 148;
    private const string CoralTownLibName = "Coral_Town";

    private static NpcTalkEntry HeidiRow() => new()
    {
        Type = 1311,
        PosId = 2,
        MapName = CoralTownLibName,
        MsgTable = "NpcMsg_EP0_Npc312",
        QuestId0 = 1233,
    };

    private static NpcTalkEntry BunnyMaidRow() => new()
    {
        Type = 1011,
        PosId = 5,
        MapName = CoralTownLibName,
        MsgTable = "NpcMsg_EP0_Npc12",
    };

    private static (NpcManager mgr, WorldEntityStore world, IdAllocator ids) Build(
        IEnumerable<NpcTalkEntry> rows,
        IEnumerable<((ushort, ushort) Key, NpcPoint Point)>? points = null)
    {
        var npcTalk = new InMemoryNpcTalkLookup(rows);
        var pointProvider = new InMemoryNpcPointProvider(
            points ?? Array.Empty<((ushort, ushort), NpcPoint)>());
        var world = new WorldEntityStore();
        var ids = new IdAllocator();
        var mgr = new NpcManager(npcTalk, pointProvider, world, ids);
        return (mgr, world, ids);
    }

    [Fact]
    public void IsValidChrType_RegularRange()
    {
        Assert.True(NpcManager.IsValidChrType(1000));
        Assert.True(NpcManager.IsValidChrType(1311));
        Assert.True(NpcManager.IsValidChrType(1499));
        Assert.False(NpcManager.IsValidChrType(999));
        Assert.False(NpcManager.IsValidChrType(1500));
    }

    [Fact]
    public void IsValidChrType_TimerRange()
    {
        Assert.True(NpcManager.IsValidChrType(17000));
        Assert.True(NpcManager.IsValidChrType(17500));
        Assert.True(NpcManager.IsValidChrType(17998));
        Assert.False(NpcManager.IsValidChrType(16999));
        Assert.False(NpcManager.IsValidChrType(17999));
    }

    [Fact]
    public void CreateNpc_ValidRow_InstallsInWorld()
    {
        var (mgr, world, _) = Build(
            new[] { HeidiRow() },
            new[] { (((ushort)CoralTownMapId, (ushort)2), NpcPoint.Xy(1062, 2076)) });

        var npc = mgr.CreateNpc(CoralTownMapId, HeidiRow());

        Assert.NotNull(npc);
        Assert.Equal(1311, npc.ChrType);
        Assert.Equal(CoralTownMapId, npc.MapId);
        Assert.Equal((ushort)2, npc.PosId);
        Assert.Equal(1062f, npc.Spawn.X);
        Assert.Equal(2076f, npc.Spawn.Y);
        Assert.Equal(1, world.Count);
        Assert.Equal(npc.EntityId, world.Get(npc.EntityId)!.EntityId);
    }

    [Fact]
    public void CreateNpc_InvalidChrType_ReturnsNullDoesNotInstall()
    {
        var (mgr, world, ids) = Build(Array.Empty<NpcTalkEntry>());
        var inFlightBefore = ids.InFlightCount;
        var bad = HeidiRow() with { Type = 99 };

        var npc = mgr.CreateNpc(CoralTownMapId, bad);

        Assert.Null(npc);
        Assert.Equal(0, world.Count);
        Assert.Equal(inFlightBefore, ids.InFlightCount);
    }

    [Fact]
    public void CreateNpc_NoPointFound_FallsBackToOrigin()
    {
        var (mgr, world, _) = Build(new[] { HeidiRow() });

        var npc = mgr.CreateNpc(CoralTownMapId, HeidiRow());

        Assert.NotNull(npc);
        Assert.Equal(NpcPoint.Origin().X, npc.Spawn.X);
        Assert.Equal(NpcPoint.Origin().Y, npc.Spawn.Y);
        Assert.Equal(1, world.Count);
    }

    [Fact]
    public void PreloadMap_InstallsAllNpcsForMap()
    {
        var (mgr, world, _) = Build(
            new[] { HeidiRow(), BunnyMaidRow() },
            new[]
            {
                (((ushort)CoralTownMapId, (ushort)2), NpcPoint.Xy(1062, 2076)),
                (((ushort)CoralTownMapId, (ushort)5), NpcPoint.Xy(762, 1500)),
            });

        var created = mgr.PreloadMap(CoralTownMapId, CoralTownLibName);

        Assert.Equal(2, created.Count);
        Assert.Equal(2, world.Count);
        Assert.Contains(created, n => n.ChrType == 1311);
        Assert.Contains(created, n => n.ChrType == 1011);
    }

    [Fact]
    public void PreloadMap_EmptyMap_ReturnsEmpty()
    {
        var (mgr, world, _) = Build(Array.Empty<NpcTalkEntry>());
        var created = mgr.PreloadMap(CoralTownMapId, CoralTownLibName);
        Assert.Empty(created);
        Assert.Equal(0, world.Count);
    }

    [Fact]
    public void PreloadMap_AssignsUniqueAscendingEntityIds()
    {
        var (mgr, _, _) = Build(
            new[] { HeidiRow(), BunnyMaidRow() },
            new[]
            {
                (((ushort)CoralTownMapId, (ushort)2), NpcPoint.Xy(1062, 2076)),
                (((ushort)CoralTownMapId, (ushort)5), NpcPoint.Xy(762, 1500)),
            });

        var created = mgr.PreloadMap(CoralTownMapId, CoralTownLibName);

        var ids = created.Select(n => n.EntityId).ToArray();
        Assert.Equal(ids.Length, ids.Distinct().Count());
        Assert.True(ids[1] > ids[0], "Entity ids should be ascending");
    }
}
