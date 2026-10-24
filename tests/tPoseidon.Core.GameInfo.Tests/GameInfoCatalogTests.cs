using tPoseidon.Core.GameInfo;

namespace tPoseidon.Core.GameInfoTests;

public class GameInfoCatalogTests
{
    private static readonly string LibConfigPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "captures", "libconfig_eng-decrypted.xml"));

    private static bool LibConfigAvailable => File.Exists(LibConfigPath);

    [SkippableFact]
    public void LoadFromXml_PopulatesAllExpectedTables()
    {
        Skip.IfNot(LibConfigAvailable, $"libconfig not at {LibConfigPath}");

        var catalog = GameInfoCatalog.LoadFromXml(LibConfigPath);

        Assert.True(catalog.Quests.GetById(1233) is not null,
            "Quest 1233 'Trickster Registration' must be loaded from ScenarioQuestInfo");
        Assert.True(catalog.Quests.GetById(1234) is not null,
            "Quest 1234 'Prove Your Spirit!' must be loaded from ScenarioQuestInfo");
        Assert.True(catalog.MonsterQuests.GetById(31) is not null,
            "Quest 31 'Request of Hunter Yuri' must be loaded from MonsterQuestInfo");
        Assert.True(catalog.Items.GetById(11805) is not null,
            "Item 11805 (Registration Form) must be loaded from ItemParamEx");

        Assert.True(catalog.TableRowCounts["Quest"] > 1300);
        Assert.True(catalog.TableRowCounts["MonsterQuestInfo"] > 300);
        Assert.True(catalog.TableRowCounts["ItemParamEx"] > 1000);
        Assert.True(catalog.TableRowCounts["ItemParam2"] > 5000);
    }

    [SkippableFact]
    public void LoadFromXml_LogsEachTableLoadStep()
    {
        Skip.IfNot(LibConfigAvailable, $"libconfig not at {LibConfigPath}");

        var logs = new List<string>();
        var catalog = GameInfoCatalog.LoadFromXml(LibConfigPath, logs.Add);

        Assert.Contains(logs, line => line.Contains("QuestInfo", StringComparison.Ordinal));
        Assert.Contains(logs, line => line.Contains("NpcTalk", StringComparison.Ordinal));
        Assert.Contains(logs, line => line.Contains("MonsterQuestInfo", StringComparison.Ordinal));
        Assert.Contains(logs, line => line.Contains("ItemParamEx", StringComparison.Ordinal));
        Assert.Contains(logs, line => line.Contains("ready", StringComparison.Ordinal));
    }

    [SkippableFact]
    public void LoadFromXml_BunnyMaidQuestHasCorrectName()
    {
        Skip.IfNot(LibConfigAvailable, $"libconfig not at {LibConfigPath}");

        var catalog = GameInfoCatalog.LoadFromXml(LibConfigPath);
        var carrots = catalog.Quests.GetById(1234);

        Assert.NotNull(carrots);
        Assert.Equal("Bunny Maid", carrots.Client);
        Assert.Equal("Prove Your Spirit!", carrots.Name);
    }
}
