namespace tPoseidon.Core.LibConfig.Tests;

public class NpcLookupTests
{
    private const string SyntheticXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <INI TableCount="3" TableInfoCount="3">
        <TABLE name="NpcTalk_Beach" RowCount="2" TableInfoID="115" FieldCnt="56">
            <ROW>
                <Type>1311</Type>
                <PosId>1</PosId>
                <QuestId>1233</QuestId>
                <QuestId1>0</QuestId1>
                <QuestId2>0</QuestId2>
                <QuestId3>0</QuestId3>
                <QuestId4>0</QuestId4>
                <MsgTable><![CDATA[NpcMsg_Beach_Heidi]]></MsgTable>
                <HeadMarkType>2</HeadMarkType>
            </ROW>
            <ROW>
                <Type>1012</Type>
                <PosId>2</PosId>
                <QuestId>0</QuestId>
                <QuestId1>0</QuestId1>
                <QuestId2>0</QuestId2>
                <QuestId3>0</QuestId3>
                <QuestId4>0</QuestId4>
                <MsgTable><![CDATA[NpcMsg_Beach_BunnyMaid]]></MsgTable>
                <HeadMarkType>0</HeadMarkType>
            </ROW>
        </TABLE>
        <TABLE name="NpcMsg_Beach_Heidi" RowCount="2" TableInfoID="2024" FieldCnt="3">
            <ROW>
                <No>0</No>
                <NpcSType>10</NpcSType>
                <Message><![CDATA[Welcome to Caballa Island!\Yes\S_Quest_Check,1233]]></Message>
            </ROW>
            <ROW>
                <No>1</No>
                <NpcSType>10</NpcSType>
                <Message><![CDATA[Take this Registration Form.\Okay\Exit]]></Message>
            </ROW>
        </TABLE>
        <TABLE name="OtherTable" RowCount="1" TableInfoID="999" FieldCnt="1">
            <ROW><X>42</X></ROW>
        </TABLE>
        </INI>
        """;

    private static (IReadOnlyList<NpcTalkEntry>, IReadOnlyList<NpcMsgEntry>) ParseSynthetic()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, SyntheticXml);
            var reader = new LibConfigXmlReader(path);
            return (reader.LoadAllNpcTalkTables(), reader.LoadAllNpcMsgTables());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadAllNpcTalkTables_ReadsBothNpcs()
    {
        var (talk, _) = ParseSynthetic();
        Assert.Equal(2, talk.Count);
        Assert.Contains(talk, e => e.Type == 1311 && e.MapName == "Beach");
        Assert.Contains(talk, e => e.Type == 1012 && e.MapName == "Beach");
    }

    [Fact]
    public void LoadAllNpcTalkTables_PopulatesMsgTableField()
    {
        var (talk, _) = ParseSynthetic();
        var heidi = talk.Single(e => e.Type == 1311);
        Assert.Equal("NpcMsg_Beach_Heidi", heidi.MsgTable);
        Assert.Equal(1233, heidi.QuestId0);
        Assert.Equal(2, heidi.HeadMarkType);
    }

    [Fact]
    public void LoadAllNpcMsgTables_ReadsAllRows()
    {
        var (_, msgs) = ParseSynthetic();
        Assert.Equal(2, msgs.Count);
        var row0 = msgs.Single(m => m.No == 0);
        Assert.Equal("NpcMsg_Beach_Heidi", row0.TableName);
        Assert.Equal((byte)10, row0.NpcSType);
        Assert.Contains("Welcome to Caballa", row0.Message);
    }

    [Fact]
    public void NpcTalkLookup_GetByTypeOnMap_FindsBinding()
    {
        var (talk, _) = ParseSynthetic();
        var lookup = new InMemoryNpcTalkLookup(talk);
        var heidi = lookup.GetByTypeOnMap(1311, "Beach");
        Assert.NotNull(heidi);
        Assert.Equal("NpcMsg_Beach_Heidi", heidi.MsgTable);

        var missing = lookup.GetByTypeOnMap(9999, "Beach");
        Assert.Null(missing);
    }

    [Fact]
    public void NpcTalkLookup_GetByTypeAnyMap_FindsFirstMatch()
    {
        var (talk, _) = ParseSynthetic();
        var lookup = new InMemoryNpcTalkLookup(talk);
        var heidi = lookup.GetByTypeAnyMap(1311);
        Assert.NotNull(heidi);
        Assert.Equal("Beach", heidi.MapName);
    }

    [Fact]
    public void NpcTalkLookup_GetAllByMap_ReturnsAllOrderedByPosId()
    {
        var (talk, _) = ParseSynthetic();
        var lookup = new InMemoryNpcTalkLookup(talk);

        var beachNpcs = lookup.GetAllByMap("Beach");
        Assert.Equal(2, beachNpcs.Count);
        Assert.Equal(1, beachNpcs[0].PosId);
        Assert.Equal(2, beachNpcs[1].PosId);
        Assert.Equal(1311, beachNpcs[0].Type);
        Assert.Equal(1012, beachNpcs[1].Type);

        Assert.Empty(lookup.GetAllByMap("NonexistentMap"));
    }

    [Fact]
    public void NpcMsgLookup_GetRow_FindsByCompositeKey()
    {
        var (_, msgs) = ParseSynthetic();
        var lookup = new InMemoryNpcMsgLookup(msgs);

        var row0 = lookup.GetRow("NpcMsg_Beach_Heidi", 0);
        Assert.NotNull(row0);
        Assert.Contains("Welcome", row0.Message);

        var row1 = lookup.GetRow("NpcMsg_Beach_Heidi", 1);
        Assert.NotNull(row1);
        Assert.Contains("Registration Form", row1.Message);

        var missing = lookup.GetRow("NpcMsg_Beach_Heidi", 99);
        Assert.Null(missing);
    }
}
