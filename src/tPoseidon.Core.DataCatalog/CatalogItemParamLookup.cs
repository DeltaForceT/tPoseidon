using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.DataCatalog;

public sealed class CatalogItemParamLookup : IItemParamLookup
{
    private readonly ItemCatalog _items;

    public CatalogItemParamLookup(ItemCatalog items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = items;
        Count = items.DistinctIds;
    }

    public int Count { get; }

    public ItemParamEntry? GetById(int itemId) => _items.Get(itemId)?.Entry;
}
