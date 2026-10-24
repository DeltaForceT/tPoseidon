using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Net.World;

public sealed class ZoneManager
{
    private readonly IMapInfoLookup _mapInfo;
    private readonly IReadOnlyList<IZoneCreator> _mapCreators;
    private readonly IReadOnlyList<IZoneEntityCreator> _entityCreators;
    private readonly ZoneInitializer? _initializer;
    private readonly Action<string>? _log;

    private readonly Dictionary<ushort, Zone> _zones = new();

    public ZoneManager(
        IMapInfoLookup mapInfo,
        IReadOnlyList<IZoneCreator> mapCreators,
        IReadOnlyList<IZoneEntityCreator> entityCreators,
        ZoneInitializer? initializer = null,
        Action<string>? log = null)
    {
        _mapInfo = mapInfo ?? throw new ArgumentNullException(nameof(mapInfo));
        _mapCreators = mapCreators ?? throw new ArgumentNullException(nameof(mapCreators));
        _entityCreators = entityCreators ?? throw new ArgumentNullException(nameof(entityCreators));
        _initializer = initializer;
        _log = log;
    }

    public IReadOnlyCollection<Zone> All => _zones.Values;

    public int Count => _zones.Count;

    public void BuildWorld()
    {
        var built = 0;
        foreach (var entry in _mapInfo.All)
        {
            var zone = CreateZone(entry);

            if (_initializer is not null)
            {
                try { _initializer.Initialize(zone); }
                catch (Exception ex)
                {
                    _log?.Invoke($"BuildWorld: Initialize failed for map {entry.Id} ({entry.Name}): {ex.GetType().Name}: {ex.Message}");
                }
            }

            foreach (var creator in _mapCreators)
            {
                try { creator.Build(zone); }
                catch (Exception ex)
                {
                    _log?.Invoke($"BuildWorld: creator {creator.GetType().Name} failed for map {entry.Id} ({entry.Name}): {ex.GetType().Name}: {ex.Message}");
                }
            }
            _zones[entry.Id] = zone;
            built++;
        }
        _log?.Invoke($"BuildWorld: {built} zones loaded");

        BuildWorldNpcPostPass();
    }

    private void BuildWorldNpcPostPass()
    {
        if (_initializer is null) return;
        var totalNpcs = 0;
        var mapsWithNpcs = 0;
        foreach (var zone in _zones.Values)
        {
            if (zone.NpcMgr is null) continue;
            try
            {
                var created = zone.NpcMgr.CreateAllNpcs(zone.Name);
                if (created.Count > 0)
                {
                    totalNpcs += created.Count;
                    mapsWithNpcs++;
                }
            }
            catch (Exception ex)
            {
                _log?.Invoke($"BuildWorld NPC post-pass: failed for map {zone.MapId} ({zone.Name}): {ex.GetType().Name}: {ex.Message}");
            }
        }
        _log?.Invoke($"BuildWorld NPC post-pass: {totalNpcs} NPCs created across {mapsWithNpcs} maps");
    }

    private static Zone CreateZone(MapInfoEntry entry)
    {
        if (entry.IsDungeon) return new DungeonZone { MapId = entry.Id, MapInfo = entry };
        if (entry.IsMyCamp) return new MyCampZone { MapId = entry.Id, MapInfo = entry };
        if (entry.IsEvent) return new EventZone { MapId = entry.Id, MapInfo = entry };
        return entry.Group switch
        {
            MapGroup.World => new WorldZone { MapId = entry.Id, MapInfo = entry },
            MapGroup.Tutorial => new TutorialZone { MapId = entry.Id, MapInfo = entry },
            MapGroup.GuildGame => new GuildGameZone { MapId = entry.Id, MapInfo = entry },
            MapGroup.ParallelMonsterQuest => new ParallelMonsterQuestZone { MapId = entry.Id, MapInfo = entry },
            MapGroup.ParallelParty => new ParallelPartyZone { MapId = entry.Id, MapInfo = entry },
            MapGroup.WeddingHall => new WeddingHallZone { MapId = entry.Id, MapInfo = entry },
            _ => new GenericParallelZone { MapId = entry.Id, MapInfo = entry },
        };
    }

    public void SpawnAll()
    {
        var totalNpc = 0;
        var totalMob = 0;
        var npcMaps = 0;
        var mobMaps = 0;

        foreach (var zone in _zones.Values)
        {
            foreach (var creator in _entityCreators)
            {
                int spawned;
                try { spawned = creator.Spawn(zone); }
                catch (Exception ex)
                {
                    _log?.Invoke($"SpawnAll: creator {creator.GetType().Name} failed for map {zone.MapId} ({zone.Name}): {ex.GetType().Name}: {ex.Message}");
                    continue;
                }

                if (creator is NpcCreator)
                {
                    totalNpc += spawned;
                    if (spawned > 0) npcMaps++;
                }
                else if (creator is MonsterCreator)
                {
                    totalMob += spawned;
                    if (spawned > 0) mobMaps++;
                }
            }
        }

        _log?.Invoke($"SpawnAll: NPCs preloaded {totalNpc} across {npcMaps} maps");
        _log?.Invoke($"SpawnAll: Monsters preloaded {totalMob} across {mobMaps} maps");
    }

    public int RespawnAllMonsters(Action<int>? clearAllMonsters = null)
    {
        clearAllMonsters?.Invoke(0);

        var total = 0;
        foreach (var zone in _zones.Values)
        {
            zone.MonsterSpawnCount = 0;
            foreach (var creator in _entityCreators)
            {
                if (creator is not MonsterCreator) continue;
                try { total += creator.Spawn(zone); }
                catch (Exception ex)
                {
                    _log?.Invoke($"RespawnAllMonsters: failed for map {zone.MapId}: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }
        _log?.Invoke($"RespawnAllMonsters: {total} monsters respawned across all zones");
        return total;
    }

}
