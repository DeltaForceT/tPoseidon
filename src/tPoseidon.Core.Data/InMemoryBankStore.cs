using System.Collections.Concurrent;

namespace tPoseidon.Core.Data;

public sealed class InMemoryBankStore : IBankStore
{
    private readonly ConcurrentDictionary<uint, long> _money = new();
    private readonly ConcurrentDictionary<uint, ConcurrentDictionary<ulong, byte>> _items = new();
    private readonly IItemStore _itemStore;

    public InMemoryBankStore(IItemStore itemStore)
    {
        _itemStore = itemStore ?? throw new ArgumentNullException(nameof(itemStore));
    }

    public long GetMoney(uint accountId) => _money.TryGetValue(accountId, out var v) ? v : 0L;

    public long AdjustMoney(uint accountId, long delta)
    {
        return _money.AddOrUpdate(
            accountId,
            _ => Math.Max(0L, delta),
            (_, current) => Math.Max(0L, current + delta));
    }

    public IReadOnlyList<Item> GetItems(uint accountId)
    {
        if (!_items.TryGetValue(accountId, out var slots))
        {
            return Array.Empty<Item>();
        }
        var results = new List<Item>(slots.Count);
        foreach (var (itemUid, _) in slots)
        {
            var item = _itemStore.FindByUid(itemUid);
            if (item is not null)
            {
                results.Add(item);
            }
        }
        results.Sort((a, b) => a.SlotPosition.CompareTo(b.SlotPosition));
        return results;
    }

    public Item Deposit(uint accountId, ulong itemUid, byte slotPosition)
    {
        var item = _itemStore.FindByUid(itemUid)
            ?? throw new InvalidOperationException($"Item {itemUid} does not exist.");
        _itemStore.UnassignFromCharacter(itemUid);
        var slots = _items.GetOrAdd(accountId, _ => new ConcurrentDictionary<ulong, byte>());
        slots[itemUid] = slotPosition;
        return item with
        {
            CharacterId = 0,
            SlotKind = SlotKind.Warehouse,
            SlotPosition = slotPosition,
        };
    }

    public bool Withdraw(uint accountId, ulong itemUid)
    {
        if (!_items.TryGetValue(accountId, out var slots))
        {
            return false;
        }
        return slots.TryRemove(itemUid, out _);
    }

    public Item? FindItem(uint accountId, ulong itemUid)
    {
        if (!_items.TryGetValue(accountId, out var slots))
        {
            return null;
        }
        return slots.ContainsKey(itemUid) ? _itemStore.FindByUid(itemUid) : null;
    }
}
