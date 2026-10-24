using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.LibConfig.Tests;

public class PetDigTablesTests
{
    private static readonly string KrLibConfigPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "data", "libconfig_kr_en.xml"));

    private static bool KrAvailable => File.Exists(KrLibConfigPath);

    [SkippableFact]
    public void PetDigTablesCarryPetsFoodsExItemsAreasAndRegions()
    {
        Skip.IfNot(KrAvailable, $"KR libconfig not at {KrLibConfigPath}");

        var tables = new LibConfigXmlReader(KrLibConfigPath).LoadPetDigTables();

        Assert.Equal(8, tables.Pets.Count);
        Assert.Equal(new PetDigPetEntry(42, 100, 7000), tables.Pets[42]);
        Assert.Equal(new PetDigPetEntry(56, 200, 8000), tables.Pets[56]);
        Assert.Equal(new PetDigPetEntry(66, 300, 9000), tables.Pets[66]);
        Assert.Equal(6, tables.Foods.Count);
        Assert.Equal(new PetDigFoodEntry(19852, 30, 100, 0), tables.Foods[19852]);
        Assert.Equal(new PetDigFoodEntry(19854, 15, 200, 100), tables.Foods[19854]);
        Assert.Equal(17, tables.ExItems.Count);
        var nox = tables.ExItems[72429];
        Assert.Equal(2000, nox.ExRate);
        Assert.Equal(8, nox.Items.Count);
        Assert.Equal(new PetDigExItem(106, 2000, 1), nox.Items[0]);
        Assert.Equal(new PetDigExItem(11636, 50, 1), nox.Items[^1]);
        Assert.Equal(16, tables.Areas.Count);
        Assert.Equal(new PetDigAreaEntry(14, 270, 400), tables.Areas[14]);
        Assert.Equal(4, tables.Zones[1].Region);
        Assert.Equal(3, tables.Zones[2].Region);
        Assert.Equal(new PetDigZone(4, 0, 10, 20), tables.Zones[17]);
        Assert.True(tables.Zones[196].IsMine);
        Assert.False(tables.Zones[17].IsMine);
        Assert.Equal(12, tables.DepthBands.Count);
        Assert.Equal(new PetDigDepthBand(0, 9, 0), tables.DepthBands[0]);
        Assert.Equal(new PetDigDepthBand(250, 300, 2000), tables.DepthBands[^1]);
    }
}
