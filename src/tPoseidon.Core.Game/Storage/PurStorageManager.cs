using System.Collections.Concurrent;

namespace tPoseidon.Core.Game.Storage;

public sealed class PurStorageManager
{
    private readonly ConcurrentDictionary<ulong, PurStorage> _byOwner = new();

    public PurStorage GetOrCreate(ulong ownerCharId)
    {
        return _byOwner.GetOrAdd(ownerCharId, owner => new PurStorage
        {
            OwnerCharId = owner,
            CreatedAt = DateTimeOffset.UtcNow,
        });
    }

    public int StorageCount => _byOwner.Count;
}

public sealed class PurStorage
{
    private readonly Dictionary<int, ulong> _slots = new();

    public required ulong OwnerCharId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }

    public IReadOnlyDictionary<int, ulong> Slots
    {
        get { lock (_slots) return new Dictionary<int, ulong>(_slots); }
    }

    public bool TryStore(int slot, ulong itemUid)
    {
        lock (_slots) return _slots.TryAdd(slot, itemUid);
    }

    public ulong? TryWithdraw(int slot)
    {
        lock (_slots)
        {
            if (_slots.TryGetValue(slot, out var uid))
            {
                _slots.Remove(slot);
                return uid;
            }
            return null;
        }
    }
}
