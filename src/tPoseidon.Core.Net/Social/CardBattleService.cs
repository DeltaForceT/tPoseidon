using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.CardBattle;

namespace tPoseidon.Core.Net.Social;

public sealed record CardBattlePlayer(ulong CharacterId, string Name, int Level);

public sealed record CardBattleSteal(ulong WinnerId, ulong LoserId, uint ItemId, bool TakenFromLoser);

public sealed class CardBattleOutbox
{
    public List<CoupleMessage> Messages { get; } = new();

    public List<string> Notes { get; } = new();

    public CardBattleSteal? Steal { get; set; }

    public List<(ulong CharacterId, ushort OrderType, byte[] Data)> Orders { get; } = new();

    public void Send(ulong to, ushort opcode, byte[] payload) => Messages.Add(new CoupleMessage(to, opcode, payload));
}

public sealed class CardBattleService
{
    private readonly object _gate = new();
    private readonly Dictionary<ulong, CardBattleMatch> _byCharacter = new();
    private readonly Dictionary<ulong, ushort> _clientIds = new();
    private readonly ICardBattleStore _store;
    private readonly Func<string, CardBattlePlayer?> _findNamed;
    private readonly Func<ulong, BattleCard, bool> _owns;
    private readonly Func<int, int> _roll;
    private readonly Encoding _text;
    private readonly SpecialCardSets _sets;
    private readonly CardBattlePoints _points;
    private readonly CardGenerator? _generator;
    private ushort _nextBattle = 1;
    private ushort _nextClient = 1;

    public CardBattleService(ICardBattleStore store, Func<string, CardBattlePlayer?> findNamed, Func<ulong, BattleCard, bool> owns, Func<int, int> roll, Encoding text, SpecialCardSets? sets = null, CardBattlePoints? points = null, CardGenerator? generator = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _findNamed = findNamed ?? throw new ArgumentNullException(nameof(findNamed));
        _owns = owns ?? throw new ArgumentNullException(nameof(owns));
        _roll = roll ?? throw new ArgumentNullException(nameof(roll));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _sets = sets ?? SpecialCardSets.None;
        _points = points ?? CardBattlePoints.Default;
        _generator = generator;
    }

    public CardBattleOutbox Make(CardBattlePlayer master)
    {
        ArgumentNullException.ThrowIfNull(master);
        var outbox = new CardBattleOutbox();
        lock (_gate)
        {
            if (_byCharacter.ContainsKey(master.CharacterId))
            {
                outbox.Send(master.CharacterId, CardBattleFrames.MakeRefused, CardBattleFrames.Code(CardBattleFrames.AlreadyInBattle));
                return outbox;
            }
            var match = new CardBattleMatch(NextBattleId(), new BattleSide(master.CharacterId, ClientId(master.CharacterId), master.Name, master.Level))
            {
                Pending = CardBattleCheckKind.Make,
            };
            _byCharacter[master.CharacterId] = match;
            outbox.Orders.Add((master.CharacterId, CardBattleFrames.OrderCheck, CardBattleFrames.CheckRequest(match.Master.ClientId, match.Master.ClientId, master.CharacterId)));
            outbox.Notes.Add($"battle {match.BattleId} made, checking {master.Name}");
        }
        return outbox;
    }

    public CardBattleOutbox Ask(CardBattlePlayer asker, string targetName)
    {
        ArgumentNullException.ThrowIfNull(asker);
        var outbox = new CardBattleOutbox();
        lock (_gate)
        {
            if (!_byCharacter.TryGetValue(asker.CharacterId, out var match) || match.Master.CharacterId != asker.CharacterId)
            {
                outbox.Send(asker.CharacterId, CardBattleFrames.AskRefused, CardBattleFrames.Code(CardBattleFrames.NoBattle));
                return outbox;
            }
            AskLocked(outbox, match, asker, targetName);
        }
        return outbox;
    }

