using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.LibConfig.Tests;

public class MonsterParamLookupTests
{
    private static readonly string KrLibConfigPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "data", "libconfig_kr_en.xml"));

    private static bool KrAvailable => File.Exists(KrLibConfigPath);

    [Fact]
    public void InMemoryLookup_EmptyInput_HasZeroCount()
    {
        var lookup = new InMemoryMonsterParamLookup(Array.Empty<MonsterParamEntry>());
        Assert.Equal(0, lookup.Count);
        Assert.Null(lookup.GetById(2183));
    }

    [Fact]
    public void InMemoryLookup_GetById_ReturnsMatchingEntry()
    {
        var torobbie = new MonsterParamEntry
        {
            Id = 2183,
            Name = "Torobbie",
            Type = 0,
            Level = 2,
            MaxHpLv = 240,
            ApLv = 20,
            DpLv = 3,
            Exp = 48,
            GMin = 30,
            GMax = 30,
        };
        var lookup = new InMemoryMonsterParamLookup(new[] { torobbie });

        var got = lookup.GetById(2183);

        Assert.NotNull(got);
        Assert.Equal("Torobbie", got!.Name);
        Assert.Equal(240, got.MaxHpLv);
        Assert.Equal(48, got.Exp);
    }

    [Fact]
    public void InMemoryLookup_DuplicateIds_LastOneWins()
    {
        var lookup = new InMemoryMonsterParamLookup(new[]
        {
            new MonsterParamEntry { Id = 2183, Name = "Torobbie-old" },
            new MonsterParamEntry { Id = 2183, Name = "Torobbie-new" },
        });

        Assert.Equal(1, lookup.Count);
        Assert.Equal("Torobbie-new", lookup.GetById(2183)!.Name);
    }

    [SkippableFact]
    public void LoadMonsterParamEx2_LoadsAllRowsFromKrLibconfig()
    {
        Skip.IfNot(KrAvailable, $"KR libconfig not at {KrLibConfigPath}");

        var entries = new LibConfigXmlReader(KrLibConfigPath).LoadMonsterParamEx2();

        Assert.True(entries.Count >= 1400,
            $"Expected ≥1400 MonsterParamEx2 rows; got {entries.Count}");
    }

    [SkippableFact]
    public void LoadMonsterParamEx2_FindsTorobbieWithCorrectStats()
    {
        Skip.IfNot(KrAvailable, $"KR libconfig not at {KrLibConfigPath}");

        var entries = new LibConfigXmlReader(KrLibConfigPath).LoadMonsterParamEx2();
        var torobbie = entries.SingleOrDefault(e => e.Id == 2183);

        Assert.NotNull(torobbie);
        Assert.Equal("Torobbie", torobbie!.Name);
        Assert.True(torobbie.Level > 0, $"Torobbie level should be set; got {torobbie.Level}");
    }

    [SkippableFact]
    public void LoadMonsterParamEx2_HasIsBossFlagsForBosses()
    {
        Skip.IfNot(KrAvailable, $"KR libconfig not at {KrLibConfigPath}");

        var entries = new LibConfigXmlReader(KrLibConfigPath).LoadMonsterParamEx2();
        var bossCount = entries.Count(e => e.IsBoss);

        Assert.True(bossCount > 0, "expected at least some IsBoss=true entries");
        Assert.True(bossCount < entries.Count / 2, $"too many bosses ({bossCount}/{entries.Count}); flag parsing likely wrong");
    }

    [SkippableFact]
    public void LoadMonsterParamEx2_HasGelderRangeForLootDrops()
    {
        Skip.IfNot(KrAvailable, $"KR libconfig not at {KrLibConfigPath}");

        var entries = new LibConfigXmlReader(KrLibConfigPath).LoadMonsterParamEx2();
        var withGalder = entries.Count(e => e.GMin > 0 || e.GMax > 0);

        Assert.True(withGalder > 100,
            $"expected many mobs with galder drops; got {withGalder}");
    }

    [SkippableTheory]
    [InlineData(2285)]
    [InlineData(2183)]
    public void TheFirstFieldMonstersNameADropRowThatHoldsTheCollectItem(int monsterId)
    {
        Skip.IfNot(KrAvailable, $"KR libconfig not at {KrLibConfigPath}");
        const int collectItem = 11908;

        var reader = new LibConfigXmlReader(KrLibConfigPath);
        var monster = reader.LoadMonsterParamEx2().Single(e => e.Id == monsterId);
        var row = reader.LoadMonsterDropRows().Single(e => e.Id == monster.ItemDropIdx);

        Assert.Equal(1, monster.ItemDropCnt);
        Assert.Contains(row.Items, item => item.ItemId == collectItem && item.Rate == 1f && item.Count == 1);
    }

    [SkippableFact]
    public void TheStarterPunchSkillLoadsItsFlagsAndItsLevelRows()
    {
        Skip.IfNot(KrAvailable, $"KR libconfig not at {KrLibConfigPath}");
        const int starterPunch = 1012;

        var reader = new LibConfigXmlReader(KrLibConfigPath);
        var skill = reader.LoadSkillParam2().Single(e => e.Id == starterPunch);
        var conditions = reader.LoadSkillInvocationRows().Single(r => r.Table == skill.EsaiiTable && r.Level == 1);
        var action = reader.LoadSkillActionRows().Single(r => r.Table == skill.EsaTable && r.Level == 1);

        Assert.Equal(0x202, skill.SkillOption);
        Assert.Equal((2, 1, -1, 1296, 1), (skill.InvocationCondition, skill.TargetInput, skill.EsClass, skill.EsAction, skill.SkillExp));
        Assert.Equal((24, 80), (conditions.Amount("MpCon"), conditions.Amount("Range")));
        Assert.Equal((1.0f, 3.0f), (conditions.Value("Delay"), conditions.Value("CoolTime")));
        Assert.False(conditions.Has("Probability"));
        Assert.Equal((2, 140, 0.8f), (action.Amount("Ratio1"), action.Amount("Ratio2"), action.Value("ApplyRatio")));
    }

    [SkippableFact]
    public void DropTablesKeepTwentySlotsByColumn()
    {
        Skip.IfNot(KrAvailable, $"KR libconfig not at {KrLibConfigPath}");

        var tables = new LibConfigXmlReader(KrLibConfigPath).LoadMonsterDropTables();

        Assert.NotEmpty(tables);
        Assert.All(tables, table => Assert.Equal(MonItemTableDropEntry.SlotCount, table.Slots.Count));
        Assert.Contains(tables, table => table.Slots.Any(slot => slot.ItemId != 0 && slot.Rate > 0f));
    }
}
