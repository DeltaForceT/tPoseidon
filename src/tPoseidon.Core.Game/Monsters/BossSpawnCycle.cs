using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Monsters;

public sealed class BossSpawnCycle
{
    public const int MinimumSpawnGapMs = 20000;

    private readonly MonsterManager _monsters;
    private readonly IMonsterParamLookup _catalog;
    private readonly Random _rng;
    private readonly object _gate = new();
    private readonly Dictionary<ushort, Zone> _zones = new();

    public BossSpawnCycle(MonsterManager monsters, IMonsterParamLookup catalog, Random? rng = null)
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

    public void Configure(ushort mapId, IReadOnlyList<BossSpawnTableEntry> rows, ZoneGeometry? geometry)
    {
        ArgumentNullException.ThrowIfNull(rows);
        lock (_gate)
        {
            _zones[mapId] = new Zone
            {
                SpawnPoints = new ZoneSpawnPoints(geometry),
                Slots = rows.Select(row => new Slot(row)).ToArray(),
            };
        }
    }

    public IReadOnlyList<MonsterEntity> Minute(ushort mapId, long nowMs)
    {
        var born = new List<MonsterEntity>();
        lock (_gate)
        {
            if (!_zones.TryGetValue(mapId, out var zone)) return born;
            foreach (var slot in zone.Slots)
            {
                var elapsed = slot.Minutes;
                slot.Minutes++;
                if (slot.Row.CheckMinutes > elapsed) continue;
                var missing = slot.Row.Count - slot.Alive;
                if (missing < 1 || nowMs - slot.LastSpawnMs <= MinimumSpawnGapMs) continue;
                slot.Minutes = 0;
                slot.LastSpawnMs = nowMs;
                if (_catalog.GetById(slot.Row.MonsterId) is null) continue;
                for (var i = 0; i < missing; i++)
                {
                    var (x, y) = zone.SpawnPoints.Pick(slot.Row.Region, _rng);
                    var monster = _monsters.Spawn(
                        slot.Row.MonsterId, mapId, x, y, spawnPointId: null, explicitEntityId: null, regionId: slot.Row.Region);
                    if (monster is null) continue;
                    slot.Alive++;
                    born.Add(monster);
                }
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
            foreach (var slot in zone.Slots)
            {
                if (slot.Row.MonsterId != monster.MonsterParamId || slot.Row.Region != monster.RegionId || slot.Alive < 1) continue;
                slot.Alive--;
                return;
            }
        }
    }

    private sealed class Zone
    {
        public required ZoneSpawnPoints SpawnPoints { get; init; }
        public required Slot[] Slots { get; init; }
    }

    private sealed class Slot
    {
        public Slot(BossSpawnTableEntry row) => Row = row;

        public BossSpawnTableEntry Row { get; }
        public int Minutes { get; set; }
        public int Alive { get; set; }
        public long LastSpawnMs { get; set; } = long.MinValue / 2;
    }
}
