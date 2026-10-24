using System.Collections.Concurrent;

namespace tPoseidon.Core.Data;

public sealed class InMemoryMyCampStore : IMyCampStore
{
    private readonly object _gate = new();
    private readonly ConcurrentDictionary<uint, byte> _initialized = new();
    private readonly Dictionary<ulong, MyCampSlot> _slots = new();
    private readonly Dictionary<ulong, MyCampItem> _items = new();
    private readonly Dictionary<(ulong SlotCode, ulong ItemUid), MyCampDeco> _deco = new();
    private ulong _nextSlotCode = 0x20000000;
    private ulong _nextItemUid = 0x30000000;

    public bool IsInitializedForAccount(uint accountId) => _initialized.ContainsKey(accountId);

    public void MarkInitializedForAccount(uint accountId) => _initialized[accountId] = 1;

    public MyCampSlot CreateSlotForCharacter(ulong characterId, string slotName = "")
    {
        ArgumentNullException.ThrowIfNull(slotName);
        lock (_gate)
        {
            var slot = new MyCampSlot
            {
                SlotCode = _nextSlotCode++,
                CharacterId = characterId,
                SlotName = slotName,
                SlotUpToDate = DateTimeOffset.UtcNow,
            };
            _slots[slot.SlotCode] = slot;
            return slot;
        }
    }

    public MyCampSlot? FindSlot(ulong slotCode)
    {
        lock (_gate)
        {
            return _slots.TryGetValue(slotCode, out var slot) ? slot : null;
        }
    }

    public IReadOnlyList<MyCampSlot> GetSlotsForCharacter(ulong characterId)
    {
        lock (_gate)
        {
            return _slots.Values.Where(s => s.CharacterId == characterId).OrderBy(s => s.SlotCode).ToArray();
        }
    }

    public bool UpdateSlotName(ulong slotCode, string slotName)
    {
        ArgumentNullException.ThrowIfNull(slotName);
        lock (_gate)
        {
            if (!_slots.TryGetValue(slotCode, out var slot)) return false;
            _slots[slotCode] = slot with { SlotName = slotName, SlotUpToDate = DateTimeOffset.UtcNow };
            return true;
        }
    }

    public bool DeleteSlot(ulong slotCode)
    {
        lock (_gate)
        {
            if (!_slots.Remove(slotCode)) return false;
            var keys = _deco.Keys.Where(k => k.SlotCode == slotCode).ToArray();
            foreach (var k in keys) _deco.Remove(k);
            return true;
        }
    }

    public MyCampItem AddItemForAccount(uint accountId, int itemIndex, byte itemWhere = MyCampItemWhere.New)
    {
        lock (_gate)
        {
            var item = new MyCampItem
            {
                ItemUid = _nextItemUid++,
                AccountId = accountId,
                ItemIndex = itemIndex,
                ItemWhere = itemWhere,
                ItemBuyDate = DateTimeOffset.UtcNow,
                ItemUseDate = null,
                ItemUseTime = 0,
                ItemFlag = 0,
                SellCode = 0,
                SellType = 0,
            };
            _items[item.ItemUid] = item;
            return item;
        }
    }

    public MyCampItem? FindItem(ulong itemUid)
    {
        lock (_gate)
        {
            return _items.TryGetValue(itemUid, out var item) ? item : null;
        }
    }

    public IReadOnlyList<MyCampItem> GetItemsForAccount(uint accountId)
    {
        lock (_gate)
        {
            return _items.Values.Where(i => i.AccountId == accountId).OrderBy(i => i.ItemUid).ToArray();
        }
    }

    public IReadOnlyList<MyCampItem> GetItemsForAccountByWhere(uint accountId, byte itemWhere)
    {
        lock (_gate)
        {
            return _items.Values
                .Where(i => i.AccountId == accountId && i.ItemWhere == itemWhere)
                .OrderBy(i => i.ItemUid)
                .ToArray();
        }
    }

    public MyCampItem? GetLatestBuyItem(uint accountId)
    {
        lock (_gate)
        {
            return _items.Values
                .Where(i => i.AccountId == accountId)
                .OrderByDescending(i => i.ItemBuyDate)
                .FirstOrDefault();
        }
    }

