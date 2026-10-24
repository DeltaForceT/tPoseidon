namespace tPoseidon.Core.Data;

public sealed class InMemoryItemStore : IItemStore
{
        public const ulong StartingItemUid = 0x10000000;

    private readonly object _gate = new();
    private readonly Dictionary<ulong, Item> _byUid = new();
    private ulong _nextUid = StartingItemUid;

    public Item GrantToCharacter(
        ulong characterId,
        int itemIndex,
        byte itemClass,
        byte slotKind,
        byte slotPosition,
        ushort stack = 1)
    {
        lock (_gate)
        {
            if (slotKind == SlotKind.Equipment && SlotIsOccupied(characterId, slotKind, slotPosition))
            {
                throw new InvalidOperationException(
                    $"Equipment slot ({slotKind}, {slotPosition}) is already occupied for char {characterId}.");
            }

            var item = new Item
            {
                ItemUid = _nextUid++,
                ItemIndex = itemIndex,
                ItemClass = itemClass,
                Stack = stack,
                CharacterId = characterId,
                SlotKind = slotKind,
                SlotPosition = slotPosition,
            };
            _byUid[item.ItemUid] = item;
            return item;
        }
    }

    public IReadOnlyList<Item> GetAllForCharacter(ulong characterId)
    {
        lock (_gate)
        {
            return _byUid.Values
                .Where(i => i.CharacterId == characterId)
                .OrderBy(i => i.SlotKind)
                .ThenBy(i => i.SlotPosition)
                .ToArray();
        }
    }

    public IReadOnlyList<Item> GetForCharacterBySlotKind(ulong characterId, byte slotKind)
    {
        lock (_gate)
        {
            return _byUid.Values
                .Where(i => i.CharacterId == characterId && i.SlotKind == slotKind)
                .OrderBy(i => i.SlotPosition)
                .ToArray();
        }
    }

    public Item? FindByUid(ulong itemUid)
    {
        lock (_gate)
        {
            return _byUid.TryGetValue(itemUid, out var item) ? item : null;
        }
    }

    public Item? FindBySlot(ulong characterId, byte slotKind, byte slotPosition)
    {
        lock (_gate)
        {
            return _byUid.Values.FirstOrDefault(
                i => i.CharacterId == characterId
                  && i.SlotKind == slotKind
                  && i.SlotPosition == slotPosition);
        }
    }

    public bool MoveToSlot(ulong itemUid, byte newSlotKind, byte newSlotPosition)
    {
        lock (_gate)
        {
            if (!_byUid.TryGetValue(itemUid, out var item))
            {
                return false;
            }
            if (newSlotKind == SlotKind.Equipment
                && SlotIsOccupied(item.CharacterId, newSlotKind, newSlotPosition)
                && FindBySlotInternal(item.CharacterId, newSlotKind, newSlotPosition)?.ItemUid != itemUid)
            {
                throw new InvalidOperationException(
                    $"Equipment slot ({newSlotKind}, {newSlotPosition}) is already occupied.");
            }
            _byUid[itemUid] = item with { SlotKind = newSlotKind, SlotPosition = newSlotPosition };
            return true;
        }
    }

    public bool UnassignFromCharacter(ulong itemUid)
    {
        lock (_gate)
        {
            if (!_byUid.TryGetValue(itemUid, out var item))
            {
                return false;
            }
            _byUid[itemUid] = item with
            {
                CharacterId = 0,
                SlotKind = SlotKind.Warehouse,
            };
            return true;
        }
    }

    public bool AssignToCharacter(ulong itemUid, ulong characterId, byte slotKind, byte slotPosition)
    {
        lock (_gate)
        {
            if (!_byUid.TryGetValue(itemUid, out var item))
            {
                return false;
            }
            if (slotKind == SlotKind.Equipment
                && SlotIsOccupied(characterId, slotKind, slotPosition))
            {
                throw new InvalidOperationException(
                    $"Equipment slot ({slotKind}, {slotPosition}) is already occupied for char {characterId}.");
            }
            _byUid[itemUid] = item with
            {
                CharacterId = characterId,
                SlotKind = slotKind,
                SlotPosition = slotPosition,
            };
            return true;
        }
    }

    public bool Delete(ulong itemUid)
    {
        lock (_gate)
        {
            return _byUid.Remove(itemUid);
        }
    }

    public ushort AdjustStack(ulong itemUid, int delta)
    {
        lock (_gate)
        {
            if (!_byUid.TryGetValue(itemUid, out var item))
            {
                return 0;
            }

            var newStack = (long)item.Stack + delta;
            if (newStack <= 0)
            {
                _byUid.Remove(itemUid);
                return 0;
            }
            if (newStack > ushort.MaxValue)
            {
                newStack = ushort.MaxValue;
            }
            _byUid[itemUid] = item with { Stack = (ushort)newStack };
            return (ushort)newStack;
        }
    }

    public bool SetStack(ulong itemUid, ushort stack)
    {
        lock (_gate)
        {
            if (!_byUid.TryGetValue(itemUid, out var item)) return false;
            _byUid[itemUid] = item with { Stack = stack };
            return true;
        }
    }

        public int Count
    {
        get { lock (_gate) { return _byUid.Count; } }
    }

    private bool SlotIsOccupied(ulong characterId, byte slotKind, byte slotPosition)
        => FindBySlotInternal(characterId, slotKind, slotPosition) is not null;

    private Item? FindBySlotInternal(ulong characterId, byte slotKind, byte slotPosition)
        => _byUid.Values.FirstOrDefault(
            i => i.CharacterId == characterId
              && i.SlotKind == slotKind
              && i.SlotPosition == slotPosition);
}
