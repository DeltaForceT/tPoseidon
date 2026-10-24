using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.CardBattle;

public readonly record struct BattleCard(ulong ItemUid, uint ItemId, uint CardType, byte Number, uint Result, uint TieBreak, byte Steal)
{
    public const int Size = 26;
    public const int ItemIdShift = 1;
    public const int CardTypeShift = 2;
    public const int NumberShift = 3;
    public const int ResultShift = 4;
    public const int TieBreakShift = 5;

    public BattleCard Encoded() => this with
    {
        ItemId = ItemId << ItemIdShift,
        CardType = CardType << CardTypeShift,
        Number = (byte)(Number << NumberShift),
        Result = Result << ResultShift,
        TieBreak = TieBreak << TieBreakShift,
    };
}

public sealed class ScoreBlock
{
    public const int Size = 7;

    public byte Wins { get; set; }
    public short Score { get; set; }
    public short LastDelta { get; set; }
    public byte Streak { get; set; }
    public byte ChanceAvailable { get; set; }
}

public enum BattleState
{
    Asking,
    SelectRule,
    SelectCard,
    Battle,
    Result,
}

public enum RoundOutcome
{
    NextRound,
    ExtraRound,
    Draw,
    Won,
}

public static class CardBattleRules
{
    public const int DeckSize = 5;
    public const byte MinNumber = 1;
    public const byte MaxNumber = 15;
    public const uint MaxCardType = 5;
    public const uint ChanceCardType = 5;
    public const int MaxSameNumber = 2;
    public const int MaxSameType = 2;
    public const int RegularRounds = 4;
    public const int StealPercent = 10;
    public const uint ResultUnused = 0;
    public const uint ResultWon = 1;
    public const uint ResultLost = 2;
    public const uint ResultDraw = 3;
    public const byte StealFlag = 1;
    public const int PerfectWins = 3;
    public const ushort FirstPlayCount = 1;

    public const byte CodeOk = 0;
    public const byte CodeNoBattle = 1;
    public const byte CodeDuplicate = 0x0C;
    public const byte CodeNoItem = 0x0D;
    public const byte CodeBadNumber = 0x0E;
    public const byte CodeUsedResult = 0x0F;
    public const byte CodeCardMismatch = 0x11;
    public const byte CodeBadSlot = 0x12;

    public static byte CheckDeck(IReadOnlyList<BattleCard> deck, Func<BattleCard, bool> owned)
    {
        ArgumentNullException.ThrowIfNull(deck);
        ArgumentNullException.ThrowIfNull(owned);
        if (deck.Count != DeckSize) return CodeNoItem;
        foreach (var card in deck)
        {
            if (card.ItemId == 0) return CodeNoItem;
            if (card.Number < MinNumber || card.Number > MaxNumber) return CodeBadNumber;
            if (card.Result != ResultUnused) return CodeUsedResult;
            if (card.CardType > MaxCardType || !owned(card)) return CodeDuplicate;
        }
        if (deck.Select(card => card.ItemUid).Distinct().Count() != deck.Count) return CodeDuplicate;
        if (deck.GroupBy(card => card.Number).Any(group => group.Count() > MaxSameNumber)) return CodeDuplicate;
        if (deck.GroupBy(card => card.CardType).Any(group => group.Count() > MaxSameType)) return CodeDuplicate;
        return CodeOk;
    }

    public static int Compare(byte rule, BattleCard a, BattleCard b)
    {
        if (a.Number == b.Number) return a.TieBreak == b.TieBreak ? 0 : a.TieBreak > b.TieBreak ? 1 : -1;
        var sameType = a.CardType == b.CardType;
        var lowerWins = rule == 0 ? sameType : !sameType;
        var aLower = a.Number < b.Number;
        return aLower == lowerWins ? 1 : -1;
    }

    public static bool ChanceAvailable(bool chanceUsed, IReadOnlyList<BattleCard> deck, byte ownWins, byte otherWins)
    {
        ArgumentNullException.ThrowIfNull(deck);
        if (chanceUsed || !deck.Any(card => card.CardType == ChanceCardType && card.Result == ResultUnused)) return false;
        return (ownWins, otherWins) is (0, 1) or (1, 2) or (0, 2);
    }