    public CardBattleOutbox Answer(CardBattlePlayer target, bool yes)
    {
        ArgumentNullException.ThrowIfNull(target);
        var outbox = new CardBattleOutbox();
        lock (_gate)
        {
            var match = _byCharacter.Values.FirstOrDefault(candidate => candidate.State == BattleState.Asking && candidate.Pending == CardBattleCheckKind.None && candidate.Opponent?.CharacterId == target.CharacterId);
            if (match is null)
            {
                if (yes) outbox.Send(target.CharacterId, CardBattleFrames.AnswerRefused, CardBattleFrames.Code(CardBattleFrames.NoAsk));
                return outbox;
            }
            if (!yes)
            {
                outbox.Send(match.Master.CharacterId, CardBattleFrames.AskRefused, CardBattleFrames.Code(CardBattleFrames.Refused));
                outbox.Orders.Add((target.CharacterId, CardBattleFrames.OrderClearState, CardBattleFrames.ClearState(match.Opponent!.ClientId, match.Opponent.ClientId)));
                match.Opponent = null;
                outbox.Notes.Add($"battle {match.BattleId}: {target.Name} refused");
                return outbox;
            }
            _byCharacter[target.CharacterId] = match;
            match.State = BattleState.SelectRule;
            outbox.Send(match.Master.CharacterId, CardBattleFrames.Accepted, CardBattleFrames.Named(target.Name, _text));
            var masterStats = LoadStats(match.Master.CharacterId, target.CharacterId);
            var opponentStats = LoadStats(target.CharacterId, match.Master.CharacterId);
            match.Master.Stats = masterStats;
            match.Opponent!.Stats = opponentStats;
            match.Opponent.StartTimer(CardBattleTimer.Rule);
            outbox.Send(match.Master.CharacterId, CardBattleFrames.MasterStart, CardBattleFrames.MasterStarted(masterStats, match.Opponent!, opponentStats, _text));
            outbox.Send(target.CharacterId, CardBattleFrames.OpponentStart, CardBattleFrames.OpponentStarted(match, masterStats, opponentStats, _text));
            outbox.Notes.Add($"battle {match.BattleId}: {match.Master.Name} vs {target.Name}, rule choice");
        }
        return outbox;
    }

    public CardBattleOutbox CheckReported(ulong characterId, CardBattleCheck check)
    {
        var outbox = new CardBattleOutbox();
        lock (_gate)
        {
            if (_byCharacter.TryGetValue(characterId, out var made) && made.Pending == CardBattleCheckKind.Make && made.Master.CharacterId == characterId)
            {
                made.Pending = CardBattleCheckKind.None;
                Report(made.Master, check);
                if (MakeRefusal(check) is { } code)
                {
                    outbox.Send(characterId, CardBattleFrames.MakeRefused, CardBattleFrames.Code(code));
                    outbox.Notes.Add($"battle {made.BattleId}: {made.Master.Name} refused by the check ({code})");
                    Remove(outbox, made);
                    return outbox;
                }
                outbox.Send(characterId, CardBattleFrames.Made, CardBattleFrames.MadeBattle(made, _text));
                outbox.Notes.Add($"battle {made.BattleId}: {made.Master.Name} checked");
                return outbox;
            }
            var asked = _byCharacter.Values.FirstOrDefault(candidate => candidate.Pending == CardBattleCheckKind.Ask && candidate.Opponent?.CharacterId == characterId);
            if (asked is null) return outbox;
            asked.Pending = CardBattleCheckKind.None;
            Report(asked.Opponent!, check);
            var refusal = !check.DeckValid ? CardBattleFrames.CheckDeckCode : !check.MapAllowed ? CardBattleFrames.CheckMapCode : (byte)0;
            if (refusal != 0)
            {
                outbox.Send(asked.Master.CharacterId, CardBattleFrames.AskRefused, CardBattleFrames.Code(refusal));
                outbox.Orders.Add((characterId, CardBattleFrames.OrderClearState, CardBattleFrames.ClearState(asked.Opponent!.ClientId, asked.Opponent.ClientId)));
                outbox.Notes.Add($"battle {asked.BattleId}: {asked.Opponent.Name} refused by the check ({refusal})");
                asked.Opponent = null;
                return outbox;
            }
            outbox.Send(characterId, CardBattleFrames.Asked, CardBattleFrames.Named(asked.Master.Name, _text));
            outbox.Send(asked.Master.CharacterId, CardBattleFrames.AskSent, Array.Empty<byte>());
            outbox.Notes.Add($"battle {asked.BattleId}: {asked.Master.Name} asks {asked.Opponent!.Name}");
        }
        return outbox;
    }

