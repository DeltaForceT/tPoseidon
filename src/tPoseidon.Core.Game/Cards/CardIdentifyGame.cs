using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Cards;

public readonly record struct CardIdentifyCard(int ItemId, ushort Group);

public readonly record struct CardIdentifyPair(bool Matched, int ItemId, bool LifeChanged, bool Ends);

public readonly record struct CardIdentifyTake(int ItemId, bool LifeChanged);

public readonly record struct CardIdentifyReward(uint Exp, uint TmExp);

public sealed class CardIdentifyGame
{
    public const int Positions = 7;
    public const int Picks = 4;
    public const int Groups = 4;
    public const int MaxGrade = 12;
    public const int NoSpecial = -1;
    public const short NoCombo = -1;
    public const byte FaceDown = 0;
    public const byte FaceUp = 1;
    public const byte Taken = 2;
    public const short LifeStep = 5;
    public const short PairLife = 2;
    public const short SpecialLife = 5;
    public const short MissLife = 5;
    public const short EndBelowLife = 5;
    public const int LowCardValue = 9;
    public const int LowCardLifeBase = 32;
    public const int ScorePerStage = 10;
    public const double ExpLevelDivisor = 1500.0;
    public const double TmExpLevelDivisor = 1200.0;
    public static readonly int[] SpecialChancePercent = [0, 30, 25, 20];
    public const int PercentRoll = 100;

    private readonly CardIdentifyCard[] _cards = new CardIdentifyCard[Positions];
    private readonly byte[] _states = new byte[Positions];

    public short Life { get; private set; }

    public short Combo { get; private set; } = NoCombo;

    public short Stage { get; private set; }

    public int SpecialPosition { get; private set; } = NoSpecial;

    public int Score { get; private set; }

    public bool Dealt { get; private set; }

    public CardIdentifyCard Card(int position) => _cards[position];

    public byte State(int position) => _states[position];

    public static short StartLife(int cardValue) => (short)(cardValue < LowCardValue ? LowCardLifeBase - 2 * cardValue : 2 * cardValue);

    public static int SpecialChance(short life)
    {
        var steps = life / LifeStep;
        return steps >= 0 && steps < SpecialChancePercent.Length ? SpecialChancePercent[steps] : 0;
    }

    public bool Deal(int cardValue, CardIdentifyTables tables, Func<int, int> next)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(next);
        Stage++;
        if (Stage == 1) Life = StartLife(cardValue);
        var grade = Math.Min((int)Stage, MaxGrade);
        if (!tables.Grades.TryGetValue(grade, out var row) || !tables.Results.TryGetValue(row.Table0, out var results)) return false;
        var distinct = results.Where(result => result.Rate > 0).Select(result => result.ItemId).Distinct().Count();
        if (distinct < Picks) return false;
        Array.Clear(_cards);
        Array.Clear(_states);
        var placed = new HashSet<int>();
        var single = next(Picks);
        for (var pick = 0; pick < Picks; pick++)
        {
            var group = (ushort)(next(Groups) + 1);
            int itemId;
            do itemId = Roll(results, next);
            while (placed.Contains(itemId));
            placed.Add(itemId);
            var copies = pick == single ? 1 : 2;
            for (var copy = 0; copy < copies; copy++) _cards[FreePosition(next)] = new CardIdentifyCard(itemId, group);
        }
        SpecialPosition = next(PercentRoll) < SpecialChance(Life) ? next(Positions) : NoSpecial;
        Dealt = true;
        return true;
    }

    public CardIdentifyCard? Flip(int position)
    {
        if (!Dealt || position < 0 || position >= Positions || _states[position] != FaceDown) return null;
        _states[position] = FaceUp;
        return _cards[position];
    }

    public CardIdentifyPair? Pair(int first, int second, IReadOnlyList<int> comboRatios)
    {
        ArgumentNullException.ThrowIfNull(comboRatios);
        if (!Dealt || first == second || !Valid(first) || !Valid(second) || _states[first] != FaceUp || _states[second] != FaceUp) return null;
        if (_cards[first].ItemId != _cards[second].ItemId)
        {
            _states[first] = FaceDown;
            _states[second] = FaceDown;
            Life -= MissLife;
            Combo = NoCombo;
            return new CardIdentifyPair(false, 0, true, Life < EndBelowLife);
        }
        _states[first] = Taken;
        _states[second] = Taken;
        Life += first == SpecialPosition || second == SpecialPosition ? SpecialLife : PairLife;
        Combo++;
        var ratio = comboRatios.Count == 0 ? 1 : comboRatios[Math.Clamp((int)Combo, 0, comboRatios.Count - 1)];
        Score += Stage * ScorePerStage * ratio;
        return new CardIdentifyPair(true, _cards[first].ItemId, true, false);
    }

    public CardIdentifyTake? Take(int position)
    {
        if (!Dealt || !Valid(position) || _states[position] != FaceUp) return null;
        _states[position] = Taken;
        var special = position == SpecialPosition;
        if (special) Life += SpecialLife;
        return new CardIdentifyTake(_cards[position].ItemId, special);
    }

    public static CardIdentifyReward Reward(int score, int level, float expRatio, float tmExpRatio)
    {
        if (score <= 0) return default;
        var exp = Math.Truncate((1 - level / ExpLevelDivisor) * score * level * expRatio);
        var tmExp = Math.Truncate((1 - level / TmExpLevelDivisor) * score * level * tmExpRatio);
        return new CardIdentifyReward((uint)Math.Clamp(exp, 0, uint.MaxValue), (uint)Math.Clamp(tmExp, 0, uint.MaxValue));
    }

    private static bool Valid(int position) => position >= 0 && position < Positions;

    private static int Roll(IReadOnlyList<CardIdentifyResultEntry> results, Func<int, int> next)
    {
        var total = results.Sum(result => Math.Max(0, result.Rate));
        var roll = next(total);
        var cumulative = 0;
        foreach (var result in results)
        {
            cumulative += Math.Max(0, result.Rate);
            if (cumulative > roll) return result.ItemId;
        }
        return results[^1].ItemId;
    }

    private int FreePosition(Func<int, int> next)
    {
        var free = Enumerable.Range(0, Positions).Where(position => _cards[position].ItemId == 0).ToList();
        return free[next(free.Count)];
    }
}
