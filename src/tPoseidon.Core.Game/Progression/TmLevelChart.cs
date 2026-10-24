namespace tPoseidon.Core.Game.Progression;

public sealed class TmLevelChart
{
    public const int FirstLevel = 0;
    public const int LastLevel = 500;
    public const int NoSuchLevel = -1;

    private readonly int[] _nextTmExp = new int[LastLevel + 1];

    public TmLevelChart(IEnumerable<(int Level, int NextTmExp)> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        foreach (var (level, nextTmExp) in rows)
        {
            if (level < FirstLevel || level > LastLevel) continue;
            _nextTmExp[level] = nextTmExp;
        }
    }

    public int NextTmExp(int tmLevel) =>
        tmLevel >= FirstLevel && tmLevel <= LastLevel ? _nextTmExp[tmLevel] : NoSuchLevel;
}
