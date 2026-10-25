using tPoseidon.Core.Data;

namespace tPoseidon.Core.Game.Guardian;

public readonly record struct GuardianExpOutcome(short Level, int Exp, int Gained, int LevelsGained)
{
    public bool LeveledUp => LevelsGained > 0;
}

public static class GuardianGrowth
{
    public const short MaxLevel = 200;
    public const int MaxTableLevel = 400;
    public const int HitExpType = 1;

    public static int Gain(int baseExp, float ratio) => baseExp <= 0 ? 0 : (int)(baseExp * ratio);

    public static int Threshold(IReadOnlyDictionary<int, int> nextExpByLevel, int level)
    {
        ArgumentNullException.ThrowIfNull(nextExpByLevel);
        return level is >= 1 and <= MaxTableLevel && nextExpByLevel.TryGetValue(level, out var next) ? next : 0;
    }

    public static GuardianExpOutcome? AddExp(GuardianState guardian, int gained, IReadOnlyDictionary<int, int> nextExpByLevel)
    {
        ArgumentNullException.ThrowIfNull(guardian);
        ArgumentNullException.ThrowIfNull(nextExpByLevel);
        if (guardian.Level >= MaxLevel) return null;
        var level = (int)guardian.Level;
        var exp = (long)guardian.Exp + gained;
        var threshold = Threshold(nextExpByLevel, level);
        if (threshold == 0) return null;
        var levels = 0;
        while (threshold > 0 && exp >= threshold && level < MaxLevel)
        {
            exp -= threshold;
            level++;
            levels++;
            threshold = Threshold(nextExpByLevel, level);
        }
        if (threshold == 0 && level < MaxLevel) return null;
        if (level >= MaxLevel) exp = 0;
        return new GuardianExpOutcome((short)level, (int)Math.Min(exp, int.MaxValue), gained, levels);
    }

    public static GuardianState Apply(GuardianState guardian, GuardianExpOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(guardian);
        return guardian with
        {
            Level = outcome.Level,
            Exp = outcome.Exp,
            SkillPoint = guardian.SkillPoint + outcome.LevelsGained,
        };
    }
}
