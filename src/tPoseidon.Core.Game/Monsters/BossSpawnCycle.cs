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
    private readonly Dictionary<(ushort MapId, ushort InstanceKey), Zone> _zones = new();
    private readonly HashSet<ushort> _held = new();

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
                return _zones.Where(entry => entry.Key.InstanceKey == 0 && !entry.Value.PerInstance).Select(entry => entry.Key.MapId).ToArray();
            }
        }
    }

    public IReadOnlyCollection<ushort> InstancedMapIds
    {
        get
        {
            lock (_gate)
            {
                return _zones.Where(entry => entry.Key.InstanceKey == 0 && entry.Value.PerInstance).Select(entry => entry.Key.MapId).ToArray();
            }
        }
    }

    public void Configure(ushort mapId, IReadOnlyList<BossSpawnTableEntry> rows, ZoneGeometry? geometry, bool refills = true, bool perInstance = false)
    {
        ArgumentNullException.ThrowIfNull(rows);
        lock (_gate)
        {
            _zones[(mapId, 0)] = new Zone
            {
                SpawnPoints = new ZoneSpawnPoints(geometry),
                Slots = rows.Select(row => new Slot(row)).ToArray(),
                Refills = refills,
                PerInstance = perInstance,
            };
        }
    }

    public void Hold(ushort mapId, bool held)
    {
        lock (_gate)
        {
            if (held) _held.Add(mapId);
            else _held.Remove(mapId);
        }
    }

    public IReadOnlyList<MonsterEntity> Minute(ushort mapId, long nowMs, ushort instanceKey = 0)
    {
        var born = new List<MonsterEntity>();
        lock (_gate)
        {
            if (_held.Contains(mapId) || !_zones.TryGetValue((mapId, 0), out var template)) return born;
            if (template.PerInstance != (instanceKey != 0)) return born;
            if (!_zones.TryGetValue((mapId, instanceKey), out var zone))
            {
                zone = template.CopyEmpty();
                _zones[(mapId, instanceKey)] = zone;
            }
            foreach (var slot in zone.Slots)
            {
                var elapsed = slot.Minutes;
                slot.Minutes++;
                if (slot.Row.CheckMinutes > elapsed) continue;
                var missing = slot.Row.Count - slot.Alive;
                if (slot.Spawned && !zone.Refills) continue;
                if (missing < 1 || nowMs - slot.LastSpawnMs <= MinimumSpawnGapMs) continue;
                slot.Minutes = 0;
                slot.LastSpawnMs = nowMs;
                if (_catalog.GetById(slot.Row.MonsterId) is null) continue;
                for (var i = 0; i < missing; i++)
                {
                    var (x, y) = zone.SpawnPoints.Pick(slot.Row.Region, _rng);
                    var monster = _monsters.Spawn(
                        slot.Row.MonsterId, mapId, x, y, spawnPointId: null, explicitEntityId: null, regionId: slot.Row.Region, instanceKey: instanceKey);
                    if (monster is null) continue;
                    slot.Alive++;
                    slot.Spawned = true;
                    born.Add(monster);
                }
            }
        }
        return born;
    }

    public void Reopen(ushort mapId)
    {
        lock (_gate)
        {
            if (!_zones.TryGetValue((mapId, 0), out var zone) || zone.Refills || zone.PerInstance) return;
            foreach (var slot in zone.Slots) slot.Spawned = false;
        }
    }

    public void Release(MonsterEntity monster)
    {
        ArgumentNullException.ThrowIfNull(monster);
        lock (_gate)
        {
            if (!_zones.TryGetValue((monster.MapId, monster.InstanceKey), out var zone)) return;
            foreach (var slot in zone.Slots)
            {
                if (slot.Row.MonsterId != monster.MonsterParamId || slot.Row.Region != monster.RegionId || slot.Alive < 1) continue;
                slot.Alive--;
                return;
            }
        }
    }

    public IReadOnlyCollection<ushort> InstanceKeysOf(ushort mapId)
    {
        lock (_gate)
        {
            return _zones.Keys.Where(key => key.MapId == mapId && key.InstanceKey != 0).Select(key => key.InstanceKey).ToArray();
        }
    }

    public void Drop(ushort mapId, ushort instanceKey)
    {
        if (instanceKey == 0) return;
        lock (_gate)
        {
            _zones.Remove((mapId, instanceKey));
        }
    }

    private sealed class Zone
    {
        public required ZoneSpawnPoints SpawnPoints { get; init; }
        public required Slot[] Slots { get; init; }
        public bool Refills { get; init; } = true;
        public bool PerInstance { get; init; }

        public Zone CopyEmpty() => new()
        {
            SpawnPoints = SpawnPoints,
            Slots = Slots.Select(slot => new Slot(slot.Row)).ToArray(),
            Refills = Refills,
            PerInstance = PerInstance,
        };
    }

    private sealed class Slot
    {
        public Slot(BossSpawnTableEntry row) => Row = row;

        public BossSpawnTableEntry Row { get; }
        public int Minutes { get; set; }
        public int Alive { get; set; }
        public bool Spawned { get; set; }
        public long LastSpawnMs { get; set; } = long.MinValue / 2;
    }
}
