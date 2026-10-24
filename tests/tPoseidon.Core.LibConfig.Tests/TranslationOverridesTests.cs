using tPoseidon.Core.Common;

namespace tPoseidon.Core.LibConfig.Tests;

public class TranslationOverridesTests
{
    private static readonly string RealDatPath =
        RepoRoot.Data("variants", "En2010", "libconfig_eng.dat");

    [SkippableFact]
    public void RealCorpus_Row1095_DisplayNameBecomesLifeguardDeen()
    {
        Skip.IfNot(File.Exists(RealDatPath), $"no dat at {RealDatPath}");

        var lookup = InMemoryCharacterInfoLookup.LoadFromXml(RealDatPath);
        var deen = lookup.GetById(1095);
        Assert.NotNull(deen);
        Assert.Equal("Lifeguard Deen", deen.DisplayName);
        Assert.Equal("Lifeguard Deen", deen.CommonNameEng);
    }

    [SkippableFact]
    public void RealCorpus_NpcMsgDanoChain_TokensPreserved()
    {
        Skip.IfNot(File.Exists(RealDatPath), $"no dat at {RealDatPath}");

        var msgs = InMemoryNpcMsgLookup.LoadFromXml(RealDatPath);
        var row13 = msgs.GetRow("NpcMsg_Beach_Npc110_dano", 13);
        Assert.NotNull(row13);
        var m = row13.Message;
        Assert.DoesNotContain("找", m, StringComparison.Ordinal);
        Assert.Contains("Give_Item,14,15", m, StringComparison.Ordinal);
        Assert.Contains("@img,i3071", m, StringComparison.Ordinal);
        Assert.EndsWith("*\\*block", m, StringComparison.Ordinal);
    }

    private const string SyntheticXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <INI TableCount="2" TableInfoCount="2">
        <TABLE name="CharacterInfo" RowCount="2" TableInfoID="1" FieldCnt="3">
            <FIELDINFO Name="ID" IsKey="1" DataType="INT"></FIELDINFO>
            <ROW>
                <ID>1095</ID>
                <CommonName><![CDATA[rescuer dean]]></CommonName>
                <CommonName_Eng><![CDATA[rescuer dean]]></CommonName_Eng>
            </ROW>
            <ROW>
                <ID>1311</ID>
                <CommonName><![CDATA[Heidi]]></CommonName>
                <CommonName_Eng><![CDATA[Heidi]]></CommonName_Eng>
            </ROW>
        </TABLE>
        <TABLE name="QuestInfo" RowCount="1" TableInfoID="2" FieldCnt="28">
            <FIELDINFO Name="Id" IsKey="1" DataType="INT"></FIELDINFO>
            <ROW>
                <Id>1233</Id>
                <Name><![CDATA[trickster register]]></Name>
                <Client><![CDATA[Heidi]]></Client>
                <Pos><![CDATA[blooming cora]]></Pos>
                <Comment><![CDATA[find bunny maid %d]]></Comment>
                <MinLevel>1</MinLevel><MaxLevel>400</MaxLevel>
                <Exp>256</Exp><Tmxp>256</Tmxp>
                <SrcItem0>11805</SrcItem0><Cnt0>1</Cnt0>
                <SrcItem1>0</SrcItem1><Cnt1>0</Cnt1>
                <SrcItem2>0</SrcItem2><Cnt2>0</Cnt2>
                <SrcItem3>0</SrcItem3><Cnt3>0</Cnt3>
                <SrcItem4>0</SrcItem4><Cnt4>0</Cnt4>
                <SrcItem5>0</SrcItem5><Cnt5>0</Cnt5>
                <SrcSkill0>0</SrcSkill0><SrcSkillLv0>0</SrcSkillLv0>
                <SrcGelder>0</SrcGelder>
                <ResultTable><![CDATA[QuestResult_1233]]></ResultTable>
                <MaxCount>1</MaxCount><RefId>1233</RefId><Options>0</Options>
            </ROW>
        </TABLE>
        </INI>
        """;

    private static string WriteCorpus(string dir)
    {
        var datPath = Path.Combine(dir, "libconfig_eng.dat");
        File.WriteAllText(datPath, SyntheticXml);
        return datPath;
    }

    [Fact]
    public void NoOverrideFile_PurePassthrough()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var datPath = WriteCorpus(dir);
            var reader = new LibConfigXmlReader(datPath);

            var chars = reader.LoadCharacterInfo();
            Assert.Equal("rescuer dean", chars.Single(c => c.Id == 1095).CommonName);

            var quests = reader.LoadQuestInfo();
            var q = quests.Single(e => e.Id == 1233);
            Assert.Equal("trickster register", q.Name);
            Assert.Equal("blooming cora", q.Pos);
            Assert.Equal("find bunny maid %d", q.Comment);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void OverrideFile_SubstitutesOnlyMatchedCells()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var datPath = WriteCorpus(dir);
            File.WriteAllText(Path.Combine(dir, TranslationOverrides.FileName), """
                {
                  "CharacterInfo": {
                    "1095": { "CommonName": "Lifeguard Deen", "CommonName_Eng": "Lifeguard Deen" }
                  },
                  "QuestInfo": {
                    "1233": { "Name": "Trickster Registration", "Pos": "Blooming Cora" }
                  }
                }
                """);

            var reader = new LibConfigXmlReader(datPath);

            var chars = reader.LoadCharacterInfo();
            Assert.Equal("Lifeguard Deen", chars.Single(c => c.Id == 1095).CommonName);
            Assert.Equal("Lifeguard Deen", chars.Single(c => c.Id == 1095).CommonNameEng);
            Assert.Equal("Heidi", chars.Single(c => c.Id == 1311).CommonName);

            var quests = reader.LoadQuestInfo();
            var q = quests.Single(e => e.Id == 1233);
            Assert.Equal("Trickster Registration", q.Name);
            Assert.Equal("Blooming Cora", q.Pos);
            Assert.Equal("find bunny maid %d", q.Comment);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void OverrideValue_IsCopiedVerbatim_PreservingFormatTokens()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var datPath = WriteCorpus(dir);
            File.WriteAllText(Path.Combine(dir, TranslationOverrides.FileName), """
                { "QuestInfo": { "1233": { "Comment": "Find Bunny Maid %d" } } }
                """);

            var q = new LibConfigXmlReader(datPath).LoadQuestInfo().Single(e => e.Id == 1233);
            Assert.Equal("Find Bunny Maid %d", q.Comment);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Apply_MissingKeys_ReturnsSource()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var datPath = WriteCorpus(dir);
            File.WriteAllText(Path.Combine(dir, TranslationOverrides.FileName), """
                { "CharacterInfo": { "1095": { "CommonName": "Lifeguard Deen" } } }
                """);

            var ov = TranslationOverrides.ForLibConfigSource(datPath);
            Assert.Equal("Lifeguard Deen", ov.Apply("CharacterInfo", 1095, "CommonName", "rescuer dean"));
            Assert.Equal("rescuer dean", ov.Apply("CharacterInfo", 1095, "CommonName_Eng", "rescuer dean"));
            Assert.Equal("x", ov.Apply("CharacterInfo", 9999, "CommonName", "x"));
            Assert.Equal("y", ov.Apply("QuestInfo", 1233, "Name", "y"));
            Assert.Equal(1, ov.OverriddenCellCount);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
