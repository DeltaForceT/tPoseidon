using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.LibConfig.Tests;

public class GuardianTablesTests
{
    private static readonly string KrLibConfigPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "data", "libconfig_kr_en.xml"));

    private static bool KrAvailable => File.Exists(KrLibConfigPath);

    [SkippableFact]
    public void GuardianTablesCarryLevelsTypesGrowthBasicStatsAndExpRatios()
    {
        Skip.IfNot(KrAvailable, $"KR libconfig not at {KrLibConfigPath}");

        var tables = new LibConfigXmlReader(KrLibConfigPath).LoadGuardianTables();

        Assert.Equal(131805, tables.NextExpByLevel[0]);
        Assert.Equal(134001, tables.NextExpByLevel[1]);
        Assert.Equal(3687296, tables.NextExpByLevel[200]);
        Assert.Equal(12679944, tables.NextExpByLevel[400]);
        Assert.Equal(Enumerable.Range(15000, 8), tables.Params.Keys.Order());
        var first = tables.Params[15000];
        Assert.Equal((0, 1), (first.FType, first.LevelGrowthId));
        Assert.Equal([1, 3, 4, 5], first.RuneStones);
        Assert.Equal(9000, first.DefaultRuneSkills[0]);
        Assert.Equal([1f, 1f, 1f, 0.5f, 0.5f, 0.5f, 0.7f, 0.7f, 0.7f, 0.85f, 0.85f, 0.85f], tables.Growth[1].Ratios);
        Assert.Equal([18, 0, 10, 80, 0, 0, 1200, 0, 0, 120, 0, 0], tables.BasicStats[1].Points);
        Assert.Equal(800, tables.BasicStats.Keys.Max());
        Assert.Equal([0.1f, 0.1f, 0.3f], tables.ExpRatios.OrderBy(pair => pair.Key).Select(pair => pair.Value));
    }
}
