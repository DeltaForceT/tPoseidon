using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Monsters;

internal sealed class EscortZones
{
    private readonly object _gate = new();
    private readonly Dictionary<ushort, IReadOnlyList<(int MonsterId, int Region)>> _rowsByMap = new();
    private readonly Dictionary<(ushort Map, ushort Key), Scene> _scenes = new();

    public void Register(ushort mapId, IReadOnlyList<(int MonsterId, int Region)> rows)
    {
        lock (_gate)
        {
            if (rows.Count > 0) _rowsByMap[mapId] = rows;
        }
    }

    public bool Has(ushort mapId)
    {
        lock (_gate)
        {
            return _rowsByMap.ContainsKey(mapId);
        }
    }

    public IReadOnlyList<MonsterEntity> Enter(ushort mapId, ushort key, MonsterManager monsters, ZoneSpawnPoints spawnPoints, Random rng)
    {
        lock (_gate)
        {
            if (!_rowsByMap.TryGetValue(mapId, out var rows) || _scenes.ContainsKey((mapId, key))) return Array.Empty<MonsterEntity>();
            var scene = new Scene();
            _scenes[(mapId, key)] = scene;
            var born = new List<MonsterEntity>();
            foreach (var (monsterId, region) in rows)
            {
                var (x, y) = spawnPoints.Pick(region, rng);
                var monster = monsters.Spawn(monsterId, mapId, x, y, spawnPointId: null, explicitEntityId: null, regionId: region, instanceKey: key);
                if (monster is null) continue;
                scene.EntityIds.Add(monster.EntityId);
                born.Add(monster);
            }
            return born;
        }
    }

    public bool Resolve(ushort mapId, ushort key)
    {
        lock (_gate)
        {
            if (!_scenes.TryGetValue((mapId, key), out var scene) || scene.Resolved) return false;
            scene.Resolved = true;
            return true;
        }
    }

    public IReadOnlyList<ushort> Prune(IReadOnlySet<(ushort Map, ushort Key)> occupied)
    {
        lock (_gate)
        {
            var left = new List<ushort>();
            foreach (var key in _scenes.Keys.Where(scene => !occupied.Contains(scene)).ToArray())
            {
                left.AddRange(_scenes[key].EntityIds);
                _scenes.Remove(key);
            }
            return left;
        }
    }

    private sealed class Scene
    {
        public bool Resolved;
        public List<ushort> EntityIds { get; } = new();
    }
}
