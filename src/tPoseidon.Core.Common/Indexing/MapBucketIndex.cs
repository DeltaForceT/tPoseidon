namespace tPoseidon.Core.Common.Indexing;

public sealed class MapBucketIndex<TEntity>
    where TEntity : class
{
    private readonly object _gate = new();
    private readonly Func<TEntity, ushort> _idOf;
    private readonly Func<TEntity, ushort> _mapOf;
    private readonly Func<TEntity, int> _typeOf;
    private readonly Dictionary<ushort, TEntity> _byId = new();
    private readonly Dictionary<ushort, SortedDictionary<ushort, TEntity>> _byMap = new();

    public MapBucketIndex(Func<TEntity, ushort> idOf, Func<TEntity, ushort> mapOf, Func<TEntity, int> typeOf)
    {
        _idOf = idOf ?? throw new ArgumentNullException(nameof(idOf));
        _mapOf = mapOf ?? throw new ArgumentNullException(nameof(mapOf));
        _typeOf = typeOf ?? throw new ArgumentNullException(nameof(typeOf));
    }

    public void Install(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        lock (_gate)
        {
            var id = _idOf(entity);
            if (_byId.ContainsKey(id)) throw new InvalidOperationException($"Entity 0x{id:X4} already installed.");
            _byId[id] = entity;
            var map = _mapOf(entity);
            if (!_byMap.TryGetValue(map, out var bucket)) _byMap[map] = bucket = new SortedDictionary<ushort, TEntity>();
            bucket[id] = entity;
        }
    }

    public bool Remove(ushort id)
    {
        lock (_gate)
        {
            if (!_byId.Remove(id, out var entity)) return false;
            var map = _mapOf(entity);
            if (_byMap.TryGetValue(map, out var bucket))
            {
                bucket.Remove(id);
                if (bucket.Count == 0) _byMap.Remove(map);
            }
            return true;
        }
    }

    public int Count
    {
        get
        {
            lock (_gate) return _byId.Count;
        }
    }

    public TEntity? Get(ushort id)
    {
        lock (_gate) return _byId.TryGetValue(id, out var entity) ? entity : null;
    }

    public IReadOnlyList<TEntity> All()
    {
        lock (_gate) return _byId.Values.OrderBy(entity => _idOf(entity)).ToArray();
    }

    public IReadOnlyList<TEntity> InMap(ushort mapId)
    {
        lock (_gate)
        {
            return _byMap.TryGetValue(mapId, out var bucket) ? bucket.Values.ToArray() : Array.Empty<TEntity>();
        }
    }

    public TEntity? FindByTypeOnMap(int type, ushort mapId)
    {
        lock (_gate)
        {
            if (!_byMap.TryGetValue(mapId, out var bucket)) return null;
            foreach (var entity in bucket.Values)
            {
                if (_typeOf(entity) == type) return entity;
            }
            return null;
        }
    }
}
