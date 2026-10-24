using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.CardBattle;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.Dialogs;
using tPoseidon.Core.Net.Social;
using tPoseidon.Core.Net.World;

internal sealed class CardBattleController : IDisposable
{
    private readonly SystemServerLink _link;
    private readonly Func<ulong, InGameSessionState?> _playerById;
    private readonly IItemStore _items;
    private readonly ItemManager _itemManager;
    private readonly IItemParamLookup _catalog;
    private readonly int _bagCapacity;
    private readonly string _logTag;
    private readonly Random _rng = new();

    public CardBattleController(
        SystemServerLink link, Func<ulong, InGameSessionState?> playerById, IItemStore items, ItemManager itemManager,
        IItemParamLookup catalog, int bagCapacity, string logTag)
    {
        _link = link ?? throw new ArgumentNullException(nameof(link));
        _playerById = playerById ?? throw new ArgumentNullException(nameof(playerById));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _itemManager = itemManager ?? throw new ArgumentNullException(nameof(itemManager));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _bagCapacity = bagCapacity;
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
        _link.OrderReceived += OrderAsync;
    }

    public void Dispose() => _link.OrderReceived -= OrderAsync;

    private async Task OrderAsync(OfficerOrder order, CancellationToken ct)
    {
        if (order.OrderType != CardBattleFrames.OrderStatusUpdate) return;
        if (CardBattleFrames.ReadStatus(order.Data) is not { } update)
        {
            Console.WriteLine($"{_logTag} [cardbattle] ORDER 0x9A of {order.Data.Length} B not read");
            return;
        }
        var status = update.Status;
        if (_playerById(update.CharacterId) is not { Character: { } character } player)
        {
            await ReplyAsync(order, update, CardBattleFrames.StatusNoPlayer, CardBattleRewardBlock.None, false, ct).ConfigureAwait(false);
            return;
        }
        InGameSessionState? opponent = null;
        if (status.Winner && (opponent = _playerById(status.OpponentCharacterId)) is not { Character: not null })
        {
            await ReplyAsync(order, update, CardBattleFrames.StatusNoOpponent, CardBattleRewardBlock.None, false, ct).ConfigureAwait(false);
            return;
        }
        var code = CardBattleFrames.StatusOk;
        if (opponent is not null && !await SnatchAsync(player, opponent, status).ConfigureAwait(false)) code = CardBattleFrames.StatusBagFull;
        var cards = new List<int>(2);
        if (status.Special && !status.OverDistance && status.PresentItemId != 0) cards.Add((int)status.PresentItemId);
        if (!status.OverDistance && !status.OpponentHoldsSet && status.WonCardItemId != 0) cards.Add((int)status.WonCardItemId);
        foreach (var card in cards)
        {
            if (Grant(character.CharacterId, card) is { } change)
            {
                await SendAsync(player, Opcode.S2C_ITEM_OPERATION, QuestResultFrames.ItemOperation(change)).ConfigureAwait(false);
                Console.WriteLine($"{_logTag} [cardbattle] 0x{player.PlayerEntityId:X4} receives card {card}");
            }
            else
            {
                code = CardBattleFrames.StatusBagFull;
                Console.WriteLine($"{_logTag} [cardbattle] 0x{player.PlayerEntityId:X4} card {card}: bag full");
            }
        }
        var reward = CardBattleReward.Compute(player.Stats?.Level ?? character.Level, status);
        if (reward.Exp > 0)
        {
            if (player.GainRewardExp is { } gainExp)
            {
                foreach (var frame in gainExp(reward.Exp)) await SendAsync(player, frame.Opcode, frame.Payload).ConfigureAwait(false);
            }
            if (player.GainRewardTmExp is { } gainTm)
            {
                foreach (var frame in gainTm(reward.Exp)) await SendAsync(player, frame.Opcode, frame.Payload).ConfigureAwait(false);
            }
        }
        if (reward.Money > 0 && player.Stats is { } stats)
        {
            stats.GrantMoney(reward.Money, tPoseidon.Core.Game.Stats.StatChangeSource.CardBattle);
            await SendAsync(player, Opcode.S2C_MONEY, GroundItemFrames.Money(stats.Money)).ConfigureAwait(false);
        }
        Console.WriteLine($"{_logTag} [cardbattle] 0x{player.PlayerEntityId:X4} {(status.Winner ? "won" : "lost")} ({status.Wins} wins, score {status.Score}): exp and TM exp {reward.Exp}, gelder {reward.Money}");
        var block = new CardBattleRewardBlock(
            player.Stats?.Level ?? character.Level,
            status.Special,
            reward.Exp,
            WireLimits.Int32FieldUnsigned(player.Stats?.Exp ?? 0UL),
            status.OverDistance ? 0 : status.WonCardItemId,
            status.LostCardItemId,
            reward.Money,
            status.OverDistance ? 0 : status.PresentItemId,
            status.OverDistance ? 0 : status.SecondWonCardItemId,
            status.SecondLostCardItemId);
        var deckReady = CardBattleRules.CanStillBuildDeck(HeldCards(character.CharacterId), status.LostCardItemId, status.SecondLostCardItemId);
        await ReplyAsync(order, update, code, block, deckReady, ct).ConfigureAwait(false);
    }

