namespace tPoseidon.Core.LibConfig.Tests;

public class ItemColumnsTests
{
    private static readonly string KrLibConfigPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "data", "libconfig_kr_en.xml"));

    private static readonly int[] StrengthRingStats = { 28, 0, -1, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

    private static bool KrAvailable => File.Exists(KrLibConfigPath);

    [SkippableFact]
    public void AGroupRestrictedRingCarriesItsGroupLevelAndStatColumns()
    {
        Skip.IfNot(KrAvailable, $"KR libconfig not at {KrLibConfigPath}");

        var ring = new LibConfigXmlReader(KrLibConfigPath).LoadItemParam2().Single(item => item.Id == 7015);

        Assert.Equal((7, 7, 25), (ring.Type, ring.SubType, ring.MinLevel));
        Assert.Equal(1 | 0x10 | 0x4000, ring.Options);
        Assert.Equal(1, ring.CharacterGroupFlags);
        Assert.Equal(0, ring.CharacterTypeFlags);
        Assert.Equal(StrengthRingStats, ring.StatBonuses);
    }

    [SkippableFact]
    public void ASkillCardNamesItsSkillCostAndJobs()
    {
        Skip.IfNot(KrAvailable, $"KR libconfig not at {KrLibConfigPath}");

        var cards = new LibConfigXmlReader(KrLibConfigPath).LoadSkillCards();
        var shockwave = cards.Single(card => card.Id == 27001);
        var rally = cards.Single(card => card.Id == 27109);

        Assert.Equal((1001, 1, 1), (shockwave.AffectionSkill, shockwave.AffectionPoint, shockwave.RegisterLevel));
        Assert.Equal(1 | 2, shockwave.JobType1);
        Assert.Empty(shockwave.PreSkills);
        Assert.Equal(new SkillRequirement(1004, 11), Assert.Single(rally.PreSkills));
    }

    [SkippableFact]
    public void SkillShopsCarryTheirPagesAndMasteryData()
    {
        Skip.IfNot(KrAvailable, $"KR libconfig not at {KrLibConfigPath}");
        var reader = new LibConfigXmlReader(KrLibConfigPath);

        var louis = reader.LoadAllSkillShopZoneTables().Single(row => row.MapName == "Beach_Town" && row.NpcType == 6300);
        var master = reader.LoadSkillMasterInfo().Single(row => row.SkillId == 1104);
        var lists = reader.LoadSkillIdLists(new HashSet<string>(StringComparer.Ordinal) { "LimitSkillMaster_P_1" });

        Assert.Equal(21, louis.Pages.Count);
        Assert.StartsWith("Hey, I'm Louis.", louis.Pages[0].Text, StringComparison.Ordinal);
        Assert.Equal((24341, 3), (master.ItemId, master.ItemCount));
        Assert.Contains(1001, lists["LimitSkillMaster_P_1"]);
    }
}
