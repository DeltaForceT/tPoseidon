using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Monsters;

public sealed record MonsterRegenRow(int MonsterId, int Region, int Average, int Max, float Ratio);

public sealed class MonsterRegenCycle
{
    public const int FirstFieldMonsterId = 2000;
    public const int LastFieldMonsterId = 3434;
    public const int FirstSpecialMonsterId = 16000;
    public const int LastSpecialMonsterId = 16998;

    private readonly MonsterManager _monsters;
    private readonly IMonsterParamLookup _catalog;
    private readonly Random _rng;
    private readonly object _gate = new();
    private readonly Dictionary<ushort, Zone> _zones = new();

    public MonsterRegenCycle(MonsterManager monsters, IMonsterParamLookup catalog, Random? rng = null)
    {
        ArgumentNullException.ThrowIfNull(monsters);
        ArgumentNullException.ThrowIfNull(catalog);
        _monsters = monsters;
        _catalog = catalog;
        _rng = rng ?? new Random();
    }

    public IReadOnlyCollection<ushort> MapIds
    {
        get
        {
            lock (_gate)
            {
                return _zones.Keys.ToArray();
            }
        }
    }

    public static bool IsSpawnableId(int monsterId) =>
        (monsterId >= FirstFieldMonsterId && monsterId <= LastFieldMonsterId)
        || (monsterId >= FirstSpecialMonsterId && monsterId <= LastSpecialMonsterId);

    public void Configure(ushort mapId, int mapCap, IReadOnlyList<MonsterRegenRow> rows, ZoneGeometry? geometry)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var ratioSum = 0f;
        foreach (var row in rows) ratioSum += row.Ratio;

        lock (_gate)
        {
            _zones[mapId] = new Zone
            {
                Cap = mapCap,
                Rows = rows.ToArray(),
                RatioSum = ratioSum,
                SpawnPoints = new ZoneSpawnPoints(geometry),
            };
        }
    }

    public IReadOnlyList<MonsterEntity> Pass(ushort mapId)
    {
        var born = new List<MonsterEntity>();
        lock (_gate)
        {
            if (!_zones.TryGetValue(mapId, out var zone)) return born;
            if (zone.Alive >= zone.Cap) return born;

            foreach (var row in zone.Rows)
            {
                var key = (row.MonsterId, row.Region);
                zone.Counts.TryGetValue(key, out var count);
                if (count >= row.Max) continue;
                if (row.Average < 1)
                {
                    if (row.Ratio <= 0 || zone.RatioSum <= 0) continue;
                    var share = (int)(zone.Cap * row.Ratio / zone.RatioSum);
                    if (count >= share) continue;
                }
                else if (count >= row.Average)
                {
                    continue;
                }

                if (!IsSpawnableId(row.MonsterId)) continue;
                var definition = _catalog.GetById(row.MonsterId);
                if (definition is null || definition.Type == (int)MonsterAType.BossMonster) continue;

                var (x, y) = zone.SpawnPoints.Pick(row.Region, _rng);
                var monster = _monsters.Spawn(
                    row.MonsterId, mapId, x, y, spawnPointId: null, explicitEntityId: null, regionId: row.Region);
                if (monster is null) continue;

                zone.Counts[key] = count + 1;
                zone.Alive++;
                born.Add(monster);
            }
        }
        return born;
    }

    public void Release(MonsterEntity monster)
    {
        ArgumentNullException.ThrowIfNull(monster);
        lock (_gate)
        {
            if (!_zones.TryGetValue(monster.MapId, out var zone)) return;
            var key = (monster.MonsterParamId, monster.RegionId);
            if (!zone.Counts.TryGetValue(key, out var count) || count < 1) return;
            zone.Counts[key] = count - 1;
            zone.Alive--;
        }
    }

    private sealed class Zone
    {
        public required int Cap { get; init; }
        public required MonsterRegenRow[] Rows { get; init; }
        public required float RatioSum { get; init; }
        public required ZoneSpawnPoints SpawnPoints { get; init; }
        public Dictionary<(int MonsterId, int Region), int> Counts { get; } = new();
        public int Alive { get; set; }
    }
}
