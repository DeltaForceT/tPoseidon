namespace tPoseidon.Core.LibConfig.Tests;

public class LibConfigXmlReaderTests
{
    private const string SyntheticXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <INI TableCount="2" TableInfoCount="2">
        <TABLE name="OtherTable" RowCount="1" TableInfoID="999" FieldCnt="1">
            <FIELDINFO Name="X" IsKey="1" DataType="INT"></FIELDINFO>
            <ROW><X>42</X></ROW>
        </TABLE>
        <TABLE name="QuestInfo" RowCount="2" TableInfoID="152" FieldCnt="28">
            <FIELDINFO Name="Id" IsKey="1" DataType="INT"></FIELDINFO>
            <ROW>
                <Id>1233</Id>
                <Name><![CDATA[Trickster Registration]]></Name>
                <Client><![CDATA[Heidi]]></Client>
                <Pos><![CDATA[Blooming Cora]]></Pos>
                <Comment><![CDATA[Find Bunny Maid]]></Comment>
                <MinLevel>1</MinLevel>
                <MaxLevel>400</MaxLevel>
                <Exp>256</Exp>
                <Tmxp>256</Tmxp>
                <SrcItem0>11805</SrcItem0>
                <Cnt0>1</Cnt0>
                <SrcItem1>0</SrcItem1>
                <Cnt1>0</Cnt1>
                <SrcItem2>0</SrcItem2>
                <Cnt2>0</Cnt2>
                <SrcItem3>0</SrcItem3>
                <Cnt3>0</Cnt3>
                <SrcItem4>0</SrcItem4>
                <Cnt4>0</Cnt4>
                <SrcItem5>0</SrcItem5>
                <Cnt5>0</Cnt5>
                <SrcSkill0>0</SrcSkill0>
                <SrcSkillLv0>0</SrcSkillLv0>
                <SrcGelder>0</SrcGelder>
                <ResultTable><![CDATA[QuestResult_1233]]></ResultTable>
                <MaxCount>1</MaxCount>
                <RefId>1233</RefId>
                <Options>0</Options>
            </ROW>
            <ROW>
                <Id>1234</Id>
                <Name><![CDATA[Prove Your Spirit]]></Name>
                <Client><![CDATA[Bunny Maid]]></Client>
                <Pos><![CDATA[Coral Beach]]></Pos>
                <Comment><![CDATA[Kill 2 Baby Carrots]]></Comment>
                <MinLevel>1</MinLevel>
                <MaxLevel>400</MaxLevel>
                <Exp>512</Exp>
                <Tmxp>128</Tmxp>
                <SrcItem0>3030</SrcItem0>
                <Cnt0>1</Cnt0>
                <SrcItem1>559</SrcItem1>
                <Cnt1>1</Cnt1>
                <SrcItem2>0</SrcItem2>
                <Cnt2>0</Cnt2>
                <SrcItem3>0</SrcItem3>
                <Cnt3>0</Cnt3>
                <SrcItem4>0</SrcItem4>
                <Cnt4>0</Cnt4>
                <SrcItem5>0</SrcItem5>
                <Cnt5>0</Cnt5>
                <SrcSkill0>0</SrcSkill0>
                <SrcSkillLv0>0</SrcSkillLv0>
                <SrcGelder>0</SrcGelder>
                <ResultTable><![CDATA[QuestResult_1234]]></ResultTable>
                <MaxCount>2</MaxCount>
                <RefId>1234</RefId>
                <Options>0</Options>
            </ROW>
        </TABLE>
        </INI>
        """;

    private static IReadOnlyList<QuestInfoEntry> ParseSynthetic()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, SyntheticXml);
            return new LibConfigXmlReader(path).LoadQuestInfo();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadQuestInfo_SyntheticXml_YieldsBothQuests()
    {
        var entries = ParseSynthetic();
        Assert.Equal(2, entries.Count);
        Assert.Equal(1233, entries[0].Id);
        Assert.Equal(1234, entries[1].Id);
    }

    [Fact]
    public void LoadQuestInfo_Heidi1233_HasExpectedRewardShape()
    {
        var entries = ParseSynthetic();
        var heidi = entries.Single(e => e.Id == 1233);
        Assert.Equal("Trickster Registration", heidi.Name);
        Assert.Equal("Heidi", heidi.Client);
        Assert.Equal(1, heidi.MinLevel);
        Assert.Equal(400, heidi.MaxLevel);
        Assert.Equal(256, heidi.Exp);
        Assert.Equal(256, heidi.Tmxp);
        Assert.Equal(11805, heidi.SrcItem0);
        Assert.Equal(1, heidi.Cnt0);
        Assert.Equal(0, heidi.SrcItem1);
        Assert.Equal(0, heidi.Cnt1);
        Assert.Equal(0, heidi.SrcSkill0);
        Assert.Equal(0, heidi.SrcGelder);
        Assert.Equal("QuestResult_1233", heidi.ResultTable);
        Assert.Equal(1, heidi.MaxCount);
        Assert.Equal(0, heidi.Options);
    }

    [Fact]
    public void LoadQuestInfo_BunnyMaid1234_HasExpectedRewardShape()
    {
        var entries = ParseSynthetic();
        var bunnyMaid = entries.Single(e => e.Id == 1234);
        Assert.Equal("Prove Your Spirit", bunnyMaid.Name);
        Assert.Equal("Bunny Maid", bunnyMaid.Client);
        Assert.Equal(3030, bunnyMaid.SrcItem0);
        Assert.Equal(1, bunnyMaid.Cnt0);
        Assert.Equal(559, bunnyMaid.SrcItem1);
        Assert.Equal(1, bunnyMaid.Cnt1);
        Assert.Equal(2, bunnyMaid.MaxCount);
    }

    [Fact]
    public void LoadQuestInfo_OtherTable_IsIgnored()
    {
        var entries = ParseSynthetic();
        Assert.DoesNotContain(entries, e => e.Id == 42);
    }
}
