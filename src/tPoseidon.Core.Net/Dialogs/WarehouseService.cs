using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.Game.Quests;
using tPoseidon.Core.Game.Stats;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.S2;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Dialogs;

public sealed record WarehousePlayer
{
    public required Func<Character?> Character { get; init; }
    public required Func<StatManager?> Stats { get; init; }
    public required Func<int> MaxWeight { get; init; }
    public Func<Item, IReadOnlyList<ItemProperty>>? Properties { get; init; }
}

public readonly record struct WarehouseTab(byte Id, int Slots);

public sealed class WarehouseService
{
    public const int OpenLevel = 5;
    public const int OpenFee = 50;
    public const int LevelTooLow = 2;
    public const int MoneyTooLow = 3;
    public const int TabSlots = 80;
    public const int MaxSlots = 800;
    public const int NotStorableOption = 0x10000;

    private static readonly byte[] TabIds = { 2, 21, 22, 23, 24, 25, 26, 27, 28, 29 };

    private readonly S2SessionContext _ctx;
    private readonly IItemStore _items;
    private readonly IItemParamLookup _catalog;
    private readonly WarehousePlayer _player;
    private readonly Random _rng;
    private readonly string _logTag;
    private bool _contentsSent;

    public WarehouseService(
        S2SessionContext ctx,
        IItemStore items,
        IItemParamLookup catalog,
        WarehousePlayer player,
        Random rng,
        string logTag)
    {
        _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public bool IsOpen { get; private set; }

    public void Leave() => IsOpen = false;

    public static IReadOnlyList<WarehouseTab> Tabs(int totalSlots)
    {
        if (totalSlots < 1 || totalSlots > MaxSlots) return Array.Empty<WarehouseTab>();
        var tabs = new List<WarehouseTab>();
        var left = totalSlots;
        foreach (var id in TabIds)
        {
            if (left <= 0) break;
            var slots = Math.Min(TabSlots, left);
            tabs.Add(new WarehouseTab(id, slots));
            left -= slots;
        }
        return tabs;
    }

    public async Task<int> OpenAsync(CancellationToken ct)
    {
        if (_player.Character() is not { } character || _player.Stats() is not { } stats) return NpcScript.Silent;
        if (IsOpen)
        {
            Console.WriteLine($"{_logTag} {_ctx.Remote}   storage: already open");
            return NpcScript.Silent;
        }
        if (stats.Level < OpenLevel) return LevelTooLow;
        if (stats.Money < OpenFee) return MoneyTooLow;

        await SendAsync(Opcode.C2S_TRIGGER_75, Array.Empty<byte>(), ct).ConfigureAwait(false);
        stats.GrantMoney(-OpenFee, StatChangeSource.Warehouse);
        await SendAsync(Opcode.S2C_MONEY, GroundItemFrames.Money(stats.Money), ct).ConfigureAwait(false);
        IsOpen = true;
        var tabs = Tabs(character.WarehouseSlots);
        if (tabs.Count == 0)
        {
            Console.WriteLine($"{_logTag} {_ctx.Remote}   storage: {character.WarehouseSlots} slots give no tabs");
            return NpcScript.Silent;
        }
        if (_contentsSent)
        {
            await SendAsync(Opcode.S2C_WAREHOUSE_CONTENTS, WarehouseFrames.AlreadyLoaded(tabs[0].Id), ct).ConfigureAwait(false);
            return NpcScript.Silent;
        }
        var total = 0;
        foreach (var tab in tabs)
        {
            var stored = _items.GetForCharacterBySlotKind(character.CharacterId, tab.Id)
                .Select(item => (item, ClassOf(item)))
                .ToList();
            total += stored.Count;
            await SendAsync(Opcode.S2C_WAREHOUSE_CONTENTS, WarehouseFrames.Contents(tab.Id, stored, _player.Properties), ct).ConfigureAwait(false);
        }
        _contentsSent = true;
        Console.WriteLine($"{_logTag} {_ctx.Remote}   storage opened: {tabs.Count} tabs, {character.WarehouseSlots} slots, {total} items");
        return NpcScript.Silent;
    }

    public async Task DepositAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (!IsOpen || !WarehouseFrames.TryReadItemRequest(payload.Span, out var tabId, out var wireUid, out var itemId, out var count)) return;
        if (_player.Character() is not { } character) return;

        if (count < 1)
        {
            await RefuseAsync(Opcode.S2C_WAREHOUSE_DEPOSIT_REFUSED, tabId, $"deposit of {count}", ct).ConfigureAwait(false);
            return;
        }
        if (_catalog.GetById(itemId) is not { } named || (named.Options & NotStorableOption) != 0)
        {
            await RefuseAsync(Opcode.S2C_WAREHOUSE_DEPOSIT_REFUSED, tabId, $"item {itemId} cannot be stored", ct).ConfigureAwait(false);
            return;
        }
        var uid = BagItemIds.Stored(wireUid);
        var held = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory).FirstOrDefault(item => item.ItemUid == uid);
        var definition = held is null ? null : _catalog.GetById(held.ItemIndex);
        if (held is null || definition is null || held.Stack < count)
        {
            await RefuseAsync(Opcode.S2C_WAREHOUSE_DEPOSIT_REFUSED, tabId, $"the bag does not hold {count} of 0x{uid:X}", ct).ConfigureAwait(false);
            return;
        }
        if (Tabs(character.WarehouseSlots).FirstOrDefault(tab => tab.Id == tabId) is not { Slots: > 0 } target)
        {
            Console.WriteLine($"{_logTag} {_ctx.Remote}   storage: deposit into tab {tabId}, which the character does not have");
            return;
        }
        var stored = _items.GetForCharacterBySlotKind(character.CharacterId, tabId);
        var merge = definition.IsStackable ? stored.FirstOrDefault(item => item.ItemIndex == held.ItemIndex) : null;
        if (stored.Count >= target.Slots && merge is null)
        {
            await RefuseAsync(Opcode.S2C_WAREHOUSE_DEPOSIT_REFUSED, tabId, $"tab {tabId} is full", ct).ConfigureAwait(false);
            return;
        }
        if (stored.Any(item => item.ItemUid == uid))
        {
            await RefuseAsync(Opcode.S2C_WAREHOUSE_DEPOSIT_REFUSED, tabId, $"0x{uid:X} is already in tab {tabId}", ct).ConfigureAwait(false);
            return;
        }
        if (merge is not null && merge.Stack + count > BankService.StackLimit)
        {
            await RefuseAsync(Opcode.S2C_WAREHOUSE_DEPOSIT_REFUSED, tabId, $"the stored stack of {held.ItemIndex} would pass {BankService.StackLimit}", ct).ConfigureAwait(false);
            return;
        }