    public static bool Steals(int roll) => roll < StealPercent;

    public const int MinDeckKinds = 3;

    public static bool CanStillBuildDeck(IEnumerable<HeldCard> bag, uint lostCard, uint secondLostCard)
    {
        ArgumentNullException.ThrowIfNull(bag);
        var types = new int[MaxCardType + 1];
        var numbers = new int[MaxNumber + 1];
        var chanceNumbers = new int[MaxNumber + 1];
        var chances = 0;
        var regular = 0;
        foreach (var card in bag)
        {
            if (card.Count == 1 && (card.ItemId == lostCard || card.ItemId == secondLostCard)) continue;
            var number = card.Number is >= MinNumber and <= MaxNumber ? card.Number : 0;
            if (card.CardType == ChanceCardType)
            {
                if (number != 0) chanceNumbers[number]++;
                chances++;
                continue;
            }
            if (card.CardType is 0 or > MaxCardType) continue;
            if (types[card.CardType] < MaxSameType) types[card.CardType]++;
            if (number != 0 && numbers[number] < MaxSameNumber) numbers[number]++;
            regular++;
        }
        if (regular + chances < DeckSize) return false;
        var typeKinds = types.Count(count => count > 0);
        var typeTotal = types.Sum();
        var numberKinds = numbers.Count(count => count > 0);
        var numberTotal = numbers.Sum();
        var chanceFillsNumber = Enumerable.Range(MinNumber, MaxNumber).FirstOrDefault(n => numbers[n] < 1 && chanceNumbers[n] > 0);
        if (chanceFillsNumber != 0)
        {
            numberTotal++;
            numberKinds++;
        }
        else if (Enumerable.Range(MinNumber, MaxNumber).Any(n => numbers[n] < MaxSameNumber))
        {
            numberTotal++;
        }
        if (chances > 0)
        {
            typeTotal++;
            typeKinds++;
        }
        return typeTotal >= DeckSize && typeKinds >= MinDeckKinds && numberTotal >= DeckSize && numberKinds >= MinDeckKinds;
    }
}

public readonly record struct HeldCard(uint ItemId, uint CardType, int Number, int Count);

public sealed class BattleSide
{
    public BattleSide(ulong characterId, ushort clientId, string name, int level)
    {
        CharacterId = characterId;
        ClientId = clientId;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Level = level;
    }

    public ulong CharacterId { get; }
    public ushort ClientId { get; }
    public string Name { get; }
    public int Level { get; }
    public BattleCard[] Deck { get; set; } = Array.Empty<BattleCard>();
    public int? Dropped { get; set; }
    public bool ChanceUsed { get; set; }
    public bool ExitReserved { get; set; }
    public bool ResultSeen { get; set; }
    public bool StatusReported { get; set; }
    public CardSpecialSetEntry? Set { get; set; }
    public CardBattlePayout Payout { get; set; }

    public uint ChanceCardItemId => ChanceUsed
        ? Deck.FirstOrDefault(card => card.CardType == CardBattleRules.ChanceCardType && card.Steal == CardBattleRules.StealFlag).ItemId
        : 0;
    public CardBattleRewardBlock Reward { get; set; } = CardBattleRewardBlock.None;
    public ScoreBlock Score { get; } = new();
}

public sealed class CardBattleMatch
{
    public CardBattleMatch(ushort battleId, BattleSide master)
    {
        BattleId = battleId;
        Master = master ?? throw new ArgumentNullException(nameof(master));
    }

    public ushort BattleId { get; }
    public BattleSide Master { get; }
    public BattleSide? Opponent { get; set; }
    public BattleState State { get; set; } = BattleState.Asking;
    public byte Rule { get; set; }
    public int Round { get; private set; }
    public BattleSide? Winner { get; private set; }

    public BattleSide? Side(ulong characterId) =>
        Master.CharacterId == characterId ? Master : Opponent?.CharacterId == characterId ? Opponent : null;

    public BattleSide? Other(BattleSide side) => ReferenceEquals(side, Master) ? Opponent : Master;

