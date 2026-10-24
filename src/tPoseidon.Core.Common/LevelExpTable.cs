namespace tPoseidon.Core.Common;

public sealed class LevelExpTable
{
    private readonly Dictionary<int, ulong> _expToNext;
    private readonly Dictionary<int, ulong> _tmxpToNext;

    public int MinLevel { get; }
    public int MaxLevelWithData { get; }
    public int MaxTmLevelWithData { get; }

    private LevelExpTable(Dictionary<int, ulong> expToNext, Dictionary<int, ulong> tmxpToNext)
    {
        _expToNext = expToNext;
        _tmxpToNext = tmxpToNext;
        MinLevel = expToNext.Count > 0 ? expToNext.Keys.Min() : 1;
        MaxLevelWithData = expToNext.Count > 0 ? expToNext.Keys.Max() : 1;
        MaxTmLevelWithData = tmxpToNext.Count > 0 ? tmxpToNext.Keys.Max() : 0;
    }

    public ulong? ExpToNextLevel(int currentLevel)
    {
        return _expToNext.TryGetValue(currentLevel, out var v) ? v : null;
    }

    public ulong? TmXpToNextLevel(int currentTmLevel)
    {
        return _tmxpToNext.TryGetValue(currentTmLevel, out var v) ? v : null;
    }

    public static LevelExpTable FromInlineThresholds(
        IReadOnlyDictionary<int, ulong> expToNext,
        IReadOnlyDictionary<int, ulong>? tmxpToNext = null)
    {
        return new LevelExpTable(
            new Dictionary<int, ulong>(expToNext),
            tmxpToNext is null ? new() : new Dictionary<int, ulong>(tmxpToNext));
    }

}