    public bool MoveItemToInventory(ulong itemUid) => MoveItemTo(itemUid, MyCampItemWhere.Inventory);

    public bool MoveItemTo(ulong itemUid, byte itemWhere)
    {
        lock (_gate)
        {
            if (!_items.TryGetValue(itemUid, out var item)) return false;
            _items[itemUid] = item with { ItemWhere = itemWhere };
            return true;
        }
    }

    public bool DeleteItem(ulong itemUid)
    {
        lock (_gate)
        {
            if (!_items.Remove(itemUid)) return false;
            var keys = _deco.Keys.Where(k => k.ItemUid == itemUid).ToArray();
            foreach (var k in keys) _deco.Remove(k);
            return true;
        }
    }

    public int BulkDeleteItems(IEnumerable<ulong> itemUids)
    {
        ArgumentNullException.ThrowIfNull(itemUids);
        var count = 0;
        lock (_gate)
        {
            foreach (var uid in itemUids)
            {
                if (_items.Remove(uid))
                {
                    count++;
                    var keys = _deco.Keys.Where(k => k.ItemUid == uid).ToArray();
                    foreach (var k in keys) _deco.Remove(k);
                }
            }
        }
        return count;
    }

    public MyCampDeco InsertDeco(ulong slotCode, ulong itemUid, short x, short y, byte z, byte direction, ulong parentsUid = 0)
    {
        lock (_gate)
        {
            var deco = new MyCampDeco
            {
                SlotCode = slotCode,
                ItemUid = itemUid,
                DecoX = x,
                DecoY = y,
                DecoZ = z,
                DecoDirection = direction,
                ParentsUid = parentsUid,
            };
            _deco[(slotCode, itemUid)] = deco;
            if (_items.TryGetValue(itemUid, out var item) && item.ItemWhere == MyCampItemWhere.New)
            {
                _items[itemUid] = item with { ItemWhere = MyCampItemWhere.Inventory, ItemUseDate = DateTimeOffset.UtcNow };
            }
            return deco;
        }
    }

    public IReadOnlySet<ulong> GetPlacedItemUidsForAccount(uint accountId)
    {
        lock (_gate)
        {
            return _deco.Keys.Select(key => key.ItemUid)
                .Where(uid => _items.TryGetValue(uid, out var item) && item.AccountId == accountId)
                .ToHashSet();
        }
    }

    public void ReplaceDeco(ulong slotCode, IReadOnlyList<MyCampDeco> placed)
    {
        ArgumentNullException.ThrowIfNull(placed);
        lock (_gate)
        {
            foreach (var key in _deco.Keys.Where(key => key.SlotCode == slotCode).ToArray()) _deco.Remove(key);
        }
        foreach (var deco in placed) InsertDeco(slotCode, deco.ItemUid, deco.DecoX, deco.DecoY, deco.DecoZ, deco.DecoDirection, deco.ParentsUid);
    }

    public IReadOnlyList<MyCampDeco> GetDecoForSlot(ulong slotCode)
    {
        lock (_gate)
        {
            return _deco.Values.Where(d => d.SlotCode == slotCode).ToArray();
        }
    }

    public bool RemoveDeco(ulong slotCode, ulong itemUid)
    {
        lock (_gate)
        {
            if (!_deco.Remove((slotCode, itemUid))) return false;
            if (_items.TryGetValue(itemUid, out var item))
            {
                _items[itemUid] = item with { ItemWhere = MyCampItemWhere.Inventory };
            }
            return true;
        }
    }

    public int InitDecoForSlot(ulong slotCode)
    {
        lock (_gate)
        {
            var keys = _deco.Keys.Where(k => k.SlotCode == slotCode).ToArray();
            foreach (var k in keys) _deco.Remove(k);
            return keys.Length;
        }
    }

    public int MoveDecoToInventory(ulong slotCode)
    {
        lock (_gate)
        {
            var keys = _deco.Keys.Where(k => k.SlotCode == slotCode).ToArray();
            foreach (var k in keys)
            {
                _deco.Remove(k);
                if (_items.TryGetValue(k.ItemUid, out var item))
                {
                    _items[k.ItemUid] = item with { ItemWhere = MyCampItemWhere.Inventory };
                }
            }
            return keys.Length;
        }
    }
}