    public CardBattleOutbox CheckNotSent(ulong characterId)
    {
        var outbox = new CardBattleOutbox();
        lock (_gate)
        {
            if (_byCharacter.TryGetValue(characterId, out var made) && made.Pending == CardBattleCheckKind.Make && made.Master.CharacterId == characterId)
            {
                made.Pending = CardBattleCheckKind.None;
                outbox.Send(characterId, CardBattleFrames.MakeRefused, CardBattleFrames.Code(CardBattleFrames.MakeCheckFailed));
                Remove(outbox, made);
                return outbox;
            }
            var asked = _byCharacter.Values.FirstOrDefault(candidate => candidate.Pending == CardBattleCheckKind.Ask && candidate.Opponent?.CharacterId == characterId);
            if (asked is null) return outbox;
            asked.Pending = CardBattleCheckKind.None;
            asked.Opponent = null;
            outbox.Send(asked.Master.CharacterId, CardBattleFrames.AskRefused, CardBattleFrames.Code(CardBattleFrames.AskCheckFailed));
        }
        return outbox;
    }

    private static void Report(BattleSide side, CardBattleCheck check)
    {
        side.GameLevel = (ushort)check.Level;
        side.GameMoney = check.Money;
        side.GameEntityId = check.EntityId;
    }

    private static byte? MakeRefusal(CardBattleCheck check) =>
        !check.OwnNormal ? CardBattleFrames.CheckOwnStateCode
        : !check.OtherNormal ? CardBattleFrames.CheckTargetStateCode
        : !check.OwnLevel ? CardBattleFrames.CheckOwnLevelCode
        : !check.OtherLevel ? CardBattleFrames.CheckOtherLevelCode
        : !check.DeckValid ? CardBattleFrames.CheckDeckCode
        : !check.MapAllowed ? CardBattleFrames.CheckMapCode
        : check.BagFull ? CardBattleFrames.CheckBagCode
        : null;

    public CardBattleOutbox SelectRule(ulong characterId, byte rule)
    {
        var outbox = new CardBattleOutbox();
        lock (_gate)
        {
            if (!_byCharacter.TryGetValue(characterId, out var match) || match.State != BattleState.SelectRule) return outbox;
            var side = match.Side(characterId)!;
            if (match.Other(side) is not { } other)
            {
                outbox.Send(characterId, CardBattleFrames.RuleRefused, CardBattleFrames.Code(CardBattleFrames.OpponentMissing));
                return outbox;
            }
            side.StopTimer();
            match.Rule = rule;
            match.State = BattleState.SelectCard;
            side.StartTimer(CardBattleTimer.Deck);
            other.StartTimer(CardBattleTimer.Deck);
            outbox.Send(characterId, CardBattleFrames.RuleSelected, new[] { rule });
            outbox.Send(other.CharacterId, CardBattleFrames.OpponentRule, new[] { rule });
            outbox.Notes.Add($"battle {match.BattleId}: rule {rule}");
        }
        return outbox;
    }

