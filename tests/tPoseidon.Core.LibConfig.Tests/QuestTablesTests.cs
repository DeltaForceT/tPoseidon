using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.LibConfig.Tests;

public class QuestTablesTests
{
    private static readonly string KrLibConfigPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "data", "libconfig_kr_en.xml"));

    private static bool KrAvailable => File.Exists(KrLibConfigPath);

    [SkippableFact]
    public void TheFirstHuntOfTheBeachTutorialLoadsWithItsResultRow()
    {
        Skip.IfNot(KrAvailable, $"KR libconfig not at {KrLibConfigPath}");
        var reader = new LibConfigXmlReader(KrLibConfigPath);

        var quest = reader.LoadMonsterQuestInfo().Single(q => q.Id == 520);
        var results = new InMemoryMonsterQuestResultLookup(reader.LoadMonsterQuestResultTables());
        var rows = results.RowsOf(quest.ResultTable);

        Assert.Equal((2185, 5, 10, 1), (quest.SrcMonster0, quest.Cnt0, quest.Time0, quest.MaxCount));
        Assert.Equal(0, quest.SrcMonster1);
        Assert.Equal("MonsterQuestResult_520", quest.ResultTable);
        Assert.Single(rows);
        Assert.Equal((0, 104, 5), (rows[0]!.Type, rows[0]!.Item0, rows[0]!.Cnt0));
        Assert.False(string.IsNullOrWhiteSpace(rows[0]!.Message(0)));
    }

    [SkippableFact]
    public void TheLifeguardHoldsItsQuestSlotsAndItsGreetingCheck()
    {
        Skip.IfNot(KrAvailable, $"KR libconfig not at {KrLibConfigPath}");

        var talk = new LibConfigXmlReader(KrLibConfigPath).LoadAllNpcTalkTables()
            .Single(t => t.Type == 1095 && t.MapName == "Coral_Play3");

        Assert.Equal((520, 1236, 1237), (talk.QuestId0, talk.QuestId1, talk.QuestId2));
        Assert.Equal((7, 1237, 9, 0), (talk.PreCheckType, talk.PreCheckArg, talk.PreCheckYes, talk.PreCheckNo));
    }

    [SkippableFact]
    public void QuestRecordsRememberTheirTableAndResultTablesAreFoundByName()
    {
        Skip.IfNot(KrAvailable, $"KR libconfig not at {KrLibConfigPath}");
        var reader = new LibConfigXmlReader(KrLibConfigPath);

        var quests = reader.LoadAllQuestTables();
        var results = new InMemoryQuestResultLookup(reader.LoadAllQuestResultTables());

        Assert.Equal("ScenarioQuestInfo", quests.Single(q => q.Id == 1234).SourceTable);
        Assert.Equal("QuestInfo", quests.Single(q => q.Id == 675).SourceTable);
        Assert.NotEmpty(results.RowsOf("QuestResult_1234"));
        Assert.NotEmpty(results.RowsOf("QuestResult_407_KEY"));
        Assert.NotNull(results.GetByQuestId(1234, 1));
    }
}
