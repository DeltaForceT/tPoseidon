using tPoseidon.Core.Common;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Net.World;

public sealed class MonsterCreator : IZoneEntityCreator
{
    public const int MaxMonstersPerMapCap = 400;
    public const int MaxDensityMultiplier = 4;

    private readonly MonsterManager _monsterManager;
    private readonly IMonsterSpawnProvider _spawnProvider;
    private readonly IZoneMonsterPoolLookup _zonePools;
    private readonly ZonePoolOverridesProvider _zoneOverrides;
    private readonly SpawnPointOverridesProvider _pointOverrides;
    private readonly Func<int> _densityMultiplierAccessor;

    public MonsterCreator(
        MonsterManager monsterManager,
        IMonsterSpawnProvider spawnProvider,
        IZoneMonsterPoolLookup zonePools,
        ZonePoolOverridesProvider zoneOverrides,
        SpawnPointOverridesProvider pointOverrides,
        Func<int> densityMultiplierAccessor)
    {
        _monsterManager = monsterManager ?? throw new ArgumentNullException(nameof(monsterManager));
        _spawnProvider = spawnProvider ?? throw new ArgumentNullException(nameof(spawnProvider));
        _zonePools = zonePools ?? throw new ArgumentNullException(nameof(zonePools));
        _zoneOverrides = zoneOverrides ?? throw new ArgumentNullException(nameof(zoneOverrides));
        _pointOverrides = pointOverrides ?? throw new ArgumentNullException(nameof(pointOverrides));
        _densityMultiplierAccessor = densityMultiplierAccessor ?? throw new ArgumentNullException(nameof(densityMultiplierAccessor));
    }

    public int Spawn(Zone zone)
    {
        var defs = ComputeSpawnDefinitions(zone).ToList();
        var spawned = 0;
        foreach (var d in defs)
        {
            if (_monsterManager.Spawn(d.MonsterParamId, zone.MapId, d.X, d.Y, (ushort?)d.SpawnPointId) is not null)
            {
                spawned++;
            }
        }
        zone.MonsterSpawnCount = spawned;
        return spawned;
    }

    private IEnumerable<MonsterSpawnDefinition> ComputeSpawnDefinitions(Zone zone)
    {
        var density = Math.Clamp(_densityMultiplierAccessor(), 1, MaxDensityMultiplier);
        var perMapCap = Math.Min(int.MaxValue / 4, MaxMonstersPerMapCap * density);

        List<(int MonsterID, int Avr, int Max)> poolEntries;
        var poolOvr = _zoneOverrides.Current.Get(zone.MapId);
        if (poolOvr is not null && poolOvr.Pool.Count > 0)
        {
            poolEntries = poolOvr.Pool.Select(p => (p.MonsterID, p.Avr, p.Max)).ToList();
        }
        else
        {
            var basePool = _zonePools.GetByMap(zone.MapId);
            if (basePool is null || basePool.Pool.Count == 0) yield break;
            poolEntries = basePool.Pool.Select(p => (p.MonsterID, p.Avr, p.Max)).ToList();
        }

        var basePts = _spawnProvider.GetSpawnPoints(zone.MapId);
        var ptOvr = _pointOverrides.Current.Get(zone.MapId);
        var removed = ptOvr?.Removed is null ? new HashSet<int>() : new HashSet<int>(ptOvr.Removed);
        var pts = new List<(int Id, ushort X, ushort Y)>(basePts.Count + (ptOvr?.Added?.Count ?? 0));
        foreach (var p in basePts)
        {
            if (removed.Contains(p.Id)) continue;
            pts.Add((p.Id, p.X, p.Y));
        }
        if (ptOvr?.Added is { } added)
        {
            foreach (var a in added)
            {
                pts.Add((a.Id,
                    (ushort)Math.Clamp(a.X, 0, ushort.MaxValue),
                    (ushort)Math.Clamp(a.Y, 0, ushort.MaxValue)));
            }
        }
        if (pts.Count == 0) yield break;

        var emitted = 0;
        var ptIdx = 0;
        foreach (var entry in poolEntries)
        {
            var copies = Math.Max(0, entry.Avr) * density;
            for (var i = 0; i < copies; i++)
            {
                if (emitted >= perMapCap) yield break;
                var pt = pts[ptIdx % pts.Count];
                ptIdx++;
                yield return new MonsterSpawnDefinition
                {
                    MonsterParamId = entry.MonsterID,
                    X = pt.X,
                    Y = pt.Y,
                    SpawnPointId = (ushort)pt.Id,
                };
                emitted++;
            }
        }
    }
}
