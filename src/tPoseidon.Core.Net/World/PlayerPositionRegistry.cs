using System.Collections.Concurrent;

namespace tPoseidon.Core.Net.World;

public sealed class PlayerPositionRegistry
{
    private readonly ConcurrentDictionary<ushort, PlayerPosition> _byEntityId = new();

    public void Upsert(ushort entityId, ushort mapId, ushort x, ushort y)
    {
        _byEntityId[entityId] = new PlayerPosition
        {
            EntityId = entityId,
            MapId = mapId,
            X = x,
            Y = y,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }

    public bool Remove(ushort entityId) => _byEntityId.TryRemove(entityId, out _);

    public IReadOnlyCollection<PlayerPosition> All => _byEntityId.Values.ToArray();

    public IEnumerable<PlayerPosition> OnMap(ushort mapId)
        => _byEntityId.Values.Where(p => p.MapId == mapId);

    public PlayerPosition? Get(ushort entityId)
        => _byEntityId.TryGetValue(entityId, out var v) ? v : null;

    public int Count => _byEntityId.Count;
}

public sealed record PlayerPosition
{
    public required ushort EntityId { get; init; }
    public required ushort MapId { get; init; }
    public required ushort X { get; init; }
    public required ushort Y { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}
