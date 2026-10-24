namespace tPoseidon.Core.LibConfig.Tests;

public class SoulSystemTablesTests
{
    private static readonly string KrLibConfigPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "data", "libconfig_kr_en.xml"));

    private static bool KrAvailable => File.Exists(KrLibConfigPath);

    [SkippableFact]
    public void SoulTablesCarryTheGaugeSourcesPotionsRecipesAndTartarosMaps()
    {
        Skip.IfNot(KrAvailable, $"KR libconfig not at {KrLibConfigPath}");

        var tables = new LibConfigXmlReader(KrLibConfigPath).LoadSoulSystemTables();

        Assert.Equal(new SoulSystemInfoEntry(114, 100, 15, 1, 10, 60, 2), tables.Info);
        Assert.Equal(24, tables.Sources.Count);
        Assert.Contains(new SoulSourceEntry(66324, 15000, 78), tables.Sources);
        Assert.Equal([1, 2, 3, 4, 5], tables.PotionCounts.Select(row => row.Count));
        Assert.Equal(14, tables.Equipment.Count);
        Assert.Equal(new SoulEquipmentEntry(50029, 5047, 10, 1), tables.Equipment[50029]);
        var steelSword = tables.CraftsByResult[50029];
        Assert.Equal(155000, steelSword.Fee);
        Assert.Equal(new SoulMaterial(50026, 1), steelSword.Materials[0]);
        Assert.Equal(5, steelSword.Materials.Count);
        Assert.Equal(20, tables.Intensify.Count);
        Assert.Equal(747900, tables.Intensify.Single(row => row.SoulLevel == 10 && row.WeaponLevel == 320).Cost);
        Assert.Equal(3, tables.Intensify.Single(row => row.SoulLevel == 1 && row.WeaponLevel == 320).Materials.Count);
        Assert.Equal(97, tables.TartarosMaps.Count);
        Assert.Contains(698, tables.TartarosMaps);
    }
}