    public bool BothDecks => Master.Deck.Length == CardBattleRules.DeckSize && Opponent?.Deck.Length == CardBattleRules.DeckSize;

    public bool BothDropped => Master.Dropped is not null && Opponent?.Dropped is not null;

    public byte CheckDrop(BattleSide side, int slot, BattleCard card)
    {
        ArgumentNullException.ThrowIfNull(side);
        if (State != BattleState.Battle) return CardBattleRules.CodeNoBattle;
        if (slot < 0 || slot >= side.Deck.Length) return CardBattleRules.CodeBadSlot;
        var held = side.Deck[slot];
        if (held.ItemUid != card.ItemUid || held.ItemId != card.ItemId || held.Result != CardBattleRules.ResultUnused || side.Dropped is not null) return CardBattleRules.CodeCardMismatch;
        return CardBattleRules.CodeOk;
    }

    public (BattleSide? RoundWinner, RoundOutcome Outcome) Resolve()
    {
        if (Opponent is not { } opponent || Master.Dropped is not { } masterSlot || opponent.Dropped is not { } opponentSlot) throw new InvalidOperationException("Both cards are needed.");
        var masterCard = Master.Deck[masterSlot];
        var opponentCard = opponent.Deck[opponentSlot];
        var compared = CardBattleRules.Compare(Rule, masterCard, opponentCard);
        BattleSide? winner = compared > 0 ? Master : compared < 0 ? opponent : null;
        if (winner is null)
        {
            Master.Deck[masterSlot] = masterCard with { Result = CardBattleRules.ResultDraw };
            opponent.Deck[opponentSlot] = opponentCard with { Result = CardBattleRules.ResultDraw };
            Master.Score.Streak = 0;
            opponent.Score.Streak = 0;
            Master.Score.LastDelta = 0;
            opponent.Score.LastDelta = 0;
        }
        else
        {
            var loser = Other(winner)!;
            var winnerCard = winner == Master ? masterCard : opponentCard;
            var difference = (short)Math.Abs(masterCard.Number - opponentCard.Number);
            winner.Score.Score += difference;
            loser.Score.Score -= difference;
            winner.Score.LastDelta = difference;
            loser.Score.LastDelta = (short)-difference;
            winner.Score.Wins++;
            winner.Score.Streak++;
            loser.Score.Streak = 0;
            if (winnerCard.Steal == CardBattleRules.StealFlag && loser.Score.Wins > 0) loser.Score.Wins--;
            if (winner.Score.Streak > 1) winner.Score.Score += (short)(winner.Set is null ? winner.Score.Streak : winner.Score.Streak * winner.Score.Streak);
            Master.Deck[masterSlot] = masterCard with { Result = winner == Master ? CardBattleRules.ResultWon : CardBattleRules.ResultLost };
            opponent.Deck[opponentSlot] = opponentCard with { Result = winner == opponent ? CardBattleRules.ResultWon : CardBattleRules.ResultLost };
        }
        Master.Dropped = null;
        opponent.Dropped = null;
        var outcome = Advance();
        foreach (var side in new[] { Master, opponent })
        {
            var other = Other(side)!;
            side.Score.ChanceAvailable = CardBattleRules.ChanceAvailable(side.ChanceUsed, side.Deck, side.Score.Wins, other.Score.Wins) ? (byte)1 : (byte)0;
        }
        return (winner, outcome);
    }

    private RoundOutcome Advance()
    {
        var opponent = Opponent!;
        var extraRoundPlayed = Round >= CardBattleRules.RegularRounds;
        if (!extraRoundPlayed)
        {
            Round++;
            if (Round < CardBattleRules.RegularRounds) return RoundOutcome.NextRound;
        }
        if (Master.Score.Wins == opponent.Score.Wins)
        {
            if (!extraRoundPlayed) return RoundOutcome.ExtraRound;
            State = BattleState.Result;
            return RoundOutcome.Draw;
        }
        Winner = Master.Score.Wins > opponent.Score.Wins ? Master : opponent;
        State = BattleState.Result;
        return RoundOutcome.Won;
    }
}
