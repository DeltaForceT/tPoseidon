using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Items;

public sealed class ItemManager
{
    private readonly IItemStore _itemStore;
    private readonly IItemParamLookup _itemCatalog;

    public ItemManager(IItemStore itemStore, IItemParamLookup itemCatalog)
    {
        ArgumentNullException.ThrowIfNull(itemStore);
        ArgumentNullException.ThrowIfNull(itemCatalog);
        _itemStore = itemStore;
        _itemCatalog = itemCatalog;
    }

    public Func<int, int?>? DrillLifeOf { get; set; }

    public IReadOnlyDictionary<int, int> TimerMinutes { get; set; } = new Dictionary<int, int>();

    public TimeProvider Clock { get; set; } = TimeProvider.System;

    public Item StampPeriod(Item item, int minutes)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (minutes <= 0 || item.ItemClass != Item.RareClass) return item;
        var expiresAt = Clock.GetUtcNow().AddMinutes(minutes);
        return _itemStore.SetBreakedAt(item.ItemUid, expiresAt) ? item with { BreakedAt = expiresAt } : item;
    }

    public ushort NewStack(int itemIndex, ushort stack) =>
        DrillLifeOf?.Invoke(itemIndex) is int life && life > 0 ? (ushort)Math.Min(life, ushort.MaxValue) : stack;

    public event Action<ItemGrantedEvent>? ItemGranted;
    public event Action<ItemRevokedEvent>? ItemRevoked;
    public event Action<ItemEquippedEvent>? ItemEquipped;
    public event Action<ItemReleasedEvent>? ItemReleased;
    public event Action<ItemDeletedEvent>? ItemDeleted;

    public Item GrantQuestItem(ulong characterId, int itemIndex, int questId)
    {
        const byte questItemClass = 4;

        var existing = _itemStore.GetForCharacterBySlotKind(characterId, SlotKind.Inventory)
            .FirstOrDefault(i => i.ItemIndex == itemIndex);
        Item granted;
        bool isNew;
        if (existing is not null)
        {
            granted = existing;
            isNew = false;
        }
        else
        {
            granted = _itemStore.GrantToCharacter(
                characterId,
                itemIndex: itemIndex,
                itemClass: questItemClass,
                slotKind: SlotKind.Inventory,
                slotPosition: 0,
                stack: 1);
            isNew = true;
        }
        ItemGranted?.Invoke(new ItemGrantedEvent
        {
            CharacterId = characterId,
            Item = granted,
            QuestId = questId,
            IsNewlyMinted = isNew,
            IsEquipment = IsEquipmentClass(itemIndex),
        });
        return granted;
    }

    public Item GrantRewardItem(
        ulong characterId,
        int itemIndex,
        ushort stack,
        byte slotKind = SlotKind.Inventory,
        byte? itemClassOverride = null,
        int? periodMinutes = null)
    {
        var occupiedSlots = new HashSet<byte>(
            _itemStore.GetForCharacterBySlotKind(characterId, slotKind)
                .Select(i => i.SlotPosition));
        byte freeSlot = 0;
        while (occupiedSlots.Contains(freeSlot)) freeSlot++;

        var resolvedClass = itemClassOverride ?? ResolveItemClassFromCatalog(itemIndex);

        var granted = _itemStore.GrantToCharacter(
            characterId,
            itemIndex: itemIndex,
            itemClass: resolvedClass,
            slotKind: slotKind,
            slotPosition: freeSlot,
            stack: NewStack(itemIndex, stack));
        granted = StampPeriod(granted, periodMinutes ?? TimedItemPeriods.MinutesOf(_itemCatalog.GetById(itemIndex), TimerMinutes));
        ItemGranted?.Invoke(new ItemGrantedEvent
        {
            CharacterId = characterId,
            Item = granted,
            QuestId = 0,
            IsNewlyMinted = true,
            IsEquipment = IsEquipmentClass(itemIndex),
        });
        return granted;
    }

    private byte ResolveItemClassFromCatalog(int itemIndex)
    {
        var def = _itemCatalog.GetById(itemIndex);
        if (def is null) return 1;
        return def.Class == 5 ? (byte)1 : def.ObjectClass;
    }

    public Item? RevokeQuestItem(ulong characterId, int itemIndex, int questId = 0)
    {
        var row = _itemStore.GetForCharacterBySlotKind(characterId, SlotKind.Inventory)
            .FirstOrDefault(i => i.ItemIndex == itemIndex);
        if (row is null) return null;
        if (!_itemStore.Delete(row.ItemUid)) return null;
        ItemRevoked?.Invoke(new ItemRevokedEvent
        {
            CharacterId = characterId,
            Item = row,
            QuestId = questId,
        });
        return row;
    }

    public EquipResult EquipFromInventory(ulong characterId, ulong itemUid, byte targetSlot)
    {
        var item = _itemStore.FindByUid(itemUid);
        if (item is null || item.CharacterId != characterId)
            return EquipResult.NotFound;
        if (item.SlotKind != SlotKind.Inventory)
            return EquipResult.NotInInventory;

        var occupant = _itemStore.FindBySlot(characterId, SlotKind.Equipment, targetSlot);
        if (occupant is not null)
            return EquipResult.SlotOccupied;

        if (!_itemStore.MoveToSlot(itemUid, SlotKind.Equipment, targetSlot))
            return EquipResult.NotFound;

        ItemEquipped?.Invoke(new ItemEquippedEvent
        {
            CharacterId = characterId,
            ItemUid = itemUid,
            SlotPosition = targetSlot,
            DisplacedItemUid = null,
        });
        return EquipResult.Equipped;
    }

    public EquipResult ExchangeInventoryAndEquip(
        ulong characterId,
        ulong inventoryItemUid,
        byte targetSlot)
    {
        var inventoryItem = _itemStore.FindByUid(inventoryItemUid);
        if (inventoryItem is null || inventoryItem.CharacterId != characterId)
            return EquipResult.NotFound;
        if (inventoryItem.SlotKind != SlotKind.Inventory)
            return EquipResult.NotInInventory;

        var equipped = _itemStore.FindBySlot(characterId, SlotKind.Equipment, targetSlot);
        if (equipped is null)
            return EquipFromInventory(characterId, inventoryItemUid, targetSlot);

        var freeInvSlot = NextFreeInventorySlot(characterId, exclude: inventoryItem.SlotPosition);
        if (!_itemStore.MoveToSlot(equipped.ItemUid, SlotKind.Inventory, freeInvSlot))
            return EquipResult.NotFound;
        if (!_itemStore.MoveToSlot(inventoryItemUid, SlotKind.Equipment, targetSlot))
        {
            _itemStore.MoveToSlot(equipped.ItemUid, SlotKind.Equipment, targetSlot);
            return EquipResult.NotFound;
        }

        ItemEquipped?.Invoke(new ItemEquippedEvent
        {
            CharacterId = characterId,
            ItemUid = inventoryItemUid,
            SlotPosition = targetSlot,
            DisplacedItemUid = equipped.ItemUid,
        });
        return EquipResult.Equipped;
    }

    public ReleaseResult ReleaseToInventory(ulong characterId, ulong itemUid)
    {
        var item = _itemStore.FindByUid(itemUid);
        if (item is null || item.CharacterId != characterId)
            return ReleaseResult.NotFound;
        if (item.SlotKind != SlotKind.Equipment)
            return ReleaseResult.NotEquipped;

        var freeSlot = NextFreeInventorySlot(characterId);
        if (!_itemStore.MoveToSlot(itemUid, SlotKind.Inventory, freeSlot))
            return ReleaseResult.NotFound;

        ItemReleased?.Invoke(new ItemReleasedEvent
        {
            CharacterId = characterId,
            ItemUid = itemUid,
            FromSlotPosition = item.SlotPosition,
            ToInventorySlot = freeSlot,
        });
        return ReleaseResult.Released;
    }

    public IReadOnlyList<Item> ReleaseAllToInventory(ulong characterId, ushort characterLevel)
    {
        var equipped = _itemStore.GetForCharacterBySlotKind(characterId, SlotKind.Equipment);
        var released = new List<Item>();
        foreach (var item in equipped)
        {
            if (characterLevel < 20 && (item.SlotPosition == EquipmentSlot.Ear || item.SlotPosition == EquipmentSlot.Tail))
                continue;
            var freeSlot = NextFreeInventorySlot(characterId);
            if (_itemStore.MoveToSlot(item.ItemUid, SlotKind.Inventory, freeSlot))
            {
                ItemReleased?.Invoke(new ItemReleasedEvent
                {
                    CharacterId = characterId,
                    ItemUid = item.ItemUid,
                    FromSlotPosition = item.SlotPosition,
                    ToInventorySlot = freeSlot,
                });
                released.Add(item);
            }
        }
        return released;
    }

    public bool DeleteItem(ulong characterId, ulong itemUid)
    {
        var item = _itemStore.FindByUid(itemUid);
        if (item is null || item.CharacterId != characterId) return false;
        if (item.SlotKind != SlotKind.Equipment && item.SlotKind != SlotKind.Inventory) return false;
        if (!_itemStore.Delete(itemUid)) return false;

        ItemDeleted?.Invoke(new ItemDeletedEvent
        {
            CharacterId = characterId,
            Item = item,
        });
        return true;
    }

    public ushort UseStackable(ulong characterId, ulong itemUid, int consumeCount = 1)
    {
        var item = _itemStore.FindByUid(itemUid);
        if (item is null || item.CharacterId != characterId) return 0;
        var newStack = _itemStore.AdjustStack(itemUid, -Math.Abs(consumeCount));
        if (newStack == 0)
        {
            ItemDeleted?.Invoke(new ItemDeletedEvent
            {
                CharacterId = characterId,
                Item = item,
            });
        }
        return newStack;
    }

    private bool IsEquipmentClass(int itemIndex)
    {
        var def = _itemCatalog.GetById(itemIndex);
        if (def is null) return false;
        return def.Class == 5;
    }

    private byte NextFreeInventorySlot(ulong characterId, byte? exclude = null)
    {
        var occupied = new HashSet<byte>(
            _itemStore.GetForCharacterBySlotKind(characterId, SlotKind.Inventory)
                .Select(i => i.SlotPosition));
        if (exclude.HasValue) occupied.Remove(exclude.Value);
        byte slot = 0;
        while (occupied.Contains(slot)) slot++;
        return slot;
    }
}

public enum EquipResult
{
    Equipped,
    NotFound,
    NotInInventory,
    SlotOccupied,
}

public enum ReleaseResult
{
    Released,
    NotFound,
    NotEquipped,
}

public sealed record ItemGrantedEvent
{
    public required ulong CharacterId { get; init; }
    public required Item Item { get; init; }
    public required int QuestId { get; init; }
    public required bool IsNewlyMinted { get; init; }
    public required bool IsEquipment { get; init; }
}

public sealed record ItemRevokedEvent
{
    public required ulong CharacterId { get; init; }
    public required Item Item { get; init; }
    public required int QuestId { get; init; }
}

public sealed record ItemEquippedEvent
{
    public required ulong CharacterId { get; init; }
    public required ulong ItemUid { get; init; }
    public required byte SlotPosition { get; init; }
    public required ulong? DisplacedItemUid { get; init; }
}

public sealed record ItemReleasedEvent
{
    public required ulong CharacterId { get; init; }
    public required ulong ItemUid { get; init; }
    public required byte FromSlotPosition { get; init; }
    public required byte ToInventorySlot { get; init; }
}

public sealed record ItemDeletedEvent
{
    public required ulong CharacterId { get; init; }
    public required Item Item { get; init; }
}
