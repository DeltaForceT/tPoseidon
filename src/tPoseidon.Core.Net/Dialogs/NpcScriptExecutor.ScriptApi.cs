using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.Game.Quests;
using tPoseidon.Core.Game.Scripting.Host;
using tPoseidon.Core.Game.Stats;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Dialogs;

public sealed partial class NpcScriptExecutor
{
    public const int MaximumScriptItemStack = ushort.MaxValue;
    public const int MaximumScriptEquipmentCopies = 20;

    public Character? ScriptCharacter => _player.Character();

    public StatManager? ScriptStats => _player.Stats();

    public static bool IsWindowFunction(uint functionId) => NpcScript.Classify(functionId).Operation is
        NpcScriptOperation.OpenBank or NpcScriptOperation.OpenWarehouse or NpcScriptOperation.OpenRefine or NpcScriptOperation.OpenGrowth
        or NpcScriptOperation.OpenResetMix or NpcScriptOperation.OpenCashBox or NpcScriptOperation.OpenLookChange or NpcScriptOperation.OpenCouponExchange or NpcScriptOperation.OpenItemRecycle or NpcScriptOperation.OpenMyCampRecycle
        or NpcScriptOperation.OpenSlotExtend or NpcScriptOperation.OpenMelting or NpcScriptOperation.OpenEquipComposing or NpcScriptOperation.OpenChargeTimer or NpcScriptOperation.OpenPetRetrain
        or NpcScriptOperation.OpenPetLevelUp or NpcScriptOperation.OpenPetReinforce or NpcScriptOperation.OpenPetReinforceReset or NpcScriptOperation.OpenSoulWindow or NpcScriptOperation.OpenEquipReform or NpcScriptOperation.OpenGhostExchange
        or NpcScriptOperation.EnterFortuneHouse or NpcScriptOperation.OpenCompound or NpcScriptOperation.OpenExchangeShop or NpcScriptOperation.OpenVipShop;

    public static bool TakesTable(uint functionId) => NpcScript.Classify(functionId).Operation is
        NpcScriptOperation.OpenCompound or NpcScriptOperation.OpenExchangeShop or NpcScriptOperation.OpenVipShop;

