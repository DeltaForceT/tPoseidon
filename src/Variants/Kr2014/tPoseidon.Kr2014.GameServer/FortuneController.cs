using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Fortune;
using tPoseidon.Core.Game.Quests;
using tPoseidon.Core.InterServer;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.World;

internal sealed class FortuneHold
{
    public required FortuneInfo Info { get; set; }
    public FortuneBonus Bonus { get; set; } = FortuneBonus.None;
    public bool Active { get; set; }
    public DateTimeOffset AppliedAt { get; set; }
    public FortuneRound Round { get; } = new();
    public bool DayChecked { get; set; }

    public IReadOnlyList<int> ActiveStats => Active ? Bonus.Stats : FortuneBonus.None.Stats;

    public int ActiveRefine => Active ? Bonus.Refine : 0;

    public FortuneBlock Block() => new((uint)Math.Max(0, Info.ViewCount), AppliedAt, Info.ExpiresAt, (uint)Info.TitleId, Bonus);
}

internal sealed class FortuneController
{
    public const ushort FortuneProfileTitleType = 2;
    public const int NotEnoughMoney = 1;
    public const byte TodayExchangeWithoutTable = 0;

    private readonly MasterFortune _master;
    private readonly Func<FortuneTables> _tables;
    private readonly Func<IReadOnlyList<int>> _basicCards;
    private readonly IItemStore _items;
    private readonly IItemParamLookup _catalog;
    private readonly Func<InGameSessionState, int, Task> _seedEvent;
    private readonly string _logTag;
    private readonly Random _rng = new();
    private readonly object _rngGate = new();

    public FortuneController(
        MasterFortune master,
        Func<FortuneTables> tables,
        Func<IReadOnlyList<int>> basicCards,
        IItemStore items,
        IItemParamLookup catalog,
        Func<InGameSessionState, int, Task> seedEvent,
        string logTag)
    {
        _master = master ?? throw new ArgumentNullException(nameof(master));
        _tables = tables ?? throw new ArgumentNullException(nameof(tables));
        _basicCards = basicCards ?? throw new ArgumentNullException(nameof(basicCards));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _seedEvent = seedEvent ?? throw new ArgumentNullException(nameof(seedEvent));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public async Task<FortuneHold?> LoadAsync(ulong characterId, DateTimeOffset now, CancellationToken ct)
    {
        var (loaded, stored) = await _master.LoadAsync(characterId, ct).ConfigureAwait(false);
        if (!loaded) return null;
        var info = stored ?? new FortuneInfo
        {
            CharacterId = characterId,
            UpdatedAt = FortuneHouse.NoRecordDate,
            ExpiresAt = FortuneHouse.NoRecordDate,
        };
        var hold = new FortuneHold { Info = info, AppliedAt = now };
        if (info.ItemAttrString.Length >= FortuneRules.UsesResetLength) hold.Info = info with { ViewCount = 0 };
        if (!FortuneRules.IsActive(now, info.ExpiresAt))
        {
            hold.Info = hold.Info with { ItemAttrString = string.Empty };
            return hold;
        }
        hold.Bonus = FortuneRules.Parse(hold.Info.ItemAttrString);
        hold.Active = true;
        Console.WriteLine($"{_logTag} [fortune] char 0x{characterId:X16} award '{hold.Info.ItemAttrString}' until {info.ExpiresAt:u}, title {info.TitleId}");
        return hold;
    }

    public static bool ClearsProfileTitle(FortuneHold? hold, ushort profileTitleType) =>
        hold is { Active: false } && profileTitleType == FortuneProfileTitleType;

    public static byte[]? AppliedFrame(FortuneHold? hold) =>
        hold is { Active: true } ? FortuneFrames.Applied(FortuneFrames.AppliedByAward, hold.Block()) : null;

    public async Task<bool> StarTearsAsync(InGameSessionState player, ItemParamEntry definition, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(definition);
        var extension = (long)definition.Life * FortuneRules.SecondsPerHour;
        if (extension <= 0 || player.Fortune is not { Active: true } hold) return false;
        hold.Info = hold.Info with { ExpiresAt = hold.Info.ExpiresAt.AddSeconds(extension) };
        await _master.UpdateAsync(hold.Info, ct).ConfigureAwait(false);
        await SendAsync(player, Opcode.S2C_FORTUNE_APPLIED, FortuneFrames.Applied(FortuneFrames.AppliedByStarTears, hold.Block())).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} [fortune] 0x{player.PlayerEntityId:X4} award extended to {hold.Info.ExpiresAt:u}");
        return true;
    }

