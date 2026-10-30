namespace tPoseidon.Core.Game.Progression;

public enum LevelSetOutcome
{
    Raise,
    SameLevel,
    LowerRefused,
    InvalidLevel,
}

public readonly record struct LevelSetPlan(LevelSetOutcome Outcome, int TargetLevel, long ExpToGain);

public static class LevelSetRules
{
    public static LevelSetPlan Plan(int currentLevel, ulong currentExp, int requestedLevel, LevelExpChart chart)
    {
        ArgumentNullException.ThrowIfNull(chart);
        if (requestedLevel < LevelExpChart.FirstLevel) return new LevelSetPlan(LevelSetOutcome.InvalidLevel, currentLevel, 0);
        var target = Math.Min(requestedLevel, LevelExpChart.LastLevel);
        if (target < currentLevel) return new LevelSetPlan(LevelSetOutcome.LowerRefused, currentLevel, 0);
        if (target == currentLevel) return new LevelSetPlan(LevelSetOutcome.SameLevel, currentLevel, 0);

        var needed = (long)chart.NextExp(currentLevel) - (long)currentExp;
        for (var level = currentLevel + 1; level < target; level++) needed += chart.NextExp(level);
        return new LevelSetPlan(LevelSetOutcome.Raise, target, Math.Max(needed, 0L));
    }
}
