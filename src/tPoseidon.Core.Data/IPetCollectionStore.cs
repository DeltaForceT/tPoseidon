namespace tPoseidon.Core.Data;

public sealed record PetCollectedItem(ulong CollectedUid, int ItemId, byte ItemClass, ushort Count);

public interface IPetCollectionStore
{
    IReadOnlyList<PetCollectedItem> Load(ulong characterId);

    PetCollectedItem Add(ulong characterId, int itemId, byte itemClass, ushort count);

    bool Remove(ulong characterId, ulong collectedUid);
}

public sealed class InMemoryPetCollectionStore : IPetCollectionStore
{
    private readonly object _gate = new();
    private readonly Dictionary<ulong, List<PetCollectedItem>> _bags = new();
    private ulong _lastUid;

    public IReadOnlyList<PetCollectedItem> Load(ulong characterId)
    {
        lock (_gate)
        {
            return _bags.TryGetValue(characterId, out var bag) ? bag.ToArray() : Array.Empty<PetCollectedItem>();
        }
    }

    public PetCollectedItem Add(ulong characterId, int itemId, byte itemClass, ushort count)
    {
        lock (_gate)
        {
            if (!_bags.TryGetValue(characterId, out var bag))
            {
                bag = new List<PetCollectedItem>();
                _bags[characterId] = bag;
            }
            var item = new PetCollectedItem(++_lastUid, itemId, itemClass, count);
            bag.Add(item);
            return item;
        }
    }

    public bool Remove(ulong characterId, ulong collectedUid)
    {
        lock (_gate)
        {
            return _bags.TryGetValue(characterId, out var bag) && bag.RemoveAll(item => item.CollectedUid == collectedUid) > 0;
        }
    }
}