        var (bagOperation, warehouseOperation) = Transfer(character.CharacterId, held, definition.ObjectClass, count, merge, tabId, FreePosition(stored));
        await SendAsync(Opcode.S2C_WAREHOUSE_DEPOSITED, WarehouseFrames.Moved(tabId, bagOperation, warehouseOperation), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   storage: deposited {held.ItemIndex} x{count} into tab {tabId}");
    }

    public async Task WithdrawAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (!IsOpen || !WarehouseFrames.TryReadItemRequest(payload.Span, out var tabId, out var wireUid, out _, out var count)) return;
        if (_player.Character() is not { } character) return;

        var uid = BagItemIds.Stored(wireUid);
        var hasTab = Tabs(character.WarehouseSlots).Any(tab => tab.Id == tabId);
        var stored = hasTab
            ? _items.GetForCharacterBySlotKind(character.CharacterId, tabId).FirstOrDefault(item => item.ItemUid == uid)
            : null;
        if (count < 1 || stored is null || stored.Stack < count || _catalog.GetById(stored.ItemIndex) is not { } definition)
        {
            await RefuseAsync(Opcode.S2C_WAREHOUSE_WITHDRAW_REFUSED, tabId, $"tab {tabId} does not hold {count} of 0x{uid:X}", ct).ConfigureAwait(false);
            return;
        }
        var bag = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
        var weight = definition.Weight * count;
        var bagWeight = bag.Sum(item => (_catalog.GetById(item.ItemIndex)?.Weight ?? 0) * item.WeightUnits);
        if (bag.Count >= PickupRules.BagCapacity || (weight != 0 && bagWeight + weight > _player.MaxWeight()))
        {
            await RefuseAsync(Opcode.S2C_WAREHOUSE_WITHDRAW_REFUSED, tabId, $"no room or weight for {stored.ItemIndex} x{count}", ct).ConfigureAwait(false);
            return;
        }
        if (bag.Any(item => item.ItemUid == uid))
        {
            await RefuseAsync(Opcode.S2C_WAREHOUSE_WITHDRAW_REFUSED, tabId, $"0x{uid:X} is already in the bag", ct).ConfigureAwait(false);
            return;
        }
        var merge = definition.IsStackable ? bag.FirstOrDefault(item => item.ItemIndex == stored.ItemIndex) : null;
        if (merge is not null && merge.Stack + count > BankService.StackLimit)
        {
            await RefuseAsync(Opcode.S2C_WAREHOUSE_WITHDRAW_REFUSED, tabId, $"the bag stack of {stored.ItemIndex} would pass {BankService.StackLimit}", ct).ConfigureAwait(false);
            return;
        }

        var (warehouseOperation, bagOperation) = Transfer(character.CharacterId, stored, definition.ObjectClass, count, merge, SlotKind.Inventory, EquipmentRules.FirstFreeBagPosition(bag));
        await SendAsync(Opcode.S2C_WAREHOUSE_WITHDRAWN, WarehouseFrames.Moved(tabId, bagOperation, warehouseOperation), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   storage: withdrew {stored.ItemIndex} x{count} from tab {tabId}");
    }

    public async Task MoveAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (!IsOpen || !WarehouseFrames.TryReadMoveRequest(payload.Span, out var from, out var to, out var wireUid, out var count)) return;
        if (_player.Character() is not { } character) return;

