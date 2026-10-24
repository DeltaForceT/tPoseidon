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

public sealed record BankPlayer
{
    public required Func<Character?> Character { get; init; }
    public required Func<StatManager?> Stats { get; init; }
    public required Func<int> MaxWeight { get; init; }
    public Func<Item, IReadOnlyList<ItemProperty>>? Properties { get; init; }
}

public sealed class BankService
{
    public const int OpenLevel = 10;
    public const int OpenFee = 100;
    public const int LevelTooLow = 2;
    public const int MoneyTooLow = 3;
    public const int Capacity = 24;
    public const long MoneyLimit = 100_000_000;
    public const long CarriedMoneyLimit = 2_099_999_999;
    public const int StackLimit = 32000;
    public const int NotStorableOptions = 0x200 | 0x400;
    public const int NotStorableOption = 0x20000;
    public const int FirstSpeciesItem = 6900;
    public const int LastSpeciesItem = 6999;
    public const byte Added = 8;
    public const byte Taken = 7;
    public const ushort WrongAmount = 0;
    public const ushort MoreThanCarried = 1;
    public const ushort AboveBankLimit = 2;
    public const ushort BankShort = 0;
    public const ushort AboveCarriedLimit = 1;

    private readonly S2SessionContext _ctx;
    private readonly IBankStore _bank;
    private readonly IItemStore _items;
    private readonly IItemParamLookup _catalog;
    private readonly BankPlayer _player;
    private readonly Random _rng;
    private readonly string _logTag;
    private bool _contentsSent;

