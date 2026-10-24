namespace tPoseidon.Core.LibConfig.Tests;

public sealed class PartyQuestCatalogTests
{
    private const string SyntheticXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <INI TableCount="3" TableInfoCount="3">
        <TABLE name="PartyMonQuestInfo" RowCount="1" TableInfoID="4197" FieldCnt="29">
            <FIELDINFO Name="Id" IsKey="1" DataType="INT"></FIELDINFO>
            <ROW>
                <Id>3006</Id>
                <Name><![CDATA[Beach Party Quest]]></Name>
                <MinLevel>1</MinLevel>
                <MaxLevel>400</MaxLevel>
                <Time>20</Time>
                <SrcMonster0>2094</SrcMonster0>
                <Cnt0>32</Cnt0>
                <SrcMonster1>0</SrcMonster1>
                <Cnt1>0</Cnt1>
                <ResultTable><![CDATA[PMonQuestResult_3006]]></ResultTable>
                <Exp>1000</Exp>
                <Tmxp>200</Tmxp>
                <MaxCount>0</MaxCount>
                <ReturnMapID>3</ReturnMapID>
            </ROW>
        </TABLE>
        <TABLE name="PartyQuestExpTable" RowCount="2" TableInfoID="4198" FieldCnt="2">
            <FIELDINFO Name="Times" IsKey="1" DataType="INT"></FIELDINFO>
            <ROW><Times>1</Times><Ratio>1.800000</Ratio></ROW>
            <ROW><Times>2</Times><Ratio>1.500000</Ratio></ROW>
        </TABLE>
        <TABLE name="PMonQuestResult_3006" RowCount="1" TableInfoID="4221" FieldCnt="26">
            <FIELDINFO Name="Grade" IsKey="1" DataType="INT"></FIELDINFO>
            <ROW><Grade>0</Grade><Type>0</Type><Item0>63132</Item0><Cnt0>1</Cnt0><Item4>500</Item4><Cnt4>2</Cnt4><Gelder>0</Gelder></ROW>
        </TABLE>
        </INI>
        """;

    private static (PartyQuestCatalog Catalog, IReadOnlyList<MonsterQuestResultEntry> Results) Load()
    {
        var path = Path.Combine(Path.GetTempPath(), $"party-quests-{Guid.NewGuid():N}.xml");
        File.WriteAllText(path, SyntheticXml);
        try
        {
            var reader = new LibConfigXmlReader(path);
            return (new PartyQuestCatalog(reader.LoadPartyQuestInfo(), reader.LoadPartyQuestExpRatios()), reader.LoadPartyQuestResultTables());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AQuestKeepsItsTargetsTimeAndDefaultMaxCount()
    {
        var quest = Load().Catalog.GetById(3006);

        Assert.NotNull(quest);
        Assert.Equal(new PartyQuestTarget(2094, 32), Assert.Single(quest!.Targets));
        Assert.Equal(20, quest.TimeMinutes);
        Assert.Equal(PartyQuestInfoEntry.DefaultMaxCount, quest.EffectiveMaxCount);
        Assert.Equal(3, quest.ReturnMapId);
    }

    [Fact]
    public void TheResultRowIncludesTheFifthItem()
    {
        var row = Assert.Single(Load().Results);

        Assert.Equal("PMonQuestResult_3006", row.TableName);
        Assert.Equal(new[] { (63132, 1), (500, 2) }, row.RowItems());
    }

    [Theory]
    [InlineData(0, 0f)]
    [InlineData(1, 1.8f)]
    [InlineData(2, 1.5f)]
    [InlineData(50, 1.5f)]
    public void TheExpRatioFollowsTheCompletionCount(int completions, float ratio)
    {
        Assert.Equal(ratio, Load().Catalog.ExpRatio(completions));
    }

    [Fact]
    public void TheRewardFollowsTheKrFormula()
    {
        var catalog = Load().Catalog;

        var exp = catalog.RewardFor(1000, memberCount: 2, ownKills: 16, targetCount: 64, completionCount: 1);

        Assert.Equal((int)((1 * 1000.0 * 16 / 64 + (0.05 * 2 + 0.2) * 1000) * 1.8f), exp);
    }
}
