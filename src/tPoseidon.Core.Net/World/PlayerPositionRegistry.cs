using System.Collections.Concurrent;

namespace tPoseidon.Core.Net.World;

public sealed class PlayerPositionRegistry
{
    private readonly ConcurrentDictionary<ushort, PlayerPosition> _byEntityId = new();

    public IEnumerable<PlayerPosition> OnMap(ushort mapId)
        => _byEntityId.Values.Where(p => p.MapId == mapId);

}

public sealed record PlayerPosition
{
    public required ushort EntityId { get; init; }
    public required ushort MapId { get; init; }
    public required ushort X { get; init; }
    public required ushort Y { get; init; }
}
