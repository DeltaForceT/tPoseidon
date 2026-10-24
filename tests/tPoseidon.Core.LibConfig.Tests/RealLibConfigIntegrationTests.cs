namespace tPoseidon.Core.LibConfig.Tests;

public class RealLibConfigIntegrationTests
{
    private static readonly string LibConfigPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "captures", "libconfig_eng-decrypted.xml"));

    private static bool LibConfigAvailable => File.Exists(LibConfigPath);

    [SkippableFact]
    public void LoadAllQuestTables_FindsQuest1233_InScenarioQuestInfo()
    {
        Skip.IfNot(LibConfigAvailable, $"libconfig not at {LibConfigPath}");

        var entries = new LibConfigXmlReader(LibConfigPath).LoadAllQuestTables();
        Assert.True(entries.Count > 1300,
            $"Expected ≥1300 quests (1185 QuestInfo + 189 ScenarioQuestInfo); got {entries.Count}");

        var heidi = entries.SingleOrDefault(e => e.Id == 1233);
        Assert.NotNull(heidi);
        Assert.Equal("Trickster Registration", heidi.Name);
        Assert.Equal("Heidi", heidi.Client);
        Assert.Equal(11805, heidi.SrcItem0);
    }

    [SkippableFact]
    public void InMemoryLookup_LoadFromXml_FindsQuest1233()
    {
        Skip.IfNot(LibConfigAvailable, $"libconfig not at {LibConfigPath}");

        var lookup = InMemoryQuestInfoLookup.LoadFromXml(LibConfigPath);
        var heidi = lookup.GetById(1233);
        Assert.NotNull(heidi);
        Assert.Equal("Trickster Registration", heidi.Name);
    }

    [SkippableFact]
    public void LoadMonsterQuestInfo_FindsHunterYuriQuest31()
    {
        Skip.IfNot(LibConfigAvailable, $"libconfig not at {LibConfigPath}");

        var entries = new LibConfigXmlReader(LibConfigPath).LoadMonsterQuestInfo();
        Assert.True(entries.Count > 300,
            $"Expected ≥300 MonsterQuestInfo rows; got {entries.Count}");

        var yuri = entries.SingleOrDefault(e => e.Id == 31);
        Assert.NotNull(yuri);
        Assert.Equal("Request of Hunter Yuri", yuri.Name);
        Assert.NotEqual(0, yuri.SrcMonster0);
        Assert.True(yuri.Cnt0 > 0);
    }

    [SkippableFact]
    public void LoadItemParamExtended_LoadsEquipmentCatalog()
    {
        Skip.IfNot(LibConfigAvailable, $"libconfig not at {LibConfigPath}");

        var entries = new LibConfigXmlReader(LibConfigPath).LoadItemParamExtended();
        Assert.True(entries.Count > 1000,
            $"Expected ≥1000 ItemParamEx rows; got {entries.Count}");

        Assert.All(entries, e =>
        {
            Assert.True(e.Id > 0, $"item id must be positive; got {e.Id}");
        });
    }

    [SkippableFact]
    public void LoadItemParam2_FindsRegistrationForm11805()
    {
        Skip.IfNot(LibConfigAvailable, $"libconfig not at {LibConfigPath}");

        var entries = new LibConfigXmlReader(LibConfigPath).LoadItemParam2();
        Assert.True(entries.Count > 5000,
            $"Expected ≥5000 ItemParam2 rows; got {entries.Count}");

        var form = entries.SingleOrDefault(e => e.Id == 11805);
        Assert.NotNull(form);
        Assert.NotEqual(string.Empty, form.Name);
    }

    [SkippableFact]
    public void LoadQuestItemInfo_LoadsWithoutError()
    {
        Skip.IfNot(LibConfigAvailable, $"libconfig not at {LibConfigPath}");

        var entries = new LibConfigXmlReader(LibConfigPath).LoadQuestItemInfo();
        Assert.NotNull(entries);
        foreach (var entry in entries)
        {
            Assert.True(entry.Id > 0, "QuestItemInfo.ID must be positive");
            Assert.True(entry.QuestId > 0, "QuestItemInfo.QID must be positive");
            Assert.True(entry.ItemId > 0, "QuestItemInfo.ItemId must be positive");
        }
    }
}