    public CardBattleOutbox SubmitDeck(ulong characterId, BattleCard[] deck)
    {
        ArgumentNullException.ThrowIfNull(deck);
        var outbox = new CardBattleOutbox();
        lock (_gate)
        {
            if (!_byCharacter.TryGetValue(characterId, out var match) || match.State != BattleState.SelectCard) return outbox;
            var side = match.Side(characterId)!;
            var code = CardBattleRules.CheckDeck(deck, card => _owns(characterId, card));
            if (code != CardBattleRules.CodeOk)
            {
                outbox.Send(characterId, CardBattleFrames.DeckRefused, CardBattleFrames.Code(code));
                outbox.Notes.Add($"deck refused: {code}");
                return outbox;
            }
            side.StopTimer();
            side.Deck = deck.ToArray();
            side.Set = _sets.Held(side.Deck);
            if (!match.BothDecks)
            {
                outbox.Send(characterId, CardBattleFrames.DeckAccepted, Array.Empty<byte>());
                return outbox;
            }
            match.StartBattle();
            foreach (var each in new[] { match.Master, match.Opponent! })
            {
                each.StartTimer(CardBattleTimer.Drop);
                outbox.Send(each.CharacterId, CardBattleFrames.BattleCards, CardBattleFrames.Cards((byte)match.Round, each, match.Other(each)!));
            }
            outbox.Notes.Add($"battle {match.BattleId}: both decks in");
        }
        return outbox;
    }

    public CardBattleOutbox DropCard(ulong characterId, byte slot, BattleCard card)
    {
        var outbox = new CardBattleOutbox();
        lock (_gate)
        {
            if (!_byCharacter.TryGetValue(characterId, out var match) || match.Side(characterId) is not { } side)
            {
                outbox.Send(characterId, CardBattleFrames.DropRefused, CardBattleFrames.Code(CardBattleRules.CodeNoBattle));
                return outbox;
            }
            var code = match.CheckDrop(side, slot, card);
            if (code != CardBattleRules.CodeOk)
            {
                outbox.Send(characterId, CardBattleFrames.DropRefused, CardBattleFrames.Code(code));
                return outbox;
            }
            side.StopTimer();
            side.Dropped = slot;
            var other = match.Other(side)!;
            outbox.Send(characterId, CardBattleFrames.Dropped, new[] { slot });
            outbox.Send(other.CharacterId, CardBattleFrames.OpponentDropped, new[] { slot });
            if (match.BothDropped) ResolveLocked(outbox, match);
        }
        return outbox;
    }

    public CardBattleOutbox Chance(ulong characterId)
    {
        var outbox = new CardBattleOutbox();
        lock (_gate)
        {
            if (!_byCharacter.TryGetValue(characterId, out var match) || match.State != BattleState.Battle
                || match.Side(characterId) is not { } side || match.Other(side) is not { } other) return outbox;
            var slot = Array.FindIndex(side.Deck, card => card.CardType == CardBattleRules.ChanceCardType);
            if (slot < 0 || side.Dropped is not null || !CardBattleRules.ChanceAvailable(side.ChanceUsed, side.Deck, side.Score.Wins, other.Score.Wins))
            {
                outbox.Send(characterId, CardBattleFrames.ChanceRefused, Array.Empty<byte>());
                return outbox;
            }
            side.StopTimer();
            side.ChanceUsed = true;
            side.Deck[slot] = side.Deck[slot] with { Steal = CardBattleRules.StealFlag };
            side.Dropped = slot;
            outbox.Send(characterId, CardBattleFrames.ChanceDropped, new[] { (byte)slot });
            outbox.Send(other.CharacterId, CardBattleFrames.OpponentChanceDropped, new[] { (byte)slot });
            outbox.Notes.Add($"battle {match.BattleId}: {side.Name} plays the chance card in slot {slot}");
            if (match.BothDropped) ResolveLocked(outbox, match);
        }
        return outbox;
    }

    public CardBattleOutbox Tick(int elapsedMs)
    {
        var outbox = new CardBattleOutbox();
        lock (_gate)
        {
            foreach (var match in _byCharacter.Values.Distinct().ToList())
            {
                foreach (var side in new[] { match.Master, match.Opponent }.OfType<BattleSide>())
                {
                    var opcode = side.Elapse(elapsedMs) switch
                    {
                        CardBattleTimer.Rule => CardBattleFrames.RuleTimeOver,
                        CardBattleTimer.Deck => CardBattleFrames.DeckTimeOver,
                        CardBattleTimer.Drop => CardBattleFrames.DropTimeOver,
                        _ => (ushort)0,
                    };
                    if (opcode == 0) continue;
                    outbox.Send(side.CharacterId, opcode, Array.Empty<byte>());
                    outbox.Notes.Add($"battle {match.BattleId}: {side.Name} timer over (0x{opcode:X4})");
                }
            }
        }
        return outbox;
    }

