using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.LibConfig.Tests;

public class MonsterTableRatioTests
{
    private static readonly string KrLibConfigPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "data", "libconfig_kr_en.xml"));

    private static bool KrAvailable => File.Exists(KrLibConfigPath);

    [SkippableFact]
    public void MonZoneRowsKeepTheDecimalRatio()
    {
        Skip.IfNot(KrAvailable, $"KR libconfig not at {KrLibConfigPath}");

        var rows = new LibConfigXmlReader(KrLibConfigPath).LoadAllMonZoneTables()
            .Where(row => row.MapName == "Coral_Play3")
            .ToList();

        Assert.Equal(6, rows.Count);
        Assert.Equal((2183, 1, 50, 50, 2f), (rows[0].MonsterId, rows[0].Region, rows[0].Avr, rows[0].Max, rows[0].Ratio));
        Assert.All(rows, row => Assert.Equal(2f, row.Ratio));
    }

    [SkippableFact]
    public void MonsterPoolTablesKeepTheDecimalRatio()
    {
        Skip.IfNot(KrAvailable, $"KR libconfig not at {KrLibConfigPath}");

        var pool = new LibConfigXmlReader(KrLibConfigPath).LoadAllMonsterPoolTables()["Mon_Coral_Play3"];

        Assert.Equal(new MonsterPoolRow(2183, 50, 50, 2.0, 1), pool[0]);
    }
}