    public BankService(
        S2SessionContext ctx,
        IBankStore bank,
        IItemStore items,
        IItemParamLookup catalog,
        BankPlayer player,
        Random rng,
        string logTag)
    {
        _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        _bank = bank ?? throw new ArgumentNullException(nameof(bank));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public bool IsOpen { get; private set; }

    public void Leave() => IsOpen = false;

    public async Task<int> OpenAsync(CancellationToken ct)
    {
        if (_player.Character() is not { } character || _player.Stats() is not { } stats) return NpcScript.Silent;
        if (stats.Level < OpenLevel) return LevelTooLow;
        if (stats.Money < OpenFee) return MoneyTooLow;

        await SendAsync(Opcode.C2S_TRIGGER_75, Array.Empty<byte>(), ct).ConfigureAwait(false);
        stats.GrantMoney(-OpenFee, StatChangeSource.Bank);
        await SendAsync(Opcode.S2C_MONEY, GroundItemFrames.Money(stats.Money), ct).ConfigureAwait(false);
        IsOpen = true;
        if (_contentsSent)
        {
            await SendAsync(Opcode.S2C_BANK_CONTENTS, BankFrames.AlreadyLoaded(), ct).ConfigureAwait(false);
            return NpcScript.Silent;
        }
        var stored = _bank.GetItems(character.OwnerAccountId)
            .Select(item => (item, ClassOf(item)))
            .ToList();
        var bankMoney = (uint)Math.Clamp(_bank.GetMoney(character.OwnerAccountId), 0, uint.MaxValue);
        await SendAsync(Opcode.S2C_BANK_CONTENTS, BankFrames.Contents(bankMoney, stored, _player.Properties), ct).ConfigureAwait(false);
        _contentsSent = true;
        Console.WriteLine($"{_logTag} {_ctx.Remote}   bank opened: {stored.Count} items, {bankMoney} Gelder stored");
        return NpcScript.Silent;
    }

    public async Task DepositAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (!IsOpen || !BankFrames.TryReadItemRequest(payload.Span, out var wireUid, out var itemId, out var count)) return;
        if (_player.Character() is not { } character) return;

        if (_catalog.GetById(itemId) is not { } named
            || (named.Options & NotStorableOptions) == NotStorableOptions
            || (named.Options & NotStorableOption) != 0
            || itemId is >= FirstSpeciesItem and <= LastSpeciesItem)
        {
            await RefuseAsync(Opcode.S2C_BANK_NOT_ACCEPTED, $"item {itemId} cannot be stored", ct).ConfigureAwait(false);
            return;
        }

        var uid = BagItemIds.Stored(wireUid);
        var stored = _bank.GetItems(character.OwnerAccountId);
        var bag = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
        var held = bag.FirstOrDefault(item => item.ItemUid == uid);
        var definition = held is null ? null : _catalog.GetById(held.ItemIndex);
        var merge = definition is { IsStackable: true } ? stored.FirstOrDefault(item => item.ItemIndex == held!.ItemIndex) : null;
        if (stored.Count >= Capacity && merge is null)
        {
            await RefuseAsync(Opcode.S2C_BANK_DEPOSIT_REFUSED, "the bank is full", ct).ConfigureAwait(false);
            return;
        }
        if (held is null || definition is null || count < 1 || held.Stack < count)
        {
            await RefuseAsync(Opcode.S2C_BANK_DEPOSIT_REFUSED, $"the bag does not hold {count} of 0x{uid:X}", ct).ConfigureAwait(false);
            return;
        }
        if (stored.Any(item => item.ItemUid == uid))
        {
            await RefuseAsync(Opcode.S2C_BANK_DEPOSIT_REFUSED, $"0x{uid:X} is already in the bank", ct).ConfigureAwait(false);
            return;
        }
        if (merge is not null && merge.Stack + count > StackLimit)
        {
            await RefuseAsync(Opcode.S2C_BANK_DEPOSIT_REFUSED, $"the bank stack of {held.ItemIndex} would pass {StackLimit}", ct).ConfigureAwait(false);
            return;
        }

        var itemClass = definition.ObjectClass;
        var whole = held.Stack == count;
        BagOperation bagOperation;
        BagOperation bankOperation;
        if (merge is not null)
        {
            var total = _items.AdjustStack(merge.ItemUid, count);
            bankOperation = new BagOperation(QuestResultFrames.StackChanged, itemClass, (uint)held.ItemIndex, BagItemIds.OnWire(merge.ItemUid), total);
            bagOperation = TakeFromBag(held, count, itemClass, whole);
        }
        else if (whole)
        {
            _bank.Deposit(character.OwnerAccountId, held.ItemUid, FreeBankPosition(stored));
            bagOperation = new BagOperation(Taken, itemClass, (uint)held.ItemIndex, BagItemIds.OnWire(held.ItemUid), 0);
            bankOperation = new BagOperation(Added, itemClass, (uint)held.ItemIndex, BagItemIds.OnWire(held.ItemUid), (ushort)count);
        }
        else
        {
            var left = _items.AdjustStack(held.ItemUid, -count);
            var split = _items.GrantToCharacter(character.CharacterId, held.ItemIndex, itemClass, SlotKind.Inventory,
                EquipmentRules.FirstFreeBagPosition(_items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory)), (ushort)count);
            _bank.Deposit(character.OwnerAccountId, split.ItemUid, FreeBankPosition(stored));
            bagOperation = new BagOperation(QuestResultFrames.StackChanged, itemClass, (uint)held.ItemIndex, BagItemIds.OnWire(held.ItemUid), left);
            bankOperation = new BagOperation(Added, itemClass, (uint)held.ItemIndex, BagItemIds.OnWire(split.ItemUid), (ushort)count);
        }

        await SendAsync(Opcode.S2C_BANK_DEPOSITED, BankFrames.Moved(bagOperation, bankOperation), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   bank: deposited {held.ItemIndex} x{count}");
    }

    public async Task WithdrawAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (!IsOpen || !BankFrames.TryReadItemRequest(payload.Span, out var wireUid, out _, out var count)) return;
        if (_player.Character() is not { } character) return;

