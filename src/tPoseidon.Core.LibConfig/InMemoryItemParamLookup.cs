namespace tPoseidon.Core.LibConfig;

public sealed class InMemoryItemParamLookup : IItemParamLookup
{
    private readonly Dictionary<int, ItemParamEntry> _byId;

    public InMemoryItemParamLookup(IEnumerable<ItemParamEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _byId = new Dictionary<int, ItemParamEntry>();
        foreach (var entry in entries)
        {
            _byId[entry.Id] = entry;
        }
    }

    public ItemParamEntry? GetById(int itemId) =>
        _byId.TryGetValue(itemId, out var entry) ? entry : null;

    public int Count => _byId.Count;
}
