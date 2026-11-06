using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Items;

public sealed class MixSlotItemStore : IItemStore
{
    private readonly IItemStore _inner;
    private readonly MixSlotRoll _roll;
    private readonly RandomStatRoll? _stats;

    public MixSlotItemStore(IItemStore inner, Func<ItemPropertyRules> rules, IItemRefineStore slots, IItemParamLookup catalog, IItemPropertyStore? properties = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _roll = new MixSlotRoll(rules, slots, catalog);
        _stats = properties is null ? null : new RandomStatRoll(rules, properties, catalog);
    }

    public Item GrantToCharacter(ulong characterId, int itemIndex, byte itemClass, byte slotKind, byte slotPosition, ushort stack = 1)
    {
        var item = _roll.Apply(_inner.GrantToCharacter(characterId, itemIndex, itemClass, slotKind, slotPosition, stack));
        return _stats is null || _stats.Apply(item) is not { Count: > 0 } rolled ? item : item with { Rolled = rolled };
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
