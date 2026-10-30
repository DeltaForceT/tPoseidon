using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Net.World;

public sealed class MaterialExchange
{
    private readonly IItemStore _items;
    private readonly IItemParamLookup _catalog;
    private readonly ItemManager? _periods;
    private readonly List<TimedItem> _timedGranted = new();

    public MaterialExchange(IItemStore items, IItemParamLookup catalog, ItemManager? periods = null)
    {
        _periods = periods;
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public bool BagHolds(ulong characterId, IReadOnlyList<CompoundMaterial> materials)
    {
        ArgumentNullException.ThrowIfNull(materials);
        var bag = _items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory);
        foreach (var material in materials)
        {
            if (material.ItemId == 0) continue;
            var uid = BagItemIds.Stored(material.ItemUid);
            var held = bag.FirstOrDefault(item => item.ItemUid == uid);
            if (held is null || held.ItemIndex != material.ItemId || material.Count < 1 || material.Count > held.Stack) return false;
        }
        return true;
    }

    public bool AllKnown(IEnumerable<int> itemIds) => itemIds.All(itemId => itemId != 0 && _catalog.GetById(itemId) is not null);

    public IReadOnlyList<BagOperation>? Apply(ulong characterId, IReadOnlyList<CompoundMaterial> materials, IReadOnlyList<(int ItemId, int Count)> grants)
    {
        ArgumentNullException.ThrowIfNull(materials);
        ArgumentNullException.ThrowIfNull(grants);
        var consumed = materials.Where(material => material.ItemId != 0)
            .Select(material => (Uid: BagItemIds.Stored(material.ItemUid), material.ItemId, Left: material.Count))
            .ToList();
        var netGrants = new List<(int ItemId, int Count)>(grants.Count);
        foreach (var (itemId, count) in grants)
        {
            var amount = count;
            if (_catalog.GetById(itemId) is { IsStackable: true })
            {
                for (var m = 0; m < consumed.Count && amount > 0; m++)
                {
                    if (consumed[m].ItemId != itemId || consumed[m].Left == 0) continue;
                    var netted = Math.Min(amount, consumed[m].Left);
                    amount -= netted;
                    consumed[m] = consumed[m] with { Left = consumed[m].Left - netted };
                }
            }
            netGrants.Add((itemId, amount));
        }

        var bag = _items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory);
        var newItems = 0;
        foreach (var (itemId, amount) in netGrants)
        {
            if (amount <= 0) continue;
            var definition = _catalog.GetById(itemId)!;
            if (!definition.IsStackable) newItems += amount;
            else if (!bag.Any(held => held.ItemIndex == itemId)) newItems++;
        }
        if (newItems > PickupRules.BagCapacity - bag.Count) return null;

        var operations = new List<BagOperation>();
        foreach (var (itemId, amount) in netGrants)
        {
            if (amount > 0) operations.AddRange(Grant(characterId, itemId, amount));
        }
        foreach (var (uid, _, left) in consumed)
        {
            if (left > 0 && bag.FirstOrDefault(held => held.ItemUid == uid) is { } held) operations.Add(Take(held, left));
        }
        return operations;
    }

    private List<BagOperation> Grant(ulong characterId, int itemId, int amount)
    {
        var definition = _catalog.GetById(itemId)!;
        var itemClass = definition.ObjectClass;
        var bag = _items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory);
        if (definition.IsStackable && bag.FirstOrDefault(held => held.ItemIndex == itemId) is { } stack)
        {
            var total = _items.AdjustStack(stack.ItemUid, amount);
            return new List<BagOperation> { new(QuestResultFrames.StackChanged, itemClass, (uint)itemId, BagItemIds.OnWire(stack.ItemUid), total) };
        }
        var pieces = definition.IsStackable ? 1 : amount;
        var pieceCount = (ushort)(definition.IsStackable ? amount : 1);
        var operations = new List<BagOperation>(pieces);
        for (var i = 0; i < pieces; i++)
        {
            var created = _items.GrantToCharacter(characterId, itemId, itemClass, SlotKind.Inventory,
                EquipmentRules.FirstFreeBagPosition(_items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory)), pieceCount);
            if (_periods is not null) created = _periods.StampPeriod(created, TimedItemPeriods.MinutesOf(definition, _periods.TimerMinutes));
            if (TimedItemRules.IsTimed(created)) _timedGranted.Add(new TimedItem(created.ItemUid, created.ItemIndex, created.BreakedAt));
            operations.Add(new BagOperation(QuestResultFrames.Created, itemClass, (uint)itemId, BagItemIds.OnWire(created.ItemUid), created.Stack, created.ItemSlot));
        }
        return operations;
    }

    public IReadOnlyList<TimedItem> TakeTimedGrants()
    {
        var granted = _timedGranted.ToList();
        _timedGranted.Clear();
        return granted;
    }

    private BagOperation Take(Item held, int count)
    {
        var itemClass = _catalog.GetById(held.ItemIndex)?.ObjectClass ?? held.ItemClass;
        if (held.Stack <= count)
        {
            _items.Delete(held.ItemUid);
            return new BagOperation(QuestResultFrames.Removed, itemClass, (uint)held.ItemIndex, BagItemIds.OnWire(held.ItemUid), 0);
        }
        var left = _items.AdjustStack(held.ItemUid, -count);
        return new BagOperation(QuestResultFrames.StackChanged, itemClass, (uint)held.ItemIndex, BagItemIds.OnWire(held.ItemUid), left);
    }
}
