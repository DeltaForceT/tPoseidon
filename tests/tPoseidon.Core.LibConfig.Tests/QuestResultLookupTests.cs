using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.LibConfig.Tests;

public class QuestResultLookupTests
{
    private const string SyntheticXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <INI TableCount="2" TableInfoCount="1">
        <TABLE name="QuestResult_1233" RowCount="1" TableInfoID="153" FieldCnt="28">
            <ROW>
                <Count>1</Count>
                <Type>0</Type>
                <Item0>3030</Item0>
                <Cnt0>1</Cnt0>
                <Item1>4030</Item1>
                <Cnt1>1</Cnt1>
                <Item2>6530</Item2>
                <Cnt2>1</Cnt2>
                <Item3>0</Item3>
                <Cnt3>0</Cnt3>
                <Gelder>0</Gelder>
                <Skill>0</Skill>
                <SkillLv>0</SkillLv>
                <Exp>0</Exp>
                <Tmxp>0</Tmxp>
            </ROW>
        </TABLE>
        <TABLE name="QuestResult_1234" RowCount="1" TableInfoID="153" FieldCnt="28">
            <ROW>
                <Count>1</Count>
                <Type>0</Type>
                <Item0>11000</Item0>
                <Cnt0>3</Cnt0>
                <Item1>0</Item1>
                <Cnt1>0</Cnt1>
                <Item2>0</Item2>
                <Cnt2>0</Cnt2>
                <Item3>0</Item3>
                <Cnt3>0</Cnt3>
                <Gelder>500</Gelder>
                <Skill>0</Skill>
                <SkillLv>0</SkillLv>
                <Exp>1374</Exp>
                <Tmxp>1374</Tmxp>
            </ROW>
        </TABLE>
        </INI>
        """;

    private static string WriteSyntheticXml()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, SyntheticXml);
        return path;
    }

    [Fact]
    public void LoadFromXml_ParsesAllQuestResultTables()
    {
        var path = WriteSyntheticXml();
        try
        {
            var lookup = InMemoryQuestResultLookup.LoadFromXml(path);
            Assert.Equal(2, lookup.Count);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void GetByQuestId_ReturnsRewardSet()
    {
        var path = WriteSyntheticXml();
        try
        {
            var lookup = InMemoryQuestResultLookup.LoadFromXml(path);
            var rewards = lookup.GetByQuestId(1233);
            Assert.NotNull(rewards);
            var items = rewards!.NonEmptyItems().ToList();
            Assert.Equal(3, items.Count);
            Assert.Equal((3030, 1), items[0]);
            Assert.Equal((4030, 1), items[1]);
            Assert.Equal((6530, 1), items[2]);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void NonEmptyItems_SkipsZeroSlots()
    {
        var path = WriteSyntheticXml();
        try
        {
            var lookup = InMemoryQuestResultLookup.LoadFromXml(path);
            var rewards = lookup.GetByQuestId(1234);
            Assert.NotNull(rewards);
            Assert.Single(rewards!.NonEmptyItems());
            Assert.Equal(500, rewards.Gelder);
            Assert.Equal(1374, rewards.Exp);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void GetByQuestId_UnknownQuest_ReturnsNull()
    {
        var path = WriteSyntheticXml();
        try
        {
            var lookup = InMemoryQuestResultLookup.LoadFromXml(path);
            Assert.Null(lookup.GetByQuestId(99999));
        }
        finally { File.Delete(path); }
    }
}
