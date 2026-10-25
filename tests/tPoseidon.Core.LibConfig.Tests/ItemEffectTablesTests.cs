namespace tPoseidon.Core.LibConfig.Tests;

public sealed class ItemEffectTablesTests
{
    private static readonly RandomPortZone[] BroomZones = [new RandomPortZone(1, 20), new RandomPortZone(54, 30)];
    private static readonly int[] Keys = [19839, 19837, 19840];

    private const string Xml = """
        <?xml version="1.0" encoding="utf-8"?>
        <INI TableCount="7" TableInfoCount="7">
        <TABLE name="TeleportAdvInfo" RowCount="2" TableInfoID="1" FieldCnt="5">
            <FIELDINFO Name="ZoneIndex" IsKey="1" DataType="INT"></FIELDINFO>
            <ROW><ZoneIndex>0</ZoneIndex><ZoneType>0</ZoneType><Name><![CDATA[Gate]]></Name><ZoneID>3</ZoneID><LCDBunchFrame>0</LCDBunchFrame></ROW>
            <ROW><ZoneIndex>1</ZoneIndex><ZoneType>1</ZoneType><Name><![CDATA[Dungeon]]></Name><ZoneID>4</ZoneID><LCDBunchFrame>1</LCDBunchFrame></ROW>
        </TABLE>
        <TABLE name="RandomPortParam" RowCount="1" TableInfoID="2" FieldCnt="3">
            <FIELDINFO Name="ID" IsKey="1" DataType="INT"></FIELDINFO>
            <ROW><ID>11333</ID><Notice><![CDATA[Fly]]></Notice><ZoneID_0>1</ZoneID_0><Ratio_0>20</Ratio_0><ZoneID_1>54</ZoneID_1><Ratio_1>30</Ratio_1><ZoneID_2>0</ZoneID_2><Ratio_2>0</Ratio_2></ROW>
        </TABLE>
        <TABLE name="BonusPresentItemInfo" RowCount="1" TableInfoID="3" FieldCnt="4">
            <FIELDINFO Name="PresentId" IsKey="1" DataType="INT"></FIELDINFO>
            <ROW><PresentId>431762</PresentId><SelectItemId>71915</SelectItemId><PickBonusPresentId>431763</PickBonusPresentId><UnpickBonusPresentId>431764</UnpickBonusPresentId></ROW>
        </TABLE>
        <TABLE name="KeyboxItemInfo" RowCount="1" TableInfoID="4" FieldCnt="7">
            <FIELDINFO Name="KeyBoxID" IsKey="1" DataType="INT"></FIELDINFO>
            <ROW><KeyBoxID>431827</KeyBoxID><KeyItemID><![CDATA[19839|19837|19840]]></KeyItemID><OnlyKeyitem>1</OnlyKeyitem><NoramlItemID>0</NoramlItemID><RareItemID_TypeA>431838</RareItemID_TypeA><RareItemID_TypeB>431843</RareItemID_TypeB><RareItem_TypeB_Droprate>30</RareItem_TypeB_Droprate></ROW>
        </TABLE>
        <TABLE name="KeyItemRateInfo" RowCount="2" TableInfoID="7" FieldCnt="2">
            <FIELDINFO Name="ItemID" IsKey="1" DataType="INT"></FIELDINFO>
            <ROW><ItemID>19839</ItemID><RaiseDroprate>10</RaiseDroprate></ROW>
            <ROW><ItemID>19837</ItemID><RaiseDroprate>25</RaiseDroprate></ROW>
        </TABLE>
        <TABLE name="Item_ExpParam" RowCount="1" TableInfoID="5" FieldCnt="5">
            <FIELDINFO Name="ItemId" IsKey="1" DataType="INT"></FIELDINFO>
            <ROW><ItemId>64703</ItemId><ExpAdvantage>1.500000</ExpAdvantage><ExpPenalty>1.000000</ExpPenalty><TmxpAdvantage>1.800000</TmxpAdvantage><TmxpPenalty>0.000000</TmxpPenalty></ROW>
        </TABLE>
        <TABLE name="ImprintSkillInfo" RowCount="2" TableInfoID="6" FieldCnt="6">
            <FIELDINFO Name="Id" IsKey="1" DataType="INT"></FIELDINFO>
            <ROW><Id>37109</Id><Name><![CDATA[Attack]]></Name><Time>24</Time><Comment><![CDATA[c]]></Comment><Type>0</Type><Value>1000.000000</Value></ROW>
            <ROW><Id>37133</Id><Name><![CDATA[Defense]]></Name><Time>1</Time><Comment><![CDATA[c]]></Comment><Type>10</Type><Value>1000.000000</Value></ROW>
        </TABLE>
        </INI>
        """;

    private static LibConfigXmlReader Reader()
    {
        var path = Path.Combine(Path.GetTempPath(), $"item-effects-{Guid.NewGuid():N}.xml");
        File.WriteAllText(path, Xml);
        return new LibConfigXmlReader(path);
    }

    [Fact]
    public void TheSpecialPortTablesCarryTheAdvancedZonesAndTheBroomRatios()
    {
        var tables = Reader().LoadSpecialPortTables();

        Assert.Equal(new TeleportAdvEntry(1, 1, 4), tables.Advanced[1]);
        var broom = tables.Random[11333];
        Assert.Equal("Fly", broom.Notice);
        Assert.Equal(BroomZones, broom.Zones);
    }

    [Fact]
    public void TheBoxTablesCarryTheBonusPresentsAndTheKeyBoxes()
    {
        var tables = Reader().LoadBoxTables();

        Assert.Equal(new BonusPresentEntry(431762, 71915, 431763, 431764), tables.Bonuses[431762]);
        var box = tables.Keyboxes[431827];
        Assert.Equal(Keys, box.KeyItemIds);
        Assert.True(box.OnlyKey);
        Assert.Equal(431838, box.RareItemIdA);
        Assert.Equal(431843, box.RareItemIdB);
        Assert.Equal(30, box.RareBRatePercent);
        Assert.Equal(new KeyRateEntry(19839, 10), tables.KeyRates[19839]);
        Assert.Equal(25, tables.KeyRates[19837].RaiseDroprate);
        Assert.False(tables.KeyRates.ContainsKey(19840));
    }

    [Fact]
    public void TheExpBoostsAndTheImprintsLoadByItemId()
    {
        var reader = Reader();

        var boost = reader.LoadExpBoosts()[64703];
        Assert.Equal(1.5f, boost.ExpAdvantage);
        Assert.Equal(1.8f, boost.TmAdvantage);
        Assert.Equal(new ImprintEntry(37109, 24, 0, 1000f), reader.LoadImprints()[37109]);
        Assert.Equal(new ImprintEntry(37133, 1, 10, 1000f), reader.LoadImprints()[37133]);
    }
}
