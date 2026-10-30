using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Items;

public sealed class MixSlotItemStore : IItemStore
{
    private readonly IItemStore _inner;
    private readonly Func<ItemPropertyRules> _rules;
    private readonly IItemRefineStore _slots;
    private readonly IItemParamLookup _catalog;
    private readonly Random _rng = new();

    public MixSlotItemStore(IItemStore inner, Func<ItemPropertyRules> rules, IItemRefineStore slots, IItemParamLookup catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _slots = slots ?? throw new ArgumentNullException(nameof(slots));
    }

    public Item GrantToCharacter(ulong characterId, int itemIndex, byte itemClass, byte slotKind, byte slotPosition, ushort stack = 1)
    {
        var item = _inner.GrantToCharacter(characterId, itemIndex, itemClass, slotKind, slotPosition, stack);
        if (itemClass != ItemParamEntry.RareClass) return item;
        var fixedSlots = Math.Max(item.ItemSlot, _catalog.GetById(itemIndex)?.CompoundSlot ?? 0);
        int rolled;
        lock (_rng) rolled = _rules().MixSlots(itemIndex, fixedSlots, _rng);
        if (rolled <= 0 || rolled == item.ItemSlot) return item;
        _slots.SetMixSlot(item.ItemUid, (byte)rolled);
        return item with { ItemSlot = (byte)rolled };
    }

    public IReadOnlyList<Item> GetAllForCharacter(ulong characterId) => _inner.GetAllForCharacter(characterId);

    public IReadOnlyList<Item> GetForCharacterBySlotKind(ulong characterId, byte slotKind) => _inner.GetForCharacterBySlotKind(characterId, slotKind);

    public Item? FindByUid(ulong itemUid) => _inner.FindByUid(itemUid);

    public Item? FindBySlot(ulong characterId, byte slotKind, byte slotPosition) => _inner.FindBySlot(characterId, slotKind, slotPosition);

    public bool MoveToSlot(ulong itemUid, byte newSlotKind, byte newSlotPosition) => _inner.MoveToSlot(itemUid, newSlotKind, newSlotPosition);

    public bool UnassignFromCharacter(ulong itemUid) => _inner.UnassignFromCharacter(itemUid);

    public bool AssignToCharacter(ulong itemUid, ulong characterId, byte slotKind, byte slotPosition) =>
        _inner.AssignToCharacter(itemUid, characterId, slotKind, slotPosition);

    public bool Delete(ulong itemUid) => _inner.Delete(itemUid);

    public ushort AdjustStack(ulong itemUid, int delta) => _inner.AdjustStack(itemUid, delta);

    public bool SetStack(ulong itemUid, ushort stack) => _inner.SetStack(itemUid, stack);

    public bool SetBreakedAt(ulong itemUid, DateTimeOffset breakedAt) => _inner.SetBreakedAt(itemUid, breakedAt);
}
