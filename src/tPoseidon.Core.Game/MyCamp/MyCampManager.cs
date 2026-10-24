using System.Collections.Concurrent;

namespace tPoseidon.Core.Game.MyCamp;

public sealed class MyCampManager
{
    private readonly ConcurrentDictionary<ulong, MyCamp> _byOwner = new();

    public MyCamp? CreateMyCamp(ulong ownerCharId)
    {
        var camp = new MyCamp
        {
            OwnerCharId = ownerCharId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        return _byOwner.TryAdd(ownerCharId, camp) ? camp : null;
    }

    public MyCamp? Get(ulong ownerCharId)
        => _byOwner.TryGetValue(ownerCharId, out var v) ? v : null;

    public bool Remove(ulong ownerCharId) => _byOwner.TryRemove(ownerCharId, out _);

    public int CampCount => _byOwner.Count;
}

public sealed record MyCamp
{
    public required ulong OwnerCharId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public string Name { get; init; } = string.Empty;
}
