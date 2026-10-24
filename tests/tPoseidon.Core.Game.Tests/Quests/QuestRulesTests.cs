using tPoseidon.Core.Game.Quests;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Quests;

public sealed class QuestRulesTests
{
    private static QuestResultEntry Row(int count, int gelder) => new() { QuestId = 9001, Count = count, Gelder = gelder };

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 10)]
    [InlineData(2, 10)]
    [InlineData(3, 30)]
    [InlineData(9, 50)]
    public void TheResultRowIsTheLargestCountNotAboveTheEntryCount(int entryCount, int gelder)
    {
        var rows = new[] { Row(1, 10), Row(3, 30), Row(5, 50) };

        Assert.Equal(gelder, QuestRules.ResultRow(rows, entryCount)?.Gelder);
    }

    [Fact]
    public void WithoutAMatchingRowTheFirstRowIsUsed()
    {
        var rows = new[] { Row(2, 20), Row(4, 40) };

        Assert.Equal(20, QuestRules.ResultRow(rows, 1)?.Gelder);
        Assert.Null(QuestRules.ResultRow(Array.Empty<QuestResultEntry>(), 1));
    }

    [Theory]
    [InlineData(0, 0, 99, true)]
    [InlineData(10, 20, 9, false)]
    [InlineData(10, 20, 20, true)]
    [InlineData(10, 20, 21, false)]
    [InlineData(1, 0, 5, false)]
    public void TheLevelRangeAppliesWhenEitherBoundIsSet(int minLevel, int maxLevel, int level, bool valid)
    {
        var quest = new QuestInfoEntry { Id = 9001, Name = "q", MinLevel = minLevel, MaxLevel = maxLevel };

        Assert.Equal(valid, QuestRules.IsLevelValid(quest, level));
    }

    [Theory]
    [InlineData(0, 25)]
    [InlineData(1, 1)]
    [InlineData(0x1FF, 0xFF)]
    public void MaxCountDefaultsToTwentyFiveAndIsOneByte(int column, int maxCount)
    {
        Assert.Equal(maxCount, QuestRules.MaxCountOf(new QuestInfoEntry { Id = 9001, Name = "q", MaxCount = column }));
    }

    [Theory]
    [InlineData(999, false)]
    [InlineData(1000, true)]
    [InlineData(1022, true)]
    [InlineData(1023, false)]
    [InlineData(1076, false)]
    [InlineData(1077, true)]
    [InlineData(1999, true)]
    [InlineData(2000, false)]
    public void ScenarioMessageRangesFollowTheServer(int questId, bool scenario)
    {
        Assert.Equal(scenario, QuestRules.UsesScenarioMessages(questId));
    }

    [Fact]
    public void AScenarioQuestIsKnownByItsTable()
    {
        Assert.True(QuestRules.IsScenario(new QuestInfoEntry { Id = 1, Name = "s", SourceTable = QuestRules.ScenarioTable }));
        Assert.False(QuestRules.IsScenario(new QuestInfoEntry { Id = 2, Name = "n", SourceTable = "QuestInfo" }));
    }

    [Fact]
    public void ASingleOnlyRewardIsGivenOnceAndAnEmptyCountIsDropped()
    {
        var items = new InMemoryItemParamLookup(new[]
        {
            new ItemParamEntry { Id = 501, Class = 1, Type = 14 },
            new ItemParamEntry { Id = 502, Class = 1, Type = 14, Options = QuestTurnIn.SingleOnlyOption },
            new ItemParamEntry { Id = 503, Class = 1, Type = 14 },
        });

        var rewards = QuestRules.RewardItems(new[] { (501, 3), (502, 0), (503, 0) }, items);

        Assert.Equal(new[] { new RewardItem(501, 3), new RewardItem(502, 1) }, rewards);
    }
}
