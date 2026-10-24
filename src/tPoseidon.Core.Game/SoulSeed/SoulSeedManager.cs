using System.Collections.Concurrent;

namespace tPoseidon.Core.Game.SoulSeed;

public sealed class SoulSeedManager
{
    private readonly ConcurrentDictionary<ulong, SoulSeedSlot> _byCharId = new();

    public SoulSeedSlot StoreOnDeath(ulong charId, int spStored)
    {
        var slot = new SoulSeedSlot
        {
            CharId = charId,
            StoredSp = spStored,
            StoredAt = DateTimeOffset.UtcNow,
        };
        _byCharId[charId] = slot;
        return slot;
    }

    public SoulSeedSlot? Get(ulong charId)
        => _byCharId.TryGetValue(charId, out var v) ? v : null;

    public int? Restore(ulong charId)
    {
        if (_byCharId.TryRemove(charId, out var v)) return v.StoredSp;
        return null;
    }

    public int SlotCount => _byCharId.Count;
}

public sealed record SoulSeedSlot
{
    public required ulong CharId { get; init; }
    public required int StoredSp { get; init; }
    public required DateTimeOffset StoredAt { get; init; }
}
