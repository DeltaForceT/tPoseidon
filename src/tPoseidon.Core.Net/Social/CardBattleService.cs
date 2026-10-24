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
    private ushort _nextBattle = 1;
    private ushort _nextClient = 1;

    public CardBattleService(ICardBattleStore store, Func<string, CardBattlePlayer?> findNamed, Func<ulong, BattleCard, bool> owns, Func<int, int> roll, Encoding text, SpecialCardSets? sets = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _findNamed = findNamed ?? throw new ArgumentNullException(nameof(findNamed));
        _owns = owns ?? throw new ArgumentNullException(nameof(owns));
        _roll = roll ?? throw new ArgumentNullException(nameof(roll));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _sets = sets ?? SpecialCardSets.None;
    }

    public CardBattleOutbox Make(CardBattlePlayer master, string targetName)
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
            var match = new CardBattleMatch(NextBattleId(), new BattleSide(master.CharacterId, ClientId(master.CharacterId), master.Name, master.Level));
            _byCharacter[master.CharacterId] = match;
            outbox.Notes.Add($"battle {match.BattleId} made");
            AskLocked(outbox, match, master, targetName);
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
            var match = _byCharacter.Values.FirstOrDefault(candidate => candidate.State == BattleState.Asking && candidate.Opponent?.CharacterId == target.CharacterId);
            if (match is null)
            {
                if (yes) outbox.Send(target.CharacterId, CardBattleFrames.AnswerRefused, CardBattleFrames.Code(CardBattleFrames.NoAsk));
                return outbox;
            }
            if (!yes)
            {
                outbox.Send(match.Master.CharacterId, CardBattleFrames.AskRefused, CardBattleFrames.Code(CardBattleFrames.Refused));
                match.Opponent = null;
                outbox.Notes.Add($"battle {match.BattleId}: {target.Name} refused");
                return outbox;
            }
            _byCharacter[target.CharacterId] = match;
            match.State = BattleState.SelectRule;
            outbox.Send(match.Master.CharacterId, CardBattleFrames.Accepted, CardBattleFrames.Named(target.Name, _text));
            var masterStats = _store.Load(match.Master.CharacterId, target.CharacterId);
            var opponentStats = _store.Load(target.CharacterId, match.Master.CharacterId);
            outbox.Send(match.Master.CharacterId, CardBattleFrames.MasterStart, CardBattleFrames.MasterStarted(masterStats, match.Opponent!, opponentStats, _text));
            outbox.Send(target.CharacterId, CardBattleFrames.OpponentStart, CardBattleFrames.OpponentStarted(match, masterStats, opponentStats, _text));
            outbox.Notes.Add($"battle {match.BattleId}: {match.Master.Name} vs {target.Name}, rule choice");
        }
        return outbox;
    }

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
            match.Rule = rule;
            match.State = BattleState.SelectCard;
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
            side.Deck = deck.ToArray();
            side.Set = _sets.Held(side.Deck);
            if (!match.BothDecks) return outbox;
            match.State = BattleState.Battle;
            foreach (var each in new[] { match.Master, match.Opponent! })
            {
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

    public CardBattleOutbox ReserveExit(ulong characterId)
    {
        var outbox = new CardBattleOutbox();
        lock (_gate)
        {
            if (!_byCharacter.TryGetValue(characterId, out var match) || match.State is not (BattleState.Battle or BattleState.Result)) return outbox;
            var side = match.Side(characterId)!;
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
            if (!_byCharacter.TryGetValue(characterId, out var match) || match.State != BattleState.Result) return outbox;
            var side = match.Side(characterId)!;
            side.ResultSeen = true;
            var other = match.Other(side);
            if (other is not null && !other.ResultSeen)
            {
                outbox.Send(characterId, CardBattleFrames.ResultWaiting, Array.Empty<byte>());
                return outbox;
            }
            foreach (var each in new[] { match.Master, match.Opponent }.OfType<BattleSide>())
            {
                outbox.Send(each.CharacterId, CardBattleFrames.ResultClosed, Array.Empty<byte>());
            }
            Remove(match);
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
            }
            outbox.Notes.Add($"battle {match.BattleId}: {side.Name} left ({reason})");
            Remove(match);
        }
        return outbox;
    }

    private void AskLocked(CardBattleOutbox outbox, CardBattleMatch match, CardBattlePlayer asker, string targetName)
    {
        if (match.State != BattleState.Asking)
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
        outbox.Send(target.CharacterId, CardBattleFrames.Asked, CardBattleFrames.Named(asker.Name, _text));
        outbox.Notes.Add($"battle {match.BattleId}: {asker.Name} asks {target.Name}");
    }

    private void ResolveLocked(CardBattleOutbox outbox, CardBattleMatch match)
    {
        var (roundWinner, outcome) = match.Resolve();
        var opponent = match.Opponent!;
        if (outcome == RoundOutcome.Won && match.Winner is { } winner)
        {
            var loser = match.Other(winner)!;
            _store.Record(winner.CharacterId, loser.CharacterId, true);
            _store.Record(loser.CharacterId, winner.CharacterId, false);
            var (winnerPayout, loserPayout) = CardBattleSettlement.Settle(winner, loser, _roll);
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
            outbox.Notes.Add($"battle {match.BattleId}: {winner.Name} wins {winner.Score.Wins}-{loser.Score.Wins}{(outbox.Steal is { } steal ? (steal.TakenFromLoser ? $", takes card {steal.ItemId} out of the loser's bag" : $", takes a copy of card {steal.ItemId}") : string.Empty)}");
        }
        else if (outcome == RoundOutcome.Draw)
        {
            _store.Record(match.Master.CharacterId, opponent.CharacterId, false);
            _store.Record(opponent.CharacterId, match.Master.CharacterId, false);
            outbox.Notes.Add($"battle {match.BattleId}: draw");
        }
        var opcode = outcome == RoundOutcome.Draw ? CardBattleFrames.DrawResult : CardBattleFrames.RoundResult;
        foreach (var each in new[] { match.Master, opponent })
        {
            outbox.Send(each.CharacterId, opcode, CardBattleFrames.Round(roundWinner?.ClientId ?? 0, (byte)match.Round, each, match.Other(each)!));
        }
    }

    public CardBattleOutbox StatusReported(ulong characterId, byte code, CardBattleRewardBlock reward)
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
                Remove(match);
                return outbox;
            }
            side.StatusReported = true;
            side.Reward = reward ?? CardBattleRewardBlock.None;
            if (code == CardBattleFrames.StatusBagFull) outbox.Notes.Add($"battle {match.BattleId}: {side.Name}'s bag is full, the won card was not given");
            if (!match.Master.StatusReported || !opponent.StatusReported) return outbox;
            foreach (var each in new[] { match.Master, opponent })
            {
                var other = match.Other(each)!;
                outbox.Send(each.CharacterId, CardBattleFrames.FinalResult,
                    CardBattleFrames.Final(match, each, _store.Load(each.CharacterId, other.CharacterId), _store.Load(other.CharacterId, each.CharacterId)));
            }
            outbox.Notes.Add($"battle {match.BattleId}: final result sent");
        }
        return outbox;
    }

    private void Remove(CardBattleMatch match)
    {
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