    public CardBattleOutbox ReserveExit(ulong characterId)
    {
        var outbox = new CardBattleOutbox();
        lock (_gate)
        {
            if (!_byCharacter.TryGetValue(characterId, out var match) || match.State is not (BattleState.Battle or BattleState.Extra or BattleState.Result)) return outbox;
            var side = match.Side(characterId)!;
            if (match.ExitReservedBy == 0) match.ExitReservedBy = side.ClientId;
            side.ExitReserved = true;
            outbox.Send(characterId, CardBattleFrames.ExitReserved, Array.Empty<byte>());
            if (match.Other(side) is { } other) outbox.Send(other.CharacterId, CardBattleFrames.OpponentExitReserved, Array.Empty<byte>());
        }
        return outbox;
    }

    public CardBattleOutbox ChatLine(ulong characterId, string text)
    {
        var outbox = new CardBattleOutbox();
        lock (_gate)
        {
            if (!_byCharacter.TryGetValue(characterId, out var match) || match.Side(characterId) is not { } side) return outbox;
            outbox.Send(characterId, CardBattleFrames.ChatEcho, CardBattleFrames.Named(text, _text));
            if (match.Other(side) is { } other) outbox.Send(other.CharacterId, CardBattleFrames.OpponentChat, CardBattleFrames.Named(text, _text));
        }
        return outbox;
    }

    public CardBattleOutbox ResultOk(ulong characterId)
    {
        var outbox = new CardBattleOutbox();
        lock (_gate)
        {
            if (!_byCharacter.TryGetValue(characterId, out var match) || match.Side(characterId) is not { ResultPending: true } side) return outbox;
            side.ResultSeen = true;
            var other = match.Other(side);
            if (other is not null && !other.ResultSeen)
            {
                outbox.Send(characterId, CardBattleFrames.ResultWaiting, Array.Empty<byte>());
                return outbox;
            }
            foreach (var each in new[] { match.Master, match.Opponent }.OfType<BattleSide>())
            {
                each.ResultPending = false;
                each.ResultSeen = false;
                outbox.Send(each.CharacterId, CardBattleFrames.ResultClosed, Array.Empty<byte>());
            }
            if (match.State == BattleState.Result) Remove(outbox, match);
        }
        return outbox;
    }

    public CardBattleOutbox Leave(ulong characterId, byte reason)
    {
        var outbox = new CardBattleOutbox();
        lock (_gate)
        {
            if (!_byCharacter.TryGetValue(characterId, out var match) || match.Side(characterId) is not { } side) return outbox;
            if (match.Other(side) is { } other && _byCharacter.ContainsKey(other.CharacterId))
            {
                outbox.Send(other.CharacterId, CardBattleFrames.OpponentLeft, CardBattleFrames.Left(reason, side.Name, _text));
                outbox.Send(other.CharacterId, CardBattleFrames.PlayerLeft, CardBattleFrames.ClientId(side.ClientId));
            }
            outbox.Notes.Add($"battle {match.BattleId}: {side.Name} left ({reason})");
            Remove(outbox, match);
        }
        return outbox;
    }

