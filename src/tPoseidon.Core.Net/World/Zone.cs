using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Net.World;

public abstract class Zone
{
    public required ushort MapId { get; init; }
    public required MapInfoEntry MapInfo { get; init; }

    public string Name => MapInfo.Name;
    public string Md3FileName => MapInfo.Md3FileName;

    public IReadOnlyList<PortalEntrance> Portals { get; internal set; } = Array.Empty<PortalEntrance>();

    public int NpcSpawnCount { get; internal set; }
    public int MonsterSpawnCount { get; internal set; }

    public abstract ZoneKind Kind { get; }

    public bool IsActivated { get; private set; }
    private readonly object _activateLock = new();

    public bool TryActivateOnce()
    {
        lock (_activateLock)
        {
            if (IsActivated) return false;
            IsActivated = true;
            return true;
        }
    }

    public List<MonsterSpawnDefinition> MonsterSpawnDefinitions { get; } = new();
    public List<NpcSpawnDefinition> NpcSpawnDefinitions { get; } = new();

    public Stage? Stage { get; internal set; }
    public ShellMsgCaster? ShellMsgCaster { get; internal set; }
    public ZoneMapAttr? ZoneMapAttr { get; internal set; }
    public PathFinder? PathFinder { get; internal set; }

    public ZoneItemMgr? ItemMgr { get; internal set; }
    public ZoneMonsterMgr? MonsterMgr { get; internal set; }
    public ZoneNpcMgr? NpcMgr { get; internal set; }
    public ZoneShopMgr? ShopMgr { get; internal set; }
    public ZoneSkillShopMgr? SkillShopMgr { get; internal set; }
    public ZoneActionMgr? ActionMgr { get; internal set; }
    public ZoneTrapMgr? TrapMgr { get; internal set; }
    public ZoneSystemAgency? SystemAgency { get; internal set; }

    public bool IsInitialized { get; internal set; }
}

public sealed record MonsterSpawnDefinition
{
    public required int MonsterParamId { get; init; }
    public required ushort X { get; init; }
    public required ushort Y { get; init; }
    public ushort SpawnPointId { get; init; }
    public int RespawnSeconds { get; init; }
}

public sealed record NpcSpawnDefinition
{
    public required int NpcParamId { get; init; }
    public required ushort X { get; init; }
    public required ushort Y { get; init; }
    public string TableName { get; init; } = string.Empty;
}

public enum ZoneKind
{
    World,
    Parallel,
    Dungeon,
    MyCamp,
    Event,
}

public sealed class WorldZone : Zone
{
    public override ZoneKind Kind => ZoneKind.World;
}

public abstract class ParallelZone : Zone
{
    public override ZoneKind Kind => ZoneKind.Parallel;
    public abstract MapGroup Group { get; }
}

public sealed class TutorialZone : ParallelZone
{
    public override MapGroup Group => MapGroup.Tutorial;
}

public sealed class GuildGameZone : ParallelZone
{
    public override MapGroup Group => MapGroup.GuildGame;
}

public sealed class ParallelMonsterQuestZone : ParallelZone
{
    public override MapGroup Group => MapGroup.ParallelMonsterQuest;
}

public sealed class ParallelPartyZone : ParallelZone
{
    public override MapGroup Group => MapGroup.ParallelParty;
}

public sealed class WeddingHallZone : ParallelZone
{
    public override MapGroup Group => MapGroup.WeddingHall;
}

public sealed class GenericParallelZone : ParallelZone
{
    public override MapGroup Group => MapGroup.Parallel;
}

public sealed class DungeonZone : Zone
{
    public override ZoneKind Kind => ZoneKind.Dungeon;
}

public sealed class MyCampZone : Zone
{
    public override ZoneKind Kind => ZoneKind.MyCamp;
}

public sealed class EventZone : Zone
{
    public override ZoneKind Kind => ZoneKind.Event;
}
