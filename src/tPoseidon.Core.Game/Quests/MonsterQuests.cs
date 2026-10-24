using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Quests;

public readonly record struct MonsterQuestGrade(int Monster, int Count, int Minutes, int MinLevel, int MaxLevel, int Exp, int Tmxp)
{
    public int TimeLimitMs => Minutes * MonsterQuests.MillisecondsPerMinute;
}

public static class MonsterQuests
{
    public const int DefaultMaxCount = 25;
    public const int MillisecondsPerMinute = 60_000;
    public const int TimerStepMs = 1_000;
    public const int Slots = 1;
    public const int NoGrade = -1;

    public static IReadOnlyList<MonsterQuestGrade> Grades(MonsterQuestInfoEntry quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        var columns = new[]
        {
            Grade(quest.SrcMonster0, quest.Cnt0, quest.Time0, quest.MinLv0, quest.MaxLv0, quest.Exp0, quest.Tmxp0),
            Grade(quest.SrcMonster1, quest.Cnt1, quest.Time1, quest.MinLv1, quest.MaxLv1, quest.Exp1, quest.Tmxp1),
            Grade(quest.SrcMonster2, quest.Cnt2, quest.Time2, quest.MinLv2, quest.MaxLv2, quest.Exp2, quest.Tmxp2),
            Grade(quest.SrcMonster3, quest.Cnt3, quest.Time3, quest.MinLv3, quest.MaxLv3, quest.Exp3, quest.Tmxp3),
            Grade(quest.SrcMonster4, quest.Cnt4, quest.Time4, quest.MinLv4, quest.MaxLv4, quest.Exp4, quest.Tmxp4),
        };
        var used = columns.Count(grade => grade.Monster != 0);
        return columns.Take(used).ToArray();
    }

    public static int MaxCountOf(MonsterQuestInfoEntry quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        return quest.MaxCount < 1 ? DefaultMaxCount : quest.MaxCount & 0xFF;
    }

    public static bool IsLevelValid(MonsterQuestInfoEntry quest, int level)
    {
        ArgumentNullException.ThrowIfNull(quest);
        var min = Math.Max(0, quest.MinLevel);
        var max = Math.Max(min, Math.Max(0, quest.MaxLevel));
        if (min < 1 && max < 1) return true;
        return level >= min && level <= max;
    }

    public static int GradeForLevel(IReadOnlyList<MonsterQuestGrade> grades, int level)
    {
        ArgumentNullException.ThrowIfNull(grades);
        for (var grade = 0; grade < grades.Count; grade++)
        {
            if (grades[grade].MinLevel <= level && level <= grades[grade].MaxLevel) return grade;
        }
        return NoGrade;
    }

    public static int GradeOfMonster(IReadOnlyList<MonsterQuestGrade> grades, int monster)
    {
        ArgumentNullException.ThrowIfNull(grades);
        for (var grade = 0; grade < grades.Count; grade++)
        {
            if (grades[grade].Monster == monster) return grade;
        }
        return NoGrade;
    }

    public static MonsterQuestResultEntry? ResultRow(IReadOnlyList<MonsterQuestResultEntry?> rows, int grade)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count == 0) return null;
        return grade < 0 || grade >= rows.Count ? rows[^1] : rows[grade];
    }

    private static MonsterQuestGrade Grade(int monster, int count, int minutes, int minLevel, int maxLevel, int exp, int tmxp) =>
        new(monster, (short)count, (short)minutes, (short)minLevel, (short)maxLevel, Math.Max(0, exp), tmxp);
}
