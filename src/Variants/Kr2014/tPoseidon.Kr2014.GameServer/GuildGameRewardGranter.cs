using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.World;

internal sealed class GuildGameRewardGranter
{
    private readonly IItemStore _items;
    private readonly ItemManager _itemManager;
    private readonly IItemParamLookup _catalog;
    private readonly int _bagCapacity;

    public GuildGameRewardGranter(IItemStore items, ItemManager itemManager, IItemParamLookup catalog, int bagCapacity)
    {
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _itemManager = itemManager ?? throw new ArgumentNullException(nameof(itemManager));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _bagCapacity = bagCapacity;
    }

    public (IReadOnlyList<BagOperation> Operations, IReadOnlyList<(uint ItemId, ushort Count)> Given) Grant(ulong characterId, IReadOnlyList<(int ItemId, int Count)> rewards)
    {
        ArgumentNullException.ThrowIfNull(rewards);
        var operations = new List<BagOperation>();
        var given = new List<(uint ItemId, ushort Count)>();
        foreach (var (itemId, count) in rewards)
        {
            var amount = (ushort)Math.Clamp(count, 1, ushort.MaxValue);
            if (Grant(characterId, itemId, amount) is not { } operation) continue;
            operations.Add(operation);
            given.Add(((uint)itemId, amount));
        }
        return (operations, given);
    }

    private BagOperation? Grant(ulong characterId, int itemId, ushort amount)
    {
        var definition = _catalog.GetById(itemId);
        var itemClass = definition?.ObjectClass ?? (byte)ItemParamEntry.StackableClass;
        var bag = _items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory);
        var stack = definition is { IsStackable: true } ? bag.FirstOrDefault(item => item.ItemIndex == itemId) : null;
        if (stack is not null)
        {
            var total = _items.AdjustStack(stack.ItemUid, amount);
            return new BagOperation(QuestResultFrames.StackChanged, itemClass, (uint)itemId, BagItemIds.OnWire(stack.ItemUid), total);
        }
        if (bag.Count >= _bagCapacity) return null;
        var minted = _itemManager.GrantRewardItem(characterId, itemId, amount, itemClassOverride: itemClass);
        return new BagOperation(QuestResultFrames.Created, itemClass, (uint)itemId, BagItemIds.OnWire(minted.ItemUid), minted.Stack, minted.ItemSlot);
    }
}
