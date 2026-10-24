using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Net.World;

public sealed class ZoneMonsterMgr
{
    private readonly Zone _zone;
    private readonly MonsterManager _global;
    private readonly IRegionInfoLookup? _regions;
    private readonly IMonZoneLookup? _zonePool;
    private readonly Random _rng;

    public ZoneMonsterMgr(
        Zone zone,
        MonsterManager global,
        IRegionInfoLookup? regions = null,
        IMonZoneLookup? zonePool = null,
        int rngSeed = 42)
    {
        _zone = zone ?? throw new ArgumentNullException(nameof(zone));
        _global = global ?? throw new ArgumentNullException(nameof(global));
        _regions = regions;
        _zonePool = zonePool;
        _rng = new Random(rngSeed);
    }

    public Zone Zone => _zone;
    public MonsterManager Global => _global;

    public bool IsInitialized { get; private set; }
    public bool IsActivated { get; private set; }

    public void Init()
    {
        IsInitialized = true;
    }

    public IReadOnlyCollection<MonsterEntity> All => _global.GetByMap(_zone.MapId);

    public MonsterEntity? Spawn(int monsterParamId, ushort x, ushort y, ushort? spawnPointId = null)
        => _global.Spawn(monsterParamId, _zone.MapId, x, y, spawnPointId);

    public int SpawnRegion(int monsterParamId, int regionId, int avr)
    {
        if (avr <= 0) return 0;
        if (_regions is null) return 0;

        var region = _regions.GetByMapAndRegion(_zone.Name, regionId);
        if (region is null) return 0;

        var spawned = 0;
        for (var i = 0; i < avr; i++)
        {
            var x = (ushort)Math.Clamp(_rng.Next(region.LTcoordX, region.RBcoordX + 1), 0, ushort.MaxValue);
            var y = (ushort)Math.Clamp(_rng.Next(region.LTcoordY, region.RBcoordY + 1), 0, ushort.MaxValue);
            if (Spawn(monsterParamId, x, y) is not null) spawned++;
        }
        _zone.MonsterSpawnCount += spawned;
        return spawned;
    }

    public int SpawnAll()
    {
        if (_zonePool is not null && _regions is not null)
        {
            return SpawnAllFromPool();
        }
        return SpawnAllFromDefinitions();
    }

    private int SpawnAllFromPool()
    {
        var rows = _zonePool!.GetByMap(_zone.Name);
        var spawned = 0;
        foreach (var row in rows)
        {
            spawned += SpawnRegion(row.MonsterId, row.Region, row.Avr);
        }
        return spawned;
    }

    private int SpawnAllFromDefinitions()
    {
        var spawned = 0;
        foreach (var def in _zone.MonsterSpawnDefinitions)
        {
            if (Spawn(def.MonsterParamId, def.X, def.Y, def.SpawnPointId) is not null)
            {
                spawned++;
            }
        }
        _zone.MonsterSpawnCount = spawned;
        return spawned;
    }

    public int Activate()
    {
        if (IsActivated) return 0;
        IsActivated = true;
        return SpawnAll();
    }
}
