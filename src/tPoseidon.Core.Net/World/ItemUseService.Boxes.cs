using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Net.World;

public sealed partial class ItemUseService
{
    public async Task OpenKeyBoxWindowAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (!KeyBoxFrames.TryReadOpen(payload.Span, out var boxUid)) return;
        if (_player.Character() is not { } character) return;
        var held = Held(character, boxUid);
        var zoneType = _player.ZoneType?.Invoke() ?? 0;
        var box = held is { } found ? KeyBoxItemOf(found.Item, found.Definition) : (KeyBoxItem?)null;
        if (!KeyBoxRules.CanOpen(box, held?.Definition.Options ?? 0, zoneType))
        {
            Console.WriteLine($"{_logTag} {_remote()}   key box window refused for 0x{BagItemIds.Stored(boxUid):X}");
            await SendAsync(Opcode.S2C_KEYBOX_OPEN_REFUSED, KeyBoxFrames.OpenRefused(), ct).ConfigureAwait(false);
            return;
        }
        await SendAsync(Opcode.S2C_KEYBOX_OPENED, Array.Empty<byte>(), ct).ConfigureAwait(false);
    }

    public async Task ConfirmKeyBoxAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (!KeyBoxFrames.TryReadConfirm(payload.Span, out var boxUid, out var keyUid)) return;
        if (_player.Character() is not { } character) return;
        var heldBox = Held(character, boxUid);
        var keyGiven = keyUid != 0;
        var heldKey = keyGiven ? Held(character, keyUid) : null;
        var choice = KeyBoxRules.Choose(
            heldBox is { } box ? KeyBoxItemOf(box.Item, box.Definition) : null,
            heldBox is { } rowBox ? _player.Keybox?.Invoke(rowBox.Item.ItemIndex) : null,
            keyGiven,
            heldKey is { } key ? KeyBoxItemOf(key.Item, key.Definition) : null,
            heldKey is { } rateKey ? _player.KeyRate?.Invoke(rateKey.Item.ItemIndex) : null,
            () =>
            {
                lock (_rng) return KeyBoxRules.RollPercent(_rng.Next);
            });
        if (choice.Refusal != KeyBoxRefusal.None || heldBox is not { } openedBox)
        {
            await RefuseKeyBoxAsync(choice.Refusal, ct).ConfigureAwait(false);
            return;
        }
        if (!_presents.TryGetValue(choice.PresentId, out var present))
        {
            Console.WriteLine($"{_logTag} {_remote()}   key box {openedBox.Item.ItemIndex}: present {choice.PresentId} not found");
            await RefuseKeyBoxAsync(KeyBoxRefusal.Failed, ct).ConfigureAwait(false);
            return;
        }
        IReadOnlyList<PresentGrant> rolled;
        lock (_rng) rolled = PresentRolls.Roll(present, _rng.Next);
        var grants = PresentRolls.Merge(rolled, itemId => _catalog.GetById(itemId) is { IsStackable: true });
        if (grants.Count == 0 || grants.Any(grant => _catalog.GetById(grant.ItemId) is null))
        {
            Console.WriteLine($"{_logTag} {_remote()}   key box {openedBox.Item.ItemIndex}: present {choice.PresentId} rolled nothing in the item table");
            await RefuseKeyBoxAsync(KeyBoxRefusal.Failed, ct).ConfigureAwait(false);
            return;
        }
        var bag = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
        var bagWeight = bag.Sum(held => (long)(_catalog.GetById(held.ItemIndex)?.Weight ?? 0) * held.WeightUnits);
        var rewardWeight = grants.Sum(grant => (long)_catalog.GetById(grant.ItemId)!.Weight * grant.Count);
        var removals = choice.UsesKey ? 2 : 1;
        var fit = KeyBoxRules.Fits(bagWeight, rewardWeight, _player.MaxWeight(), grants.Count, PickupRules.BagCapacity - bag.Count, grants.Count + removals);
        if (fit != KeyBoxRefusal.None)
        {
            await RefuseKeyBoxAsync(fit, ct).ConfigureAwait(false);
            return;
        }

        var operations = new List<BagOperation>(grants.Count + removals);
        var timedGrants = new List<Item>();
        foreach (var grant in grants) operations.Add(Grant(character.CharacterId, grant, timedGrants));
        operations.Add(TakeOne(openedBox.Item, openedBox.Definition));
        if (heldKey is { } usedKey) operations.Add(TakeOne(usedKey.Item, usedKey.Definition));
        if (_player.TimedItemsGranted is { } announce)
        {
            foreach (var timed in timedGrants) await announce(TimedItemPeriods.Registered([timed]), ct).ConfigureAwait(false);
        }
        await SendAsync(Opcode.S2C_KEYBOX_CLOSED, Array.Empty<byte>(), ct).ConfigureAwait(false);
        await SendAsync(Opcode.S2C_KEYBOX_RESULT, KeyBoxFrames.Results(operations), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_remote()}   key box {openedBox.Item.ItemIndex} opened with present {choice.PresentId}: {string.Join(", ", grants.Select(grant => $"{grant.ItemId} x{grant.Count}"))}");
    }

    private async Task RefuseKeyBoxAsync(KeyBoxRefusal refusal, CancellationToken ct)
    {
        Console.WriteLine($"{_logTag} {_remote()}   key box confirm refused: {refusal}");
        await SendAsync(Opcode.S2C_KEYBOX_CONFIRM_REFUSED, KeyBoxFrames.ConfirmRefused(refusal), ct).ConfigureAwait(false);
    }

    private static KeyBoxItem KeyBoxItemOf(Item item, ItemParamEntry definition) => new(item.ItemIndex, definition.Effect, item.Stack);

    private BagOperation Grant(ulong characterId, PresentGrant grant, List<Item>? timedGrants = null)
    {
        var given = _catalog.GetById(grant.ItemId)!;
        var count = (ushort)Math.Clamp(grant.Count, PresentRolls.SmallestCount, ushort.MaxValue);
        var stack = given.IsStackable
            ? _items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory).FirstOrDefault(held => held.ItemIndex == grant.ItemId)
            : null;
        if (stack is not null)
        {
            var total = _items.AdjustStack(stack.ItemUid, count);
            return new BagOperation(QuestResultFrames.StackChanged, given.ObjectClass, (uint)grant.ItemId, BagItemIds.OnWire(stack.ItemUid), total);
        }
        var minted = _itemManager.GrantRewardItem(characterId, grant.ItemId, count, itemClassOverride: given.ObjectClass);
        if (timedGrants is not null && TimedItemRules.IsTimed(minted)) timedGrants.Add(minted);
        return new BagOperation(QuestResultFrames.Created, given.ObjectClass, (uint)grant.ItemId, BagItemIds.OnWire(minted.ItemUid), minted.Stack, minted.ItemSlot);
    }

    private BagOperation TakeOne(Item item, ItemParamEntry definition)
    {
        var wireUid = BagItemIds.OnWire(item.ItemUid);
        if (item.Stack > 1)
        {
            var left = _items.AdjustStack(item.ItemUid, -1);
            return new BagOperation(QuestResultFrames.StackChanged, definition.ObjectClass, (uint)item.ItemIndex, wireUid, left);
        }
        _items.Delete(item.ItemUid);
        return new BagOperation(QuestResultFrames.Removed, definition.ObjectClass, (uint)item.ItemIndex, wireUid, 0);
    }
}
