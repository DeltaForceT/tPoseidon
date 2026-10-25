using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Cards;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.World;

internal sealed class CardIdentifyController
{
    private readonly Func<CardIdentifyTables> _tables;
    private readonly IItemStore _items;
    private readonly IItemParamLookup _catalog;
    private readonly ItemManager _itemManager;
    private readonly GroundItemField _groundItems;
    private readonly Func<ushort> _newEntityId;
    private readonly Func<ushort, Opcode, byte[], Task> _sendToMap;
    private readonly Func<InGameSessionState, Opcode, byte[], Task> _sendToViewers;
    private readonly int _bagCapacity;
    private readonly string _logTag;
    private readonly Random _rng = new();
    private readonly object _rngGate = new();

    public CardIdentifyController(
        Func<CardIdentifyTables> tables,
        IItemStore items,
        IItemParamLookup catalog,
        ItemManager itemManager,
        GroundItemField groundItems,
        Func<ushort> newEntityId,
        Func<ushort, Opcode, byte[], Task> sendToMap,
        Func<InGameSessionState, Opcode, byte[], Task> sendToViewers,
        int bagCapacity,
        string logTag)
    {
        _tables = tables ?? throw new ArgumentNullException(nameof(tables));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _itemManager = itemManager ?? throw new ArgumentNullException(nameof(itemManager));
        _groundItems = groundItems ?? throw new ArgumentNullException(nameof(groundItems));
        _newEntityId = newEntityId ?? throw new ArgumentNullException(nameof(newEntityId));
        _sendToMap = sendToMap ?? throw new ArgumentNullException(nameof(sendToMap));
        _sendToViewers = sendToViewers ?? throw new ArgumentNullException(nameof(sendToViewers));
        _bagCapacity = bagCapacity;
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public static void CardUsed(InGameSessionState player, ItemParamEntry card)
    {
        ArgumentNullException.ThrowIfNull(player);
        player.PendingMonsterCard = card;
    }

    public async Task StartAsync(InGameSessionState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.CardIdentify) return;
        player.CardIdentify = true;
        player.CardGame = new CardIdentifyGame();
        await SendAsync(player, Opcode.S2C_CARD_IDENTIFY_STARTED, Array.Empty<byte>()).ConfigureAwait(false);
        await _sendToViewers(player, Opcode.S2C_CARD_IDENTIFY_MARKER_SHOWN, CardIdentifyFrames.Marker(player.PlayerEntityId)).ConfigureAwait(false);
        Log(player, "starts");
    }

