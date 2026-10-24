using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Guardian;

public readonly record struct GuardianExpResult(int Level, int Exp, int LevelsGained);

public readonly record struct GuardianSkillPointCheck(int SkillPoint, bool Reset);

public static class GuardianRules
{
    public const int LevelCap = 200;
    public const int FirstTableLevel = 1;
    public const int LastTableLevel = 400;
    public const double LevelScale = 0.01;
    public const int AccuracyIndex = 1;
    public const int MagicAttackIndex = 4;
    public const int AccuracyAndMagicBonus = 50;
    public const int MaxSkillLevel = 10;
    public const int MinSkillLevel = 1;
    public const int BoostedLevelBonus = 50;
    public const double FirstRareMultiplier = 1.3;
    public const double SecondRareMultiplier = 1.2;
    public const int FirstRareType = 1;
    public const int SecondRareType = 2;

    public static int NextExp(GuardianTables tables, int level)
    {
        ArgumentNullException.ThrowIfNull(tables);
        if (level < FirstTableLevel || level > LastTableLevel) return 0;
        return tables.NextExpByLevel.TryGetValue(level, out var next) ? next : 0;
    }

    public static GuardianExpResult AddExp(GuardianTables tables, int level, int exp, int gained)
    {
        ArgumentNullException.ThrowIfNull(tables);
        if (level >= LevelCap || gained <= 0) return new GuardianExpResult(level, exp, 0);
        var total = (long)exp + gained;
        var startLevel = level;
        while (true)
        {
            var threshold = NextExp(tables, level);
            if (threshold <= 0 || total < threshold) break;
            total -= threshold;
            level++;
            if (level >= LevelCap)
            {
                level = LevelCap;
                total = 0;
                break;
            }
        }
        return new GuardianExpResult(level, (int)Math.Min(total, int.MaxValue), level - startLevel);
    }

    public static int[] Stats(IReadOnlyList<short> accumulated, int level, IReadOnlyList<float> growthRatios)
    {
        ArgumentNullException.ThrowIfNull(accumulated);
        ArgumentNullException.ThrowIfNull(growthRatios);
        if (accumulated.Count != GuardianTables.StatCount || growthRatios.Count != GuardianTables.StatCount)
        {
            throw new ArgumentException($"A guardian has {GuardianTables.StatCount} stats.");
        }
        var stats = new int[GuardianTables.StatCount];
        for (var i = 0; i < stats.Length; i++)
        {
            var basePoint = Math.Round(accumulated[i] * level * LevelScale);
            stats[i] = (int)Math.Round(basePoint * growthRatios[i]);
        }
        stats[AccuracyIndex] += AccuracyAndMagicBonus;
        stats[MagicAttackIndex] += AccuracyAndMagicBonus;
        return stats;
    }

    public static GuardianSkillPointCheck CheckSkillPoints(int level, int skillPoint, IEnumerable<int> cardLevels)
    {
        ArgumentNullException.ThrowIfNull(cardLevels);
        var spent = cardLevels.Sum(cardLevel => Math.Max(0, cardLevel - MinSkillLevel));
        return skillPoint + spent == level
            ? new GuardianSkillPointCheck(skillPoint, false)
            : new GuardianSkillPointCheck(level, true);
    }

    public static int StartingLevel(int powerPoint, int magicPoint, int sensePoint, int charmPoint) =>
        Math.Max(1, powerPoint + magicPoint + sensePoint + charmPoint);

    public static int BasicStatLevel(int ownerLevel, bool boosted, int rareType)
    {
        var effective = ownerLevel + (boosted ? BoostedLevelBonus : 0);
        var multiplier = rareType switch
        {
            FirstRareType => FirstRareMultiplier,
            SecondRareType => SecondRareMultiplier,
            _ => 1.0,
        };
        return (int)Math.Round(effective * multiplier);
    }

    public static short[]? BasicPoints(GuardianTables tables, int basicLevel)
    {
        ArgumentNullException.ThrowIfNull(tables);
        return tables.BasicStats.TryGetValue(basicLevel, out var row)
            ? row.Points.Select(point => (short)Math.Clamp(point, short.MinValue, short.MaxValue)).ToArray()
            : null;
    }
}
