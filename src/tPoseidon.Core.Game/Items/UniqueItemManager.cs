using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Items;

public sealed class UniqueItemManager
{
    private readonly HashSet<int> _legendItemIds;

    public UniqueItemManager(IEnumerable<LegendItemListEntry> legendItems)
    {
        ArgumentNullException.ThrowIfNull(legendItems);
        _legendItemIds = new HashSet<int>(legendItems.Select(e => e.ItemId));
    }

    public int LegendItemCount => _legendItemIds.Count;
}
