using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.LibConfig.Tests;

public class TutorialSystemLookupTests
{
    private const string SyntheticXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <INI TableCount="1" TableInfoCount="1">
        <TABLE name="TutorialSystem" RowCount="4" TableInfoID="12792" FieldCnt="12">
            <ROW>
                <ID>1</ID>
                <OpenType>0</OpenType>
                <QuestID>0</QuestID>
                <StartNpcID>1311</StartNpcID>
                <EndNpcID>1311</EndNpcID>
                <StartMapID>148</StartMapID>
                <MonsterMapID>0</MonsterMapID>
                <EndMapID>148</EndMapID>
                <DlgTable><![CDATA[NpcMsg_EP0_Npc312]]></DlgTable>
                <DlgIndex>8</DlgIndex>
                <UIOpen>0</UIOpen>
                <UIOpenType>0</UIOpenType>
            </ROW>
            <ROW>
                <ID>2</ID>
                <OpenType>1</OpenType>
                <QuestID>1233</QuestID>
                <StartNpcID>1311</StartNpcID>
                <EndNpcID>1011</EndNpcID>
                <StartMapID>148</StartMapID>
                <MonsterMapID>0</MonsterMapID>
                <EndMapID>148</EndMapID>
                <DlgTable><![CDATA[ ]]></DlgTable>
                <DlgIndex>0</DlgIndex>
                <UIOpen>0</UIOpen>
                <UIOpenType>0</UIOpenType>
            </ROW>
            <ROW>
                <ID>3</ID>
                <OpenType>3</OpenType>
                <QuestID>1233</QuestID>
                <StartNpcID>1311</StartNpcID>
                <EndNpcID>1011</EndNpcID>
                <StartMapID>148</StartMapID>
                <MonsterMapID>0</MonsterMapID>
                <EndMapID>148</EndMapID>
                <DlgTable><![CDATA[ ]]></DlgTable>
                <DlgIndex>0</DlgIndex>
                <UIOpen>0</UIOpen>
                <UIOpenType>1</UIOpenType>
            </ROW>
            <ROW>
                <ID>4</ID>
                <OpenType>1</OpenType>
                <QuestID>1234</QuestID>
                <StartNpcID>1011</StartNpcID>
                <EndNpcID>1011</EndNpcID>
                <StartMapID>148</StartMapID>
                <MonsterMapID>153</MonsterMapID>
                <EndMapID>148</EndMapID>
                <DlgTable><![CDATA[ ]]></DlgTable>
                <DlgIndex>0</DlgIndex>
                <UIOpen>1</UIOpen>
                <UIOpenType>0</UIOpenType>
            </ROW>
        </TABLE>
        </INI>
        """;

    private static IReadOnlyList<TutorialSystemEntry> ParseSynthetic()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, SyntheticXml);
            var reader = new LibConfigXmlReader(path);
            return reader.LoadTutorialSystem();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadTutorialSystem_ReadsAllRows()
    {
        var entries = ParseSynthetic();
        Assert.Equal(4, entries.Count);
    }

    [Fact]
    public void LoadTutorialSystem_PopulatesAllFields()
    {
        var entries = ParseSynthetic();
        var welcome = entries.Single(e => e.Id == 1);
        Assert.Equal(0, welcome.OpenType);
        Assert.Equal(0, welcome.QuestId);
        Assert.Equal(1311, welcome.StartNpcID);
        Assert.Equal(1311, welcome.EndNpcID);
        Assert.Equal(148, welcome.StartMapID);
        Assert.Equal(0, welcome.MonsterMapID);
        Assert.Equal(148, welcome.EndMapID);
        Assert.Equal("NpcMsg_EP0_Npc312", welcome.DlgTable);
        Assert.Equal(8, welcome.DlgIndex);
    }

    [Fact]
    public void GetById_ReturnsEntry()
    {
        var lookup = new InMemoryTutorialSystemLookup(ParseSynthetic());
        var step1 = lookup.GetById(1);
        Assert.NotNull(step1);
        Assert.Equal("NpcMsg_EP0_Npc312", step1.DlgTable);

        var missing = lookup.GetById(99);
        Assert.Null(missing);
    }

    [Fact]
    public void FindByStartNpcOnMap_HeidiCoralTown_ReturnsWelcomeStep()
    {
        var lookup = new InMemoryTutorialSystemLookup(ParseSynthetic());
        var match = lookup.FindByStartNpcOnMap(npcType: 1311, mapId: 148);
        Assert.NotNull(match);
        Assert.Equal(1, match.Id);
        Assert.Equal("NpcMsg_EP0_Npc312", match.DlgTable);
        Assert.Equal(8, match.DlgIndex);
    }

    [Fact]
    public void FindByStartNpcOnMap_NoMatch_ReturnsNull()
    {
        var lookup = new InMemoryTutorialSystemLookup(ParseSynthetic());
        var missing = lookup.FindByStartNpcOnMap(npcType: 9999, mapId: 148);
        Assert.Null(missing);
    }

    [Fact]
    public void FindByQuestId_QuestWithMultipleSteps_ReturnsAll()
    {
        var lookup = new InMemoryTutorialSystemLookup(ParseSynthetic());
        var quest1233 = lookup.FindByQuestId(1233);
        Assert.Equal(2, quest1233.Count);
        Assert.Contains(quest1233, e => e.Id == 2);
        Assert.Contains(quest1233, e => e.Id == 3);
    }

    [Fact]
    public void FindByQuestId_NonQuestStep_NotIndexed()
    {
        var lookup = new InMemoryTutorialSystemLookup(ParseSynthetic());
        var quest0 = lookup.FindByQuestId(0);
        Assert.Empty(quest0);
    }
}
