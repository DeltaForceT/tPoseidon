namespace tPoseidon.Core.LibConfig.Tests;

public class WorldMapTablesTests
{
    private const string Xml = """
        <?xml version="1.0" encoding="utf-8"?>
        <LIBCONFIG>
        <TABLE name="WorldMap_RegionInfo" RowCount="3" TableInfoID="1" FieldCnt="3">
        <ROW><RegionID>1</RegionID><IsOpened>1</IsOpened><RegionTable>RegionInfo_A</RegionTable></ROW>
        <ROW><RegionID>2</RegionID><IsOpened>0</IsOpened><RegionTable>RegionInfo_B</RegionTable></ROW>
        <ROW><RegionID>3</RegionID><IsOpened>1</IsOpened><RegionTable>RegionInfo_C</RegionTable></ROW>
        </TABLE>
        <TABLE name="RegionInfo_A" RowCount="2" TableInfoID="2" FieldCnt="1"><ROW><ZoneID>20</ZoneID></ROW><ROW><ZoneID>29</ZoneID></ROW></TABLE>
        <TABLE name="RegionInfo_B" RowCount="1" TableInfoID="2" FieldCnt="1"><ROW><ZoneID>40</ZoneID></ROW></TABLE>
        <TABLE name="RegionInfo_C" RowCount="1" TableInfoID="2" FieldCnt="1"><ROW><ZoneID>50</ZoneID></ROW></TABLE>
        </LIBCONFIG>
        """;

    [Fact]
    public void OnlyZonesOfOpenedRegionsAreShown()
    {
        var path = Path.Combine(Path.GetTempPath(), $"worldmap-{Guid.NewGuid():N}.xml");
        File.WriteAllText(path, Xml);
        try
        {
            var tables = new LibConfigXmlReader(path).LoadWorldMapTables();
            Assert.True(tables.Shows(20));
            Assert.True(tables.Shows(29));
            Assert.True(tables.Shows(50));
            Assert.False(tables.Shows(40));
            Assert.False(WorldMapTables.Empty.Shows(20));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