        var tabs = Tabs(character.WarehouseSlots);
        var hasFrom = tabs.Any(tab => tab.Id == from);
        var target = tabs.FirstOrDefault(tab => tab.Id == to);
        if (count < 1 || from == to || !hasFrom || target.Slots == 0)
        {
            Console.WriteLine($"{_logTag} {_ctx.Remote}   storage: move of {count} from {from} to {to} ignored");
            return;
        }
        var uid = BagItemIds.Stored(wireUid);
        var source = _items.GetForCharacterBySlotKind(character.CharacterId, from).FirstOrDefault(item => item.ItemUid == uid);
        if (source is null || source.Stack < count || _catalog.GetById(source.ItemIndex) is not { } definition)
        {
            await RefuseMoveAsync($"tab {from} does not hold {count} of 0x{uid:X}", ct).ConfigureAwait(false);
            return;
        }
        var destination = _items.GetForCharacterBySlotKind(character.CharacterId, to);
        if (destination.Count >= target.Slots)
        {
            await RefuseMoveAsync($"tab {to} is full", ct).ConfigureAwait(false);
            return;
        }
        var merge = definition.IsStackable ? destination.FirstOrDefault(item => item.ItemIndex == source.ItemIndex) : null;
        if (merge is not null && merge.Stack + count > BankService.StackLimit)
        {
            await RefuseMoveAsync($"the stack of {source.ItemIndex} in tab {to} would pass {BankService.StackLimit}", ct).ConfigureAwait(false);
            return;
        }

        var (fromOperation, toOperation) = Transfer(character.CharacterId, source, definition.ObjectClass, count, merge, to, FreePosition(destination));
        await SendAsync(Opcode.S2C_WAREHOUSE_MOVED, WarehouseFrames.Exchanged(from, fromOperation, to, toOperation), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   storage: moved {source.ItemIndex} x{count} from tab {from} to tab {to}");
    }

    private (BagOperation Source, BagOperation Destination) Transfer(
        ulong characterId, Item source, byte itemClass, int count, Item? merge, byte destination, byte destinationPosition)
    {
        var itemId = (uint)source.ItemIndex;
        var whole = source.Stack == count;
        if (merge is not null)
        {
            var total = _items.AdjustStack(merge.ItemUid, count);
            var mergedInto = new BagOperation(QuestResultFrames.StackChanged, itemClass, itemId, BagItemIds.OnWire(merge.ItemUid), total);
            if (whole)
            {
                _items.Delete(source.ItemUid);
                return (new BagOperation(BankService.Taken, itemClass, itemId, BagItemIds.OnWire(source.ItemUid), 0), mergedInto);
            }
            var kept = _items.AdjustStack(source.ItemUid, -count);
            return (new BagOperation(QuestResultFrames.StackChanged, itemClass, itemId, BagItemIds.OnWire(source.ItemUid), kept), mergedInto);
        }
        if (whole)
        {
            _items.MoveToSlot(source.ItemUid, destination, destinationPosition);
            return (
                new BagOperation(BankService.Taken, itemClass, itemId, BagItemIds.OnWire(source.ItemUid), 0),
                new BagOperation(BankService.Added, itemClass, itemId, BagItemIds.OnWire(source.ItemUid), (ushort)count));
        }
        var left = _items.AdjustStack(source.ItemUid, -count);
        var split = _items.GrantToCharacter(characterId, source.ItemIndex, itemClass, destination, destinationPosition, (ushort)count);
        return (
            new BagOperation(QuestResultFrames.StackChanged, itemClass, itemId, BagItemIds.OnWire(source.ItemUid), left),
            new BagOperation(BankService.Added, itemClass, itemId, BagItemIds.OnWire(split.ItemUid), (ushort)count));
    }

    private byte ClassOf(Item item) => _catalog.GetById(item.ItemIndex)?.ObjectClass ?? item.ItemClass;

    private static byte FreePosition(IReadOnlyList<Item> stored)
    {
        var taken = new HashSet<byte>(stored.Select(item => item.SlotPosition));
        for (var position = 0; position < TabSlots; position++)
        {
            if (!taken.Contains((byte)position)) return (byte)position;
        }
        return TabSlots;
    }

    private async Task RefuseAsync(Opcode opcode, byte tab, string reason, CancellationToken ct)
    {
        Console.WriteLine($"{_logTag} {_ctx.Remote}   storage: {reason}");
        await SendAsync(opcode, WarehouseFrames.Refused(tab), ct).ConfigureAwait(false);
    }

    private async Task RefuseMoveAsync(string reason, CancellationToken ct)
    {
        Console.WriteLine($"{_logTag} {_ctx.Remote}   storage: {reason}");
        await SendAsync(Opcode.S2C_WAREHOUSE_MOVE_REFUSED, Array.Empty<byte>(), ct).ConfigureAwait(false);
    }

    private byte NextRandKey()
    {
        lock (_rng) return (byte)_rng.Next(1, 256);
    }

    private Task SendAsync(Opcode opcode, byte[] payload, CancellationToken ct) =>
        _ctx.SendFrameAsync(opcode, payload, randKey: NextRandKey(), ct);
}