        var uid = BagItemIds.Stored(wireUid);
        var stored = _bank.FindItem(character.OwnerAccountId, uid);
        if (stored is null || count < 1 || stored.Stack < count || _catalog.GetById(stored.ItemIndex) is not { } definition)
        {
            await RefuseAsync(Opcode.S2C_BANK_WITHDRAW_REFUSED, $"the bank does not hold {count} of 0x{uid:X}", ct).ConfigureAwait(false);
            return;
        }
        var bag = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
        var weight = definition.Weight * count;
        var bagWeight = bag.Sum(item => (_catalog.GetById(item.ItemIndex)?.Weight ?? 0) * item.WeightUnits);
        if (bag.Count >= PickupRules.BagCapacity || (weight != 0 && bagWeight + weight > _player.MaxWeight()))
        {
            await RefuseAsync(Opcode.S2C_BANK_WITHDRAW_REFUSED, $"no room or weight for {stored.ItemIndex} x{count}", ct).ConfigureAwait(false);
            return;
        }
        if (bag.Any(item => item.ItemUid == uid))
        {
            await RefuseAsync(Opcode.S2C_BANK_WITHDRAW_REFUSED, $"0x{uid:X} is already in the bag", ct).ConfigureAwait(false);
            return;
        }
        var merge = definition.IsStackable ? bag.FirstOrDefault(item => item.ItemIndex == stored.ItemIndex) : null;
        if (merge is not null && merge.Stack + count > StackLimit)
        {
            await RefuseAsync(Opcode.S2C_BANK_WITHDRAW_REFUSED, $"the bag stack of {stored.ItemIndex} would pass {StackLimit}", ct).ConfigureAwait(false);
            return;
        }

        var itemClass = definition.ObjectClass;
        var whole = stored.Stack == count;
        BagOperation bagOperation;
        BagOperation bankOperation;
        if (merge is not null)
        {
            var total = _items.AdjustStack(merge.ItemUid, count);
            bagOperation = new BagOperation(QuestResultFrames.StackChanged, itemClass, (uint)stored.ItemIndex, BagItemIds.OnWire(merge.ItemUid), total);
            if (whole)
            {
                _bank.Withdraw(character.OwnerAccountId, stored.ItemUid);
                _items.Delete(stored.ItemUid);
                bankOperation = new BagOperation(Taken, itemClass, (uint)stored.ItemIndex, BagItemIds.OnWire(stored.ItemUid), 0);
            }
            else
            {
                var left = _items.AdjustStack(stored.ItemUid, -count);
                bankOperation = new BagOperation(QuestResultFrames.StackChanged, itemClass, (uint)stored.ItemIndex, BagItemIds.OnWire(stored.ItemUid), left);
            }
        }
        else if (whole)
        {
            _bank.Withdraw(character.OwnerAccountId, stored.ItemUid);
            _items.AssignToCharacter(stored.ItemUid, character.CharacterId, SlotKind.Inventory, EquipmentRules.FirstFreeBagPosition(bag));
            bankOperation = new BagOperation(Taken, itemClass, (uint)stored.ItemIndex, BagItemIds.OnWire(stored.ItemUid), 0);
            bagOperation = new BagOperation(Added, itemClass, (uint)stored.ItemIndex, BagItemIds.OnWire(stored.ItemUid), (ushort)count);
        }
        else
        {
            var left = _items.AdjustStack(stored.ItemUid, -count);
            var split = _items.GrantToCharacter(character.CharacterId, stored.ItemIndex, itemClass, SlotKind.Inventory,
                EquipmentRules.FirstFreeBagPosition(bag), (ushort)count);
            bankOperation = new BagOperation(QuestResultFrames.StackChanged, itemClass, (uint)stored.ItemIndex, BagItemIds.OnWire(stored.ItemUid), left);
            bagOperation = new BagOperation(Added, itemClass, (uint)stored.ItemIndex, BagItemIds.OnWire(split.ItemUid), (ushort)count);
        }

        await SendAsync(Opcode.S2C_BANK_WITHDRAWN, BankFrames.Moved(bagOperation, bankOperation), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   bank: withdrew {stored.ItemIndex} x{count}");
    }

