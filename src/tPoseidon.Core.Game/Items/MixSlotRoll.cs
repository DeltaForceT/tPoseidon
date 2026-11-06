using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Items;

public sealed class MixSlotRoll
{
    private readonly Func<ItemPropertyRules> _rules;
    private readonly IItemRefineStore _slots;
    private readonly IItemParamLookup _catalog;
    private readonly Random _rng = new();

    public MixSlotRoll(Func<ItemPropertyRules> rules, IItemRefineStore slots, IItemParamLookup catalog)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _slots = slots ?? throw new ArgumentNullException(nameof(slots));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public Item Apply(Item item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.ItemClass != ItemParamEntry.RareClass) return item;
        var fixedSlots = Math.Max(item.ItemSlot, _catalog.GetById(item.ItemIndex)?.CompoundSlot ?? 0);
        int rolled;
        lock (_rng) rolled = _rules().MixSlots(item.ItemIndex, fixedSlots, _rng);
        if (rolled <= 0 || rolled == item.ItemSlot) return item;
        _slots.SetMixSlot(item.ItemUid, (byte)rolled);
        return item with { ItemSlot = (byte)rolled };
    }
}