    public CardBattleOutbox Closed(ulong characterId)
    {
        var outbox = new CardBattleOutbox();
        lock (_gate)
        {
            if (!_byCharacter.TryGetValue(characterId, out var match) || match.Side(characterId) is not { } side) return outbox;
            if (match.Other(side) is { } other)
            {
                if (_byCharacter.ContainsKey(other.CharacterId))
                {
                    outbox.Send(other.CharacterId, CardBattleFrames.PlayerClosed, CardBattleFrames.ClientId(side.ClientId));
                }
                else if (ReferenceEquals(side, match.Master) && match.State == BattleState.Asking)
                {
                    outbox.Send(other.CharacterId, CardBattleFrames.OpponentLeft, CardBattleFrames.Left(CardBattleFrames.AskerClosed, side.Name, _text));
                }
            }
            outbox.Notes.Add($"battle {match.BattleId}: {side.Name} disconnected");
            if (match.Round >= CardBattleRules.DisCheckMinRound && match.Other(side) is { } opposite)
            {
                var dis = 1 + (side.ChanceCardItemId != 0 ? 1 : 0);
                var specialDis = side.Set is { Grade: > 0 } ? 1 : 0;
                _store.RecordClosed(side.CharacterId, opposite.CharacterId, dis, specialDis);
                outbox.Notes.Add($"battle {match.BattleId}: {side.Name} dis +{dis}, specialdis +{specialDis}");
            }
            Remove(outbox, match);
        }
        return outbox;
    }

    private void AskLocked(CardBattleOutbox outbox, CardBattleMatch match, CardBattlePlayer asker, string targetName)
    {
        if (match.State != BattleState.Asking || match.Pending != CardBattleCheckKind.None)
        {
            outbox.Send(asker.CharacterId, CardBattleFrames.AskRefused, CardBattleFrames.Code(CardBattleFrames.NotStandby));
            return;
        }
        if (_findNamed(targetName) is not { } target || target.CharacterId == asker.CharacterId)
        {
            outbox.Send(asker.CharacterId, CardBattleFrames.AskRefused, CardBattleFrames.Code(CardBattleFrames.TargetNotFound));
            return;
        }
        if (_byCharacter.ContainsKey(target.CharacterId))
        {
            outbox.Send(asker.CharacterId, CardBattleFrames.AskRefused, CardBattleFrames.Code(CardBattleFrames.AlreadyInBattle));
            return;
        }
        if (_byCharacter.Values.Any(candidate => candidate.Opponent?.CharacterId == target.CharacterId))
        {
            outbox.Send(asker.CharacterId, CardBattleFrames.AskRefused, CardBattleFrames.Code(CardBattleFrames.TargetBusy));
            return;
        }
        match.Opponent = new BattleSide(target.CharacterId, ClientId(target.CharacterId), target.Name, target.Level);
        match.Pending = CardBattleCheckKind.Ask;
        outbox.Orders.Add((target.CharacterId, CardBattleFrames.OrderCheck, CardBattleFrames.CheckRequest(match.Opponent.ClientId, match.Master.ClientId, asker.CharacterId)));
        outbox.Notes.Add($"battle {match.BattleId}: {asker.Name} asks {target.Name}, checking {target.Name}");
    }

    private void ResolveLocked(CardBattleOutbox outbox, CardBattleMatch match)
    {
        var (roundWinner, outcome) = match.Resolve();
        var opponent = match.Opponent!;
        if (outcome == RoundOutcome.Won) SettleLocked(outbox, match);
        var opcode = outcome == RoundOutcome.ExtraCards ? CardBattleFrames.ExtraStart : CardBattleFrames.RoundResult;
        foreach (var each in new[] { match.Master, opponent })
        {
            if (outcome is RoundOutcome.NextRound or RoundOutcome.FinalCard) each.StartTimer(CardBattleTimer.Drop);
            outbox.Send(each.CharacterId, opcode, CardBattleFrames.Round(roundWinner?.ClientId ?? 0, (byte)match.Round, each, match.Other(each)!));
        }
        if (outcome == RoundOutcome.ExtraCards) outbox.Notes.Add($"battle {match.BattleId}: tied after the fifth card, extra cards");
    }

