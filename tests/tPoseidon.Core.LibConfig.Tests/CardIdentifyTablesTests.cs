using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.LibConfig.Tests;

public class CardIdentifyTablesTests
{
    private static readonly string KrLibConfigPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "data", "libconfig_kr_en.xml"));

    private static bool KrAvailable => File.Exists(KrLibConfigPath);

    [SkippableFact]
    public void CardIdentifyTablesCarryGradesResultsCombosAndExpRatios()
    {
        Skip.IfNot(KrAvailable, $"KR libconfig not at {KrLibConfigPath}");

        var tables = new LibConfigXmlReader(KrLibConfigPath).LoadCardIdentifyTables();

        Assert.Equal(12, tables.Grades.Count);
        Assert.Equal(1, tables.Grades[1].Table0);
        Assert.Equal(12, tables.Results.Count);
        Assert.Equal(new CardIdentifyResultEntry(2003, 30), tables.Results[1][0]);
        Assert.Equal(51, tables.ComboRatios.Count);
        Assert.Equal([1, 2, 4, 7, 12], tables.ComboRatios.Take(5));
        Assert.Equal(364, tables.ComboRatios[^1]);
        Assert.Equal((0f, 1f), (tables.ExpRatio, tables.TmExpRatio));
    }
}