    private async Task<bool> SnatchAsync(InGameSessionState winner, InGameSessionState opponent, CardBattleStatus status)
    {
        var wanted = new List<int>(2);
        if (status.OpponentHoldsSet) wanted.Add((int)status.WonCardItemId);
        if (status.OpponentUsedChance) wanted.Add((int)status.SecondWonCardItemId);
        var taken = wanted.Where(itemId => itemId != 0 && Holds(opponent.Character!.CharacterId, itemId)).ToList();
        if (taken.Count == 0) return true;
        if (!status.OverDistance && !CanHold(winner.Character!.CharacterId, taken))
        {
            Console.WriteLine($"{_logTag} [cardbattle] 0x{winner.PlayerEntityId:X4} cannot take {string.Join(", ", taken)} from 0x{opponent.PlayerEntityId:X4}: bag full");
            return false;
        }
        foreach (var itemId in taken)
        {
            foreach (var removal in Take(opponent.Character!.CharacterId, itemId))
            {
                await SendAsync(opponent, Opcode.S2C_ITEM_OPERATION, QuestResultFrames.ItemOperation(removal)).ConfigureAwait(false);
            }
            Console.WriteLine($"{_logTag} [cardbattle] 0x{opponent.PlayerEntityId:X4} loses card {itemId}");
            if (status.OverDistance) continue;
            if (Grant(winner.Character!.CharacterId, itemId) is { } change)
            {
                await SendAsync(winner, Opcode.S2C_ITEM_OPERATION, QuestResultFrames.ItemOperation(change)).ConfigureAwait(false);
                Console.WriteLine($"{_logTag} [cardbattle] 0x{winner.PlayerEntityId:X4} takes card {itemId} from 0x{opponent.PlayerEntityId:X4}");
            }
        }
        return true;
    }

    private bool Holds(ulong characterId, int itemId) =>
        _items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory).Any(item => item.ItemIndex == itemId);

    private bool CanHold(ulong characterId, IReadOnlyList<int> itemIds)
    {
        var bag = _items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory);
        var newSlots = itemIds
            .Where(itemId => !(_catalog.GetById(itemId) is { IsStackable: true } && bag.Any(item => item.ItemIndex == itemId)))
            .Count();
        return bag.Count + newSlots <= _bagCapacity;
    }

    private BagOperation[] Take(ulong characterId, int itemId)
    {
        var held = _items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory).First(item => item.ItemIndex == itemId);
        var itemClass = _catalog.GetById(itemId)?.ObjectClass ?? held.ItemClass;
        if (held.Stack > 1)
        {
            var remaining = _items.AdjustStack(held.ItemUid, -1);
            return [new BagOperation(QuestResultFrames.StackChanged, itemClass, (uint)itemId, BagItemIds.OnWire(held.ItemUid), remaining)];
        }
        _items.Delete(held.ItemUid);
        return [new BagOperation(QuestResultFrames.Removed, itemClass, (uint)itemId, BagItemIds.OnWire(held.ItemUid), 0)];
    }

    private IEnumerable<HeldCard> HeldCards(ulong characterId) =>
        _items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory)
            .Select(item => (Item: item, Definition: _catalog.GetById(item.ItemIndex)))
            .Where(held => held.Definition is { Type: EquipmentRules.CardType })
            .Select(held => new HeldCard((uint)held.Item.ItemIndex, (uint)held.Definition!.ItemFType, held.Definition.CardNum, held.Item.Stack));

    private Task ReplyAsync(OfficerOrder order, CardBattleStatusOrder update, byte code, CardBattleRewardBlock reward, bool deckReady, CancellationToken ct) =>
        _link.ReportAsync(order with
        {
            OrderType = CardBattleFrames.OrderStatusReply,
            Data = CardBattleFrames.StatusReply(update.CharacterId, update.ClientId, code, update.Winner, reward, deckReady),
        }, ct);

    private BagOperation? Grant(ulong characterId, int itemId)
    {
        var definition = _catalog.GetById(itemId);
        var itemClass = definition?.ObjectClass ?? (byte)ItemParamEntry.StackableClass;
        var bag = _items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory);
        var stack = definition is { IsStackable: true } ? bag.FirstOrDefault(item => item.ItemIndex == itemId) : null;
        if (stack is not null)
        {
            var total = _items.AdjustStack(stack.ItemUid, 1);
            return new BagOperation(QuestResultFrames.StackChanged, itemClass, (uint)itemId, BagItemIds.OnWire(stack.ItemUid), total);
        }
        if (bag.Count >= _bagCapacity) return null;
        var minted = _itemManager.GrantRewardItem(characterId, itemId, 1, itemClassOverride: itemClass);
        return new BagOperation(QuestResultFrames.Created, itemClass, (uint)itemId, BagItemIds.OnWire(minted.ItemUid), minted.Stack, minted.ItemSlot);
    }

    private Task SendAsync(InGameSessionState player, Opcode opcode, byte[] payload)
    {
        byte randKey;
        lock (_rng) randKey = (byte)_rng.Next(1, 256);
        return player.SendFrameAsync(opcode, payload, randKey, default);
    }
}