    public long CountItemForScript(long itemId)
    {
        if (CharacterId is not ulong characterId) return 0;
        return _stores.Items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory)
            .Where(item => item.ItemIndex == itemId)
            .Sum(item => (long)item.Stack);
    }

    public IReadOnlyList<BagOperation>? GiveItemForScript(long itemId, long count)
    {
        if (CharacterId is not ulong characterId) return null;
        if (itemId < 1 || itemId > int.MaxValue || _catalogs.Items.GetById((int)itemId) is not { } definition)
        {
            throw new ScriptRuntimeException($"There is no item with id {itemId}");
        }
        var stackable = definition.IsStackable;
        if (count > (stackable ? MaximumScriptItemStack : MaximumScriptEquipmentCopies))
        {
            throw new ScriptRuntimeException($"At most {(stackable ? MaximumScriptItemStack : MaximumScriptEquipmentCopies)} of item {itemId} can be given at once");
        }
        var bag = _stores.Items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory);
        var existingStack = stackable && bag.Any(item => item.ItemIndex == itemId);
        var slotsNeeded = existingStack ? 0 : stackable ? 1 : (int)count;
        var weight = bag.Sum(item => WeightOf(item.ItemIndex) * item.WeightUnits) + (long)definition.Weight * count;
        if (weight > _player.MaxWeight() || PickupRules.BagCapacity - bag.Count < slotsNeeded) return null;
        var rewards = stackable
            ? new[] { new RewardItem((int)itemId, (int)count) }
            : Enumerable.Repeat(new RewardItem((int)itemId, 1), (int)count).ToArray();
        return GrantRewards(characterId, rewards);
    }

    public IReadOnlyList<BagOperation>? TakeItemForScript(long itemId, long count)
    {
        if (CharacterId is not ulong characterId) return null;
        var bag = _stores.Items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory)
            .Where(item => item.ItemIndex == itemId)
            .OrderBy(item => item.Stack)
            .ToList();
        if (bag.Sum(item => (long)item.Stack) < count) return null;
        var operations = new List<BagOperation>();
        var remaining = count;
        foreach (var held in bag)
        {
            if (remaining == 0) break;
            var itemClass = _catalogs.Items.GetById((int)itemId)?.ObjectClass ?? held.ItemClass;
            var taken = (int)Math.Min(remaining, held.Stack);
            if (held.Stack > taken)
            {
                var left = _stores.Items.AdjustStack(held.ItemUid, -taken);
                operations.Add(new BagOperation(QuestResultFrames.StackChanged, itemClass, (uint)itemId, BagItemIds.OnWire(held.ItemUid), left));
            }
            else
            {
                _stores.Items.Delete(held.ItemUid);
                operations.Add(new BagOperation(QuestResultFrames.Removed, itemClass, (uint)itemId, BagItemIds.OnWire(held.ItemUid), 0));
            }
            remaining -= taken;
        }
        return operations;
    }

    public Task SendItemOperationsAsync(IReadOnlyList<BagOperation> operations, CancellationToken ct) =>
        operations.Count == 0 ? Task.CompletedTask : SendAsync(Opcode.S2C_ITEM_ADDED, QuestResultFrames.ItemOperations(operations), ct);

    public bool ChangeMoneyForScript(long delta)
    {
        if (_player.Stats() is not { } stats) return false;
        if (delta < 0 && stats.Money + delta < 0) return false;
        stats.GrantMoney(delta, StatChangeSource.GM);
        return true;
    }

    public Task SendMoneyAsync(CancellationToken ct) =>
        _player.Stats() is { } stats ? SendAsync(Opcode.S2C_MONEY, GroundItemFrames.Money(stats.Money), ct) : Task.CompletedTask;

    public Task GrantProgressForScript(long exp, long technique, CancellationToken ct)
    {
        return _player.GrantProgress(exp, technique, ct);
    }

    public bool QuestActiveForScript(long questId) =>
        CharacterId is ulong characterId && questId is > 0 and <= int.MaxValue
        && _stores.Quests.FindActive(characterId, QuestTypes.Normal, (int)questId, monsterId: 0) is not null;

    public bool QuestDoneForScript(long questId) =>
        CharacterId is ulong characterId && questId is > 0 and <= int.MaxValue
        && _stores.History.HasCompletedAtLeast(characterId, QuestTypes.Normal, (int)questId, 0, 1);

    public Task RequestZoneChangeForScript(ushort zone, ushort x, ushort y, CancellationToken ct) =>
        ZoneChangeRequested is { } move
            ? move(new ZoneChangeRequest(zone, tPoseidon.Core.Game.Warp.TeleportArrival.ChangeZoneType, null, TimeSpan.Zero, (x, y)), ct)
            : Task.CompletedTask;

    public async Task<bool> OpenWindowForScriptAsync(uint functionId, string? table, CancellationToken ct)
    {
        var call = NpcScript.Classify(functionId);
        var result = call.Operation switch
        {
            NpcScriptOperation.OpenCompound => await _player.OpenCompound(table ?? string.Empty, ct).ConfigureAwait(false),
            NpcScriptOperation.OpenExchangeShop => await _player.OpenExchangeShop(table ?? string.Empty, ct).ConfigureAwait(false),
            NpcScriptOperation.OpenVipShop => await _player.OpenVipShop(table ?? string.Empty, ct).ConfigureAwait(false),
            NpcScriptOperation.OpenBank => await _player.OpenBank(ct).ConfigureAwait(false),
            NpcScriptOperation.OpenWarehouse => await _player.OpenWarehouse(ct).ConfigureAwait(false),
            NpcScriptOperation.OpenRefine => await _player.OpenRefine(ct).ConfigureAwait(false),
            NpcScriptOperation.OpenGrowth => await _player.OpenGrowth(false, ct).ConfigureAwait(false),
            NpcScriptOperation.OpenResetMix => await _player.OpenGrowth(true, ct).ConfigureAwait(false),
            NpcScriptOperation.OpenCashBox => await Optional(_player.OpenCashBox, ct).ConfigureAwait(false),
            NpcScriptOperation.OpenLookChange => await Optional(_player.OpenLookChange, ct).ConfigureAwait(false),
            NpcScriptOperation.OpenCouponExchange => await Optional(_player.OpenCouponExchange, ct).ConfigureAwait(false),
            NpcScriptOperation.OpenItemRecycle => await Optional(_player.OpenItemRecycle, ct).ConfigureAwait(false),
            NpcScriptOperation.OpenMyCampRecycle => await Optional(_player.OpenMyCampRecycle, ct).ConfigureAwait(false),
            NpcScriptOperation.OpenSlotExtend => await Optional(_player.OpenSlotExtend, ct).ConfigureAwait(false),
            NpcScriptOperation.OpenMelting => await Optional(_player.OpenMelting, ct).ConfigureAwait(false),
            NpcScriptOperation.OpenEquipComposing => await Optional(_player.OpenEquipComposing, ct).ConfigureAwait(false),
            NpcScriptOperation.OpenChargeTimer => await Optional(_player.OpenChargeTimer, ct).ConfigureAwait(false),
            NpcScriptOperation.OpenPetRetrain => await Optional(_player.OpenPetRetrain, ct).ConfigureAwait(false),
            NpcScriptOperation.OpenPetLevelUp => await Optional(_player.OpenPetLevelUp, ct).ConfigureAwait(false),
            NpcScriptOperation.OpenPetReinforce => await Optional(_player.OpenPetReinforce, ct).ConfigureAwait(false),
            NpcScriptOperation.OpenPetReinforceReset => await Optional(_player.OpenPetReinforceReset, ct).ConfigureAwait(false),
            NpcScriptOperation.EnterFortuneHouse => await Optional(_player.EnterFortuneHouse, ct).ConfigureAwait(false),
            NpcScriptOperation.OpenSoulWindow => _player.OpenSoulWindow is { } openSoul ? await openSoul(call.Slot, ct).ConfigureAwait(false) : NpcScript.Refused,
            NpcScriptOperation.OpenEquipReform => _player.OpenEquipReform is { } openReform ? await openReform(call.Slot, ct).ConfigureAwait(false) : NpcScript.Refused,
            NpcScriptOperation.OpenGhostExchange => _player.OpenGhostExchange is { } openGhost ? await openGhost(call.Slot == 1, ct).ConfigureAwait(false) : NpcScript.Refused,
            _ => NpcScript.Refused,
        };
        Console.WriteLine($"{_logTag} {_ctx.Remote}   script opened NPC function {functionId} ({call.Operation}): result {result}");
        return result != NpcScript.Refused;
    }

    private static Task<int> Optional(Func<CancellationToken, Task<int>>? open, CancellationToken ct) =>
        open is null ? Task.FromResult(NpcScript.Refused) : open(ct);
}