    public async Task DealAsync(InGameSessionState player, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!CardIdentifyFrames.TryReadCard(payload.Span, out var cardItemId) || !player.CardIdentify || player.CardGame is not { } game) return;
        if (player.PendingMonsterCard is not { } card || card.Id != (int)cardItemId)
        {
            Log(player, $"deal with card {cardItemId} refused: no such monster card was used since the last deal");
            return;
        }
        player.PendingMonsterCard = null;
        bool dealt;
        lock (_rngGate) dealt = game.Deal(card.CardNum, _tables(), _rng.Next);
        if (!dealt)
        {
            Log(player, $"stage {game.Stage}: no CardIdentify_GradeInfo / ResultTable rows to deal from");
            return;
        }
        await SendAsync(player, Opcode.S2C_CARD_IDENTIFY_LIFE, CardIdentifyFrames.Life(game.Life)).ConfigureAwait(false);
        await SendAsync(player, Opcode.S2C_CARD_IDENTIFY_DEALT, CardIdentifyFrames.Dealt(game.Stage, game.SpecialPosition)).ConfigureAwait(false);
        Log(player, $"stage {game.Stage} dealt with card {card.Id} (value {card.CardNum}), life {game.Life}, special {game.SpecialPosition}");
    }

    public async Task FlipAsync(InGameSessionState player, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!CardIdentifyFrames.TryReadPosition(payload.Span, out var position) || !player.CardIdentify || player.CardGame is not { } game) return;
        if (game.Flip(position) is not { } card) return;
        await SendAsync(player, Opcode.S2C_CARD_IDENTIFY_FLIPPED, CardIdentifyFrames.Flipped((uint)card.ItemId, card.Group, (ushort)position)).ConfigureAwait(false);
    }

    public async Task PairAsync(InGameSessionState player, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!CardIdentifyFrames.TryReadPair(payload.Span, out var first, out var second) || !player.CardIdentify || player.CardGame is not { } game) return;
        if (game.Pair(first, second, _tables().ComboRatios) is not { } outcome) return;
        if (!outcome.Matched)
        {
            await SendAsync(player, Opcode.S2C_CARD_IDENTIFY_LIFE, CardIdentifyFrames.Life(game.Life)).ConfigureAwait(false);
            await SendAsync(player, Opcode.S2C_CARD_IDENTIFY_MISSED, Array.Empty<byte>()).ConfigureAwait(false);
            Log(player, $"cards {first} / {second} differ, life {game.Life}");
            if (outcome.Ends) await EndAsync(player).ConfigureAwait(false);
            return;
        }
        var dropped = await GiveAsync(player, outcome.ItemId).ConfigureAwait(false);
        await SendAsync(player, Opcode.S2C_CARD_IDENTIFY_LIFE, CardIdentifyFrames.Life(game.Life)).ConfigureAwait(false);
        await SendAsync(player, Opcode.S2C_CARD_IDENTIFY_MATCHED, CardIdentifyFrames.Matched((uint)outcome.ItemId, game.Combo, (uint)Math.Max(0, game.Score), dropped)).ConfigureAwait(false);
        Log(player, $"pair of item {outcome.ItemId}: combo {game.Combo}, score {game.Score}, life {game.Life}{(dropped ? ", dropped at the feet" : string.Empty)}");
    }

    public async Task TakeAsync(InGameSessionState player, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!CardIdentifyFrames.TryReadPosition(payload.Span, out var position) || !player.CardIdentify || player.CardGame is not { } game) return;
        if (game.Take(position) is not { } taken) return;
        await GiveAsync(player, taken.ItemId).ConfigureAwait(false);
        if (taken.LifeChanged) await SendAsync(player, Opcode.S2C_CARD_IDENTIFY_LIFE, CardIdentifyFrames.Life(game.Life)).ConfigureAwait(false);
        await SendAsync(player, Opcode.S2C_CARD_IDENTIFY_TAKEN, CardIdentifyFrames.Taken((uint)taken.ItemId, true)).ConfigureAwait(false);
        Log(player, $"single card {position} taken: item {taken.ItemId}, life {game.Life}");
    }

    public async Task EndAsync(InGameSessionState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!player.CardIdentify) return;
        var game = player.CardGame;
        player.CardIdentify = false;
        player.CardGame = null;
        player.PendingMonsterCard = null;
        var score = game?.Score ?? 0;
        var tables = _tables();
        var level = player.Stats?.Level ?? 0;
        var reward = CardIdentifyGame.Reward(score, level, tables.ExpRatio, tables.TmExpRatio);
        var frames = new List<(Opcode Opcode, byte[] Payload)>();
        if (reward.Exp > 0 && player.GainRewardExp is { } gainExp)
        {
            frames.AddRange(gainExp((int)Math.Min(reward.Exp, int.MaxValue)).Select(frame => (frame.Opcode, frame.Payload)));
        }
        if (reward.TmExp > 0 && player.GainRewardTmExp is { } gainTm) frames.AddRange(gainTm((int)Math.Min(reward.TmExp, int.MaxValue)));
        foreach (var (opcode, framePayload) in frames) await SendAsync(player, opcode, framePayload).ConfigureAwait(false);
        await _sendToViewers(player, Opcode.S2C_CARD_IDENTIFY_MARKER_HIDDEN, CardIdentifyFrames.Marker(player.PlayerEntityId)).ConfigureAwait(false);
        await SendAsync(player, Opcode.CARD_IDENTIFY_END, CardIdentifyFrames.Ended((uint)Math.Max(0, score), reward.Exp, reward.TmExp)).ConfigureAwait(false);
        Log(player, $"CardIdentifyEnd() - score {score}, exp {reward.Exp}, TM exp {reward.TmExp} (level {level}, {game?.Stage ?? 0} stages)");
    }

    private async Task<bool> GiveAsync(InGameSessionState player, int itemId)
    {
        if (player.Character is not { } character || _catalog.GetById(itemId) is not { } definition)
        {
            Log(player, $"reward item {itemId} has no item row");
            return false;
        }
        var bag = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
        var stack = definition.IsStackable ? bag.FirstOrDefault(held => held.ItemIndex == itemId) : null;
        BagOperation operation;
        if (stack is not null)
        {
            var total = _items.AdjustStack(stack.ItemUid, 1);
            operation = new BagOperation(QuestResultFrames.StackChanged, definition.ObjectClass, (uint)itemId, BagItemIds.OnWire(stack.ItemUid), total);
        }
        else if (bag.Count < _bagCapacity)
        {
            var minted = _itemManager.GrantRewardItem(character.CharacterId, itemId, 1, itemClassOverride: definition.ObjectClass);
            operation = new BagOperation(QuestResultFrames.Created, definition.ObjectClass, (uint)itemId, BagItemIds.OnWire(minted.ItemUid), minted.Stack, minted.ItemSlot);
        }
        else
        {
            var item = new FieldItem(_newEntityId(), player.MapId, player.PlayerX, player.PlayerY, itemId, 1, definition.ObjectClass, false,
                player.PlayerEntityId, player.PlayerEntityId, DateTimeOffset.UtcNow);
            _groundItems.Add(item);
            await _sendToMap(player.MapId, Opcode.S2C_OP_000C_TBD, GroundItemFrames.Dropped(item)).ConfigureAwait(false);
            return true;
        }
        await SendAsync(player, Opcode.S2C_ITEM_OPERATION, QuestResultFrames.ItemOperation(operation)).ConfigureAwait(false);
        return false;
    }

    private void Log(InGameSessionState player, string message) =>
        Console.WriteLine($"{_logTag} [card identify] 0x{player.PlayerEntityId:X4} {message}");

    private Task SendAsync(InGameSessionState player, Opcode opcode, byte[] payload)
    {
        byte randKey;
        lock (_rngGate) randKey = (byte)_rng.Next(1, 256);
        return player.SendFrameAsync(opcode, payload, randKey, default);
    }
}