    public Task SendAppliedAsync(InGameSessionState player) =>
        AppliedFrame(player.Fortune) is { } frame ? SendAsync(player, Opcode.S2C_FORTUNE_APPLIED, frame) : Task.CompletedTask;

    public async Task<int> OpenAsync(InGameSessionState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Fortune is not { } hold || player.Stats is not { } stats)
        {
            Log(player, "house refused: no fortune info");
            return NpcScript.Refused;
        }
        var fee = FortuneHouse.Fee(_tables().FeeByUse, hold.Info.ViewCount + 1);
        if (stats.Money < fee) return NotEnoughMoney;
        await EnterAsync(player, hold, stats.Money).ConfigureAwait(false);
        return NpcScript.Silent;
    }

    public async Task ChangeAsync(InGameSessionState player, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Fortune is not { Round.InHouse: true } hold || player.Character is not { } character) return;
        if (!FortuneFrames.TryReadChange(payload.Span, out var request))
        {
            Log(player, $"card change of {payload.Length} bytes not read");
            return;
        }
        var tables = _tables();
        var bag = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
        var jokers = new List<FortuneJoker>(request.JokerUids.Count);
        foreach (var wireUid in request.JokerUids)
        {
            var uid = BagItemIds.Stored(wireUid);
            var held = bag.FirstOrDefault(item => item.ItemUid == uid);
            if (held is null || _catalog.GetById(held.ItemIndex) is not { Type: FortuneHouse.JokerItemType })
            {
                Log(player, $"card change refused: item 0x{wireUid:X} is not a joker card in the bag");
                return;
            }
            if (!tables.Cards.ContainsKey(held.ItemIndex))
            {
                Log(player, $"card change refused: item {held.ItemIndex} has no FortuneCardParam row");
                return;
            }
            var listed = hold.Round.JokerUids.Count(listedUid => listedUid == wireUid) + request.JokerUids.Count(asked => asked == wireUid);
            if (listed > held.Stack)
            {
                Log(player, $"card change refused: item 0x{wireUid:X} listed {listed} times, {held.Stack} held");
                return;
            }
            jokers.Add(new FortuneJoker(wireUid, (uint)held.ItemIndex));
        }
        if (hold.Round.Change(jokers, request.Indices) is { } refusal)
        {
            Log(player, $"card change refused: {refusal} (step {hold.Round.Step})");
            return;
        }
        await SendAsync(player, Opcode.S2C_FORTUNE_CARDS_CHANGED, FortuneFrames.CardsChanged(hold.Round.Cards)).ConfigureAwait(false);
        Log(player, $"places {jokers.Count} joker cards: {string.Join(",", jokers.Select(joker => joker.ItemId))}");
    }

    public async Task RollAsync(InGameSessionState player, ReadOnlyMemory<byte> payload, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Fortune is not { Round.InHouse: true } hold || player.Character is not { } character || player.Stats is not { } stats) return;
        if (hold.Round.Step != FortuneStep.Changed)
        {
            Log(player, $"roll refused: step {hold.Round.Step}");
            return;
        }
        if (!FortuneFrames.TryReadRoll(payload.Span, out var request))
        {
            Log(player, $"roll of {payload.Length} bytes not read");
            return;
        }
        var tables = _tables();
        var uses = hold.Info.ViewCount + 1;
        var fee = FortuneHouse.Fee(tables.FeeByUse, uses);
        if (stats.Money < fee)
        {
            Log(player, $"roll refused: money {stats.Money} below the fee {fee}");
            return;
        }
        var needle = tables.Rules.TryGetValue(FortuneTables.NeedleSelectCardSeed, out var needleSeed) ? needleSeed : 0f;
        var myCard = tables.Rules.TryGetValue(FortuneTables.MyCardSelectSeed, out var myCardSeed) ? myCardSeed : 0f;
        FortuneDraw before;
        FortuneDraw final;
        lock (_rngGate)
        {
            before = FortuneRules.Draw(request.Power, request.Picks, hold.Round.Cards, needle, myCard,
                card => tables.Cards.TryGetValue((int)card.CardId, out var row) ? row.Prob : 0f, _rng.Next);
            var ranges = FortuneHouse.JokerRanges(tables.CardsInRowOrder);
            final = FortuneHouse.ExchangeJokers(before, current => FortuneHouse.JokerCard(ranges, _rng.Next) ?? current);
        }
        var award = FortuneRules.Award(final.Results, tables);
        hold.Round.Rolled(award);
        hold.Info = hold.Info with { ViewCount = uses, UpdatedAt = now };

        await _seedEvent(player, FortuneHouse.RollEvent).ConfigureAwait(false);
        if (award.ThreeOfAKind) await _seedEvent(player, FortuneHouse.ThreeOfAKindEvent).ConfigureAwait(false);

        stats.GrantMoney(-fee, tPoseidon.Core.Game.Stats.StatChangeSource.Fortune);
        var paid = new List<FortunePaidJoker>(hold.Round.JokerUids.Count);
        foreach (var wireUid in hold.Round.JokerUids)
        {
            if (TakeOne(BagItemIds.Stored(wireUid)) is { } operation) paid.Add(new FortunePaidJoker(operation, wireUid));
            else Log(player, $"joker item 0x{wireUid:X} left the bag before the roll");
        }
        await SendAsync(player, Opcode.S2C_FORTUNE_PAID, FortuneFrames.Paid(character.CharacterId, stats.Money, paid)).ConfigureAwait(false);
        await SendAsync(player, Opcode.S2C_FORTUNE_ROLLED, FortuneFrames.Rolled(new FortuneRollFrame(
            award.ThreeOfAKind, before.Results, final.Results, award.DurationSeconds, (uint)award.TitleId, (uint)uses, award.Text))).ConfigureAwait(false);
        Log(player, $"roll {uses}: power {request.Power}, picks {string.Join(",", request.Picks)}, cards {string.Join(",", final.Results.Select(result => result.CardId))}{(final.JokerHit ? " (joker)" : string.Empty)}, award '{award.Text}', {award.DurationSeconds} s, title {award.TitleId}{(award.TitleDetermined ? string.Empty : " (title rules not determined)")}, fee {fee}");
    }

    public async Task AgainAsync(InGameSessionState player, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Fortune is not { Round.InHouse: true } hold || player.Stats is not { } stats) return;
        if (hold.Round.Step != FortuneStep.Rolled)
        {
            Log(player, $"play again refused: step {hold.Round.Step}");
            return;
        }
        if (!await DealAsync(player, hold, stats.Money).ConfigureAwait(false)) return;
        await _master.UpdateAsync(hold.Info, ct).ConfigureAwait(false);
        Log(player, $"plays again (use {hold.Info.ViewCount}); the last roll is discarded");
    }

    public async Task LeaveAsync(InGameSessionState player, DateTimeOffset now, Action recompute, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(recompute);
        if (player.Fortune is not { Round.InHouse: true } hold) return;
        if (hold.Round.Step is not (FortuneStep.Rolled or FortuneStep.Dealt))
        {
            Log(player, $"[!!Exception!!] LeaveTrickFortuneHouse() at step {hold.Round.Step}");
            return;
        }
        if (hold.Round is { Step: FortuneStep.Rolled, Pending: { } award })
        {
            hold.Info = hold.Info with
            {
                TitleId = award.TitleId,
                ItemAttrString = award.Text,
                ExpiresAt = now.AddSeconds(award.DurationSeconds),
            };
            hold.AppliedAt = now;
            hold.Bonus = FortuneRules.Parse(award.Text);
            hold.Active = FortuneRules.IsActive(now, hold.Info.ExpiresAt);
            recompute();
            await SendAsync(player, Opcode.GAME_ENTITY_ANNOUNCE_3D1, FortuneFrames.TitleAnnounce(player.PlayerEntityId, (uint)award.TitleId)).ConfigureAwait(false);
            await SendAsync(player, Opcode.S2C_FORTUNE_APPLIED, FortuneFrames.Applied(FortuneFrames.AppliedByAward, hold.Block())).ConfigureAwait(false);
            foreach (var eventType in FortuneHouse.StatEvents(hold.Bonus)) await _seedEvent(player, eventType).ConfigureAwait(false);
            await _master.UpdateAsync(hold.Info, ct).ConfigureAwait(false);
            Log(player, $"award '{award.Text}' applied until {hold.Info.ExpiresAt:u}, title {award.TitleId}");
        }
        hold.Round.Close();
        await SendAsync(player, Opcode.S2C_FORTUNE_LEFT, Array.Empty<byte>()).ConfigureAwait(false);
        Log(player, "leaves the fortune house");
    }

    private async Task EnterAsync(InGameSessionState player, FortuneHold hold, uint money)
    {
        if (!await DealAsync(player, hold, money).ConfigureAwait(false)) return;
        Log(player, $"enters the fortune house (use {hold.Info.ViewCount})");
    }

    private async Task<bool> DealAsync(InGameSessionState player, FortuneHold hold, uint money)
    {
        var tables = _tables();
        var nextFee = FortuneHouse.Fee(tables.FeeByUse, hold.Info.ViewCount + 1);
        if (money < nextFee)
        {
            Log(player, $"deal refused: money {money} below the next fee {nextFee}");
            return false;
        }
        FortuneCard[] cards;
        lock (_rngGate) cards = FortuneHouse.Deal(_basicCards(), _rng.Next);
        var entering = hold.Round.Step == FortuneStep.Closed;
        hold.Round.Deal(cards);
        if (entering && !hold.DayChecked)
        {
            hold.DayChecked = true;
            if (FortuneHouse.IsAnotherDay(hold.Info.UpdatedAt, DateTimeOffset.UtcNow)) hold.Info = hold.Info with { ViewCount = 0 };
        }
        var uses = (byte)Math.Clamp(hold.Info.ViewCount, 0, byte.MaxValue);
        await SendAsync(player, Opcode.S2C_FORTUNE_CARDS, FortuneFrames.Cards(uses, TodayExchangeWithoutTable, cards)).ConfigureAwait(false);
        return true;
    }

    private BagOperation? TakeOne(ulong uid)
    {
        if (_items.FindByUid(uid) is not { } held) return null;
        var itemClass = _catalog.GetById(held.ItemIndex)?.ObjectClass ?? held.ItemClass;
        if (held.Stack > 1)
        {
            var left = _items.AdjustStack(uid, -1);
            return new BagOperation(QuestResultFrames.StackChanged, itemClass, (uint)held.ItemIndex, BagItemIds.OnWire(uid), (ushort)left);
        }
        _items.Delete(uid);
        return new BagOperation(QuestResultFrames.Removed, itemClass, (uint)held.ItemIndex, BagItemIds.OnWire(uid), 0);
    }

    private void Log(InGameSessionState player, string message) =>
        Console.WriteLine($"{_logTag} [fortune] 0x{player.PlayerEntityId:X4} {message}");

    private Task SendAsync(InGameSessionState player, Opcode opcode, byte[] payload)
    {
        byte randKey;
        lock (_rngGate) randKey = (byte)_rng.Next(1, 256);
        return player.SendFrameAsync(opcode, payload, randKey, default);
    }
}
