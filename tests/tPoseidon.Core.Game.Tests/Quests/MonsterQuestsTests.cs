using tPoseidon.Core.Game.Quests;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Quests;

public sealed class MonsterQuestsTests
{
    private static MonsterQuestInfoEntry Quest(int maxCount = 1, int minLevel = 1, int maxLevel = 400) => new()
    {
        Id = 9001,
        Name = "hunt",
        MinLevel = minLevel,
        MaxLevel = maxLevel,
        SrcMonster0 = 3001, Cnt0 = 5, Time0 = 10, MinLv0 = 1, MaxLv0 = 20, Exp0 = 100, Tmxp0 = 90,
        SrcMonster1 = 3002, Cnt1 = 8, Time1 = 12, MinLv1 = 21, MaxLv1 = 40, Exp1 = -5, Tmxp1 = 180,
        MaxCount = maxCount,
    };

    [Fact]
    public void GradesAreTheFilledMonsterColumns()
    {
        var grades = MonsterQuests.Grades(Quest());

        Assert.Equal(2, grades.Count);
        Assert.Equal(new MonsterQuestGrade(3001, 5, 10, 1, 20, 100, 90), grades[0]);
        Assert.Equal(600_000, grades[0].TimeLimitMs);
    }

    [Fact]
    public void ANegativeGradeExpCountsAsZero()
    {
        Assert.Equal(0, MonsterQuests.Grades(Quest())[1].Exp);
    }

    [Fact]
    public void GradesAreReadFromTheFirstColumnsEvenWhenAGapComesFirst()
    {
        var quest = new MonsterQuestInfoEntry { Id = 9002, Name = "gap", SrcMonster1 = 3005, Cnt1 = 3 };

        var grades = MonsterQuests.Grades(quest);

        Assert.Single(grades);
        Assert.Equal(0, grades[0].Monster);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(20, 0)]
    [InlineData(21, 1)]
    [InlineData(40, 1)]
    [InlineData(41, MonsterQuests.NoGrade)]
    public void TheGradeIsTheFirstWhoseLevelsHoldThePlayer(int level, int grade)
    {
        Assert.Equal(grade, MonsterQuests.GradeForLevel(MonsterQuests.Grades(Quest()), level));
    }

    [Fact]
    public void TheGradeOfAStoredQuestIsFoundByItsMonster()
    {
        var grades = MonsterQuests.Grades(Quest());

        Assert.Equal(1, MonsterQuests.GradeOfMonster(grades, 3002));
        Assert.Equal(MonsterQuests.NoGrade, MonsterQuests.GradeOfMonster(grades, 4000));
    }

    [Theory]
    [InlineData(0, 0, 1, true)]
    [InlineData(10, 20, 9, false)]
    [InlineData(10, 20, 15, true)]
    [InlineData(10, 0, 10, true)]
    [InlineData(10, 0, 11, false)]
    [InlineData(-3, 5, 5, true)]
    public void TheQuestLevelRangeRaisesItsMaximumToItsMinimum(int minLevel, int maxLevel, int level, bool valid)
    {
        Assert.Equal(valid, MonsterQuests.IsLevelValid(Quest(minLevel: minLevel, maxLevel: maxLevel), level));
    }

    [Theory]
    [InlineData(0, 25)]
    [InlineData(-1, 25)]
    [InlineData(3, 3)]
    [InlineData(0x105, 5)]
    public void MaxCountDefaultsToTwentyFiveAndIsOneByte(int column, int maxCount)
    {
        Assert.Equal(maxCount, MonsterQuests.MaxCountOf(Quest(maxCount: column)));
    }

    [Fact]
    public void AGradeOutsideTheResultRowsUsesTheLastRow()
    {
        var rows = new MonsterQuestResultEntry?[]
        {
            new() { TableName = "r", Grade = 0, Gelder = 10 },
            new() { TableName = "r", Grade = 1, Gelder = 20 },
        };

        Assert.Equal(10, MonsterQuests.ResultRow(rows, 0)?.Gelder);
        Assert.Equal(20, MonsterQuests.ResultRow(rows, 7)?.Gelder);
        Assert.Equal(20, MonsterQuests.ResultRow(rows, -1)?.Gelder);
        Assert.Null(MonsterQuests.ResultRow(Array.Empty<MonsterQuestResultEntry?>(), 0));
    }
}