    private void SettleLocked(CardBattleOutbox outbox, CardBattleMatch match)
    {
        var winner = match.Winner!;
        var loser = match.Other(winner)!;
        _store.Record(winner.CharacterId, loser.CharacterId, true, winner.NoExperience ? 0 : CardBattleRules.WinPointsWon(winner.Stats.Grade, loser.Stats.Grade));
        _store.Record(loser.CharacterId, winner.CharacterId, false, loser.NoExperience ? 0 : CardBattleRules.WinPointsLost(loser.Stats.Grade, winner.Stats.Grade));
        var (winnerPayout, loserPayout) = CardBattleSettlement.Settle(winner, loser, _roll, NewCard);
        winner.Payout = winnerPayout;
        loser.Payout = loserPayout;
        if (winnerPayout.WonCardItemId != 0)
        {
            outbox.Steal = new CardBattleSteal(winner.CharacterId, loser.CharacterId, winnerPayout.WonCardItemId, loser.Set is not null);
        }
        foreach (var side in new[] { winner, loser })
        {
            outbox.Orders.Add((side.CharacterId, CardBattleFrames.OrderStatusUpdate, CardBattleFrames.Status(match, side)));
        }
        outbox.Notes.Add($"battle {match.BattleId}: {winner.Name} wins {winner.Score.Wins}-{loser.Score.Wins}{(outbox.Steal is { } steal ? (steal.TakenFromLoser ? $", takes card {steal.ItemId} out of the loser's bag" : $", gets card {steal.ItemId}") : string.Empty)}");
    }

    private uint NewCard() => (uint)(_generator?.Generate()?.ItemId ?? 0);

    public CardBattleOutbox ExtraCard(ulong characterId)
    {
        var outbox = new CardBattleOutbox();
        lock (_gate)
        {
            if (!_byCharacter.TryGetValue(characterId, out var match) || match.State != BattleState.Extra || match.Side(characterId) is not { } side) return outbox;
            var generated = _generator?.Generate();
            if (generated is null)
            {
                outbox.Send(characterId, CardBattleFrames.ExtraCardFailed, CardBattleFrames.Code(CardBattleRules.ExtraCardFailed));
                outbox.Notes.Add($"battle {match.BattleId}: no extra card could be made for {side.Name}");
                return outbox;
            }
            var card = new BattleCard(0, (uint)generated.ItemId, (uint)generated.ItemFType, (byte)generated.CardNum, CardBattleRules.ResultUnused, (uint)generated.MpCon, 0);
            side.Extra = card;
            if (side.ExtraCards.Count < CardBattleRules.MaxExtraCards) side.ExtraCards.Add(card);
            outbox.Send(characterId, CardBattleFrames.ExtraCard, CardBattleFrames.ExtraCardBody(card));
            if (match.Other(side) is { } other) outbox.Send(other.CharacterId, CardBattleFrames.OpponentExtraCard, CardBattleFrames.ExtraCardBody(card));
            outbox.Notes.Add($"battle {match.BattleId}: {side.Name} gets extra card {card.ItemId}");
            if (match.ExtraArrivedFrom == 0 || match.ExtraArrivedFrom == side.ClientId)
            {
                match.ExtraArrivedFrom = side.ClientId;
                return outbox;
            }
            if (!match.BothExtraCards) return outbox;
            var (_, outcome) = match.ResolveExtra();
            if (outcome == RoundOutcome.Won)
            {
                SettleLocked(outbox, match);
                return outbox;
            }
            foreach (var each in new[] { match.Master, match.Opponent! })
            {
                outbox.Send(each.CharacterId, CardBattleFrames.ExtraTie, new[] { (byte)match.Round });
            }
            outbox.Notes.Add($"battle {match.BattleId}: extra cards tied, round {match.Round}");
        }
        return outbox;
    }

    public CardBattleOutbox StatusReported(ulong characterId, byte code, CardBattleRewardBlock reward, bool deckReady = true)
    {
        var outbox = new CardBattleOutbox();
        lock (_gate)
        {
            if (!_byCharacter.TryGetValue(characterId, out var match) || match.State != BattleState.Result || match.Opponent is not { } opponent
                || match.Side(characterId) is not { } side)
            {
                outbox.Send(characterId, CardBattleFrames.BattleError, CardBattleFrames.Code(CardBattleFrames.ErrorNotInBattle));
                outbox.Notes.Add($"status reply {code} for 0x{characterId:X}: no battle in the result state");
                return outbox;
            }
            if (code is CardBattleFrames.StatusNoPlayer or CardBattleFrames.StatusNoOpponent)
            {
                var error = code == CardBattleFrames.StatusNoPlayer ? CardBattleFrames.ErrorNoPlayer : CardBattleFrames.ErrorNoOpponent;
                foreach (var each in new[] { match.Master, opponent }) outbox.Send(each.CharacterId, CardBattleFrames.BattleError, CardBattleFrames.Code(error));
                outbox.Notes.Add($"battle {match.BattleId}: status reply {code}, battle cleared");
                Remove(outbox, match);
                return outbox;
            }
            side.StatusReported = true;
            side.Reward = reward ?? CardBattleRewardBlock.None;
            if (!deckReady) match.NoDeckClient = side.ClientId;
            if (!ReferenceEquals(match.Winner, side)) match.NextChooser = side.ClientId;
            if (code == CardBattleFrames.StatusBagFull) outbox.Notes.Add($"battle {match.BattleId}: {side.Name}'s bag is full, the won card was not given");
            if (!match.Master.StatusReported || !opponent.StatusReported) return outbox;
            foreach (var each in new[] { match.Master, opponent })
            {
                var other = match.Other(each)!;
                outbox.Send(each.CharacterId, CardBattleFrames.FinalResult,
                    CardBattleFrames.Final(match, each, LoadStats(each.CharacterId, other.CharacterId), LoadStats(other.CharacterId, each.CharacterId)));
            }
            outbox.Notes.Add($"battle {match.BattleId}: final result sent");
            foreach (var each in new[] { match.Master, opponent }) each.ResultPending = true;
            if (match.CanContinue) NextBattleLocked(outbox, match);
        }
        return outbox;
    }

    private void NextBattleLocked(CardBattleOutbox outbox, CardBattleMatch match)
    {
        var opponent = match.Opponent!;
        var chooser = match.Master.ClientId == match.NextChooser ? match.Master : opponent;
        match.ResetForNextBattle();
        match.PlayCount++;
        match.Master.Stats = LoadStats(match.Master.CharacterId, opponent.CharacterId);
        opponent.Stats = LoadStats(opponent.CharacterId, match.Master.CharacterId);
        chooser.StartTimer(CardBattleTimer.Rule);
        outbox.Notes.Add($"battle {match.BattleId}: play {match.PlayCount}, {chooser.Name} chooses the rule");
    }

    private CardBattleStats LoadStats(ulong characterId, ulong otherId)
    {
        var stats = _store.Load(characterId, otherId);
        return stats with { Grade = _points.Grade(stats.WinPoint) };
    }

    private void Remove(CardBattleOutbox outbox, CardBattleMatch match)
    {
        foreach (var side in new[] { match.Master, match.Opponent }.OfType<BattleSide>())
        {
            outbox.Orders.Add((side.CharacterId, CardBattleFrames.OrderClearState, CardBattleFrames.ClearState(side.ClientId, side.ClientId)));
        }
        _byCharacter.Remove(match.Master.CharacterId);
        if (match.Opponent is { } opponent && _byCharacter.TryGetValue(opponent.CharacterId, out var joined) && ReferenceEquals(joined, match))
        {
            _byCharacter.Remove(opponent.CharacterId);
        }
    }

    private ushort NextBattleId()
    {
        var id = _nextBattle;
        _nextBattle = _nextBattle == ushort.MaxValue ? (ushort)1 : (ushort)(_nextBattle + 1);
        return id;
    }

    private ushort ClientId(ulong characterId)
    {
        if (_clientIds.TryGetValue(characterId, out var id)) return id;
        id = _nextClient;
        _nextClient = _nextClient == ushort.MaxValue ? (ushort)1 : (ushort)(_nextClient + 1);
        _clientIds[characterId] = id;
        return id;
    }
}