    public async Task MoneyInAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (!IsOpen || payload.Length < BankFrames.MoneyRequestSize) return;
        if (_player.Character() is not { } character || _player.Stats() is not { } stats) return;
        var amount = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(payload.Span);
        var bankMoney = _bank.GetMoney(character.OwnerAccountId);
        ushort? refusal = amount < 1 ? WrongAmount
            : amount > stats.Money ? MoreThanCarried
            : bankMoney + amount >= MoneyLimit ? AboveBankLimit
            : null;
        if (refusal is { } code)
        {
            await RefuseAsync(Opcode.S2C_BANK_MONEY_IN_REFUSED, BankFrames.Code(code), $"{amount} Gelder in refused ({code})", ct).ConfigureAwait(false);
            return;
        }
        stats.GrantMoney(-(int)amount, StatChangeSource.Bank);
        var total = _bank.AdjustMoney(character.OwnerAccountId, amount);
        await SendAsync(Opcode.S2C_BANK_MONEY_IN, BankFrames.Money((uint)total, stats.Money), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   bank: {amount} Gelder in, {total} stored, {stats.Money} carried");
    }

    public async Task MoneyOutAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (!IsOpen || payload.Length < BankFrames.MoneyRequestSize) return;
        if (_player.Character() is not { } character || _player.Stats() is not { } stats) return;
        var amount = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(payload.Span);
        if (amount < 1)
        {
            await RefuseAsync(Opcode.S2C_BANK_MONEY_IN_REFUSED, BankFrames.Code(WrongAmount), $"{amount} Gelder out refused", ct).ConfigureAwait(false);
            return;
        }
        var bankMoney = _bank.GetMoney(character.OwnerAccountId);
        ushort? refusal = bankMoney - amount < 0 ? BankShort
            : stats.Money + (long)amount > CarriedMoneyLimit ? AboveCarriedLimit
            : null;
        if (refusal is { } code)
        {
            await RefuseAsync(Opcode.S2C_BANK_MONEY_OUT_REFUSED, BankFrames.Code(code), $"{amount} Gelder out refused ({code})", ct).ConfigureAwait(false);
            return;
        }
        var total = _bank.AdjustMoney(character.OwnerAccountId, -(long)amount);
        stats.GrantMoney((int)amount, StatChangeSource.Bank);
        await SendAsync(Opcode.S2C_BANK_MONEY_OUT, BankFrames.Money((uint)total, stats.Money), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   bank: {amount} Gelder out, {total} stored, {stats.Money} carried");
    }

    private BagOperation TakeFromBag(Item held, int count, byte itemClass, bool whole)
    {
        if (whole)
        {
            _items.Delete(held.ItemUid);
            return new BagOperation(Taken, itemClass, (uint)held.ItemIndex, BagItemIds.OnWire(held.ItemUid), 0);
        }
        var left = _items.AdjustStack(held.ItemUid, -count);
        return new BagOperation(QuestResultFrames.StackChanged, itemClass, (uint)held.ItemIndex, BagItemIds.OnWire(held.ItemUid), left);
    }

    private byte ClassOf(Item item) => _catalog.GetById(item.ItemIndex)?.ObjectClass ?? item.ItemClass;

    private static byte FreeBankPosition(IReadOnlyList<Item> stored)
    {
        var taken = new HashSet<byte>(stored.Select(item => item.SlotPosition));
        for (var position = 0; position < byte.MaxValue; position++)
        {
            if (!taken.Contains((byte)position)) return (byte)position;
        }
        return byte.MaxValue;
    }

    private Task RefuseAsync(Opcode opcode, string reason, CancellationToken ct) =>
        RefuseAsync(opcode, Array.Empty<byte>(), reason, ct);

    private async Task RefuseAsync(Opcode opcode, byte[] payload, string reason, CancellationToken ct)
    {
        Console.WriteLine($"{_logTag} {_ctx.Remote}   bank: {reason}");
        await SendAsync(opcode, payload, ct).ConfigureAwait(false);
    }

    private byte NextRandKey()
    {
        lock (_rng) return (byte)_rng.Next(1, 256);
    }

    private Task SendAsync(Opcode opcode, byte[] payload, CancellationToken ct) =>
        _ctx.SendFrameAsync(opcode, payload, randKey: NextRandKey(), ct);
}
