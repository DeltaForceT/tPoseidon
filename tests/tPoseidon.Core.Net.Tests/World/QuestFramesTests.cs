using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class QuestFramesTests
{
    private static readonly QuestTexts Texts = new("N", "C", "P", "M");
    private static readonly int[] OneQuest = { 9001 };

    [Fact]
    public void ANormalQuestAddedCarriesItsIdTwice()
    {
        Assert.Equal(
            new byte[] { 0x29, 0x23, 0x00, 0x00, 0x01, 0x00, (byte)'N', 0, (byte)'C', 0, (byte)'P', 0, (byte)'M', 0, 0x29, 0x23 },
            QuestFrames.QuestAdded(9001, 1, Texts));
    }

    [Fact]
    public void AScenarioQuestAddedEndsWithItsTexts()
    {
        Assert.Equal(
            new byte[] { 0x29, 0x23, 0x00, 0x00, 0x02, 0x00, (byte)'N', 0, (byte)'C', 0, (byte)'P', 0, (byte)'M', 0 },
            QuestFrames.ScenarioQuestAdded(9001, 2, Texts));
    }

    [Fact]
    public void AQuestRemovalIsItsId()
    {
        Assert.Equal(new byte[] { 0x29, 0x23, 0x00, 0x00 }, QuestFrames.QuestRemoved(9001));
    }

    [Fact]
    public void AMonsterQuestAddedPutsTheGradeBeforeTheCount()
    {
        var entry = new MonsterQuestListEntry(QuestId: 0x0208, Monster: 0x0889, Count: 1, Grade: 2, Kills: 3, ClearTimeMs: 0x0102);

        Assert.Equal(
            new byte[]
            {
                0x08, 0x02, 0x89, 0x08, 0x02, 0x01, 0x03, 0x00, 0x02, 0x01, 0x00, 0x00,
                (byte)'N', 0, (byte)'C', 0, (byte)'P', 0, (byte)'M', 0,
            },
            QuestFrames.MonsterQuestAdded(entry, Texts));
    }

    [Fact]
    public void TheMonsterQuestLoginListPutsTheCountBeforeTheGrade()
    {
        var entry = new MonsterQuestListEntry(QuestId: 0x0208, Monster: 0x0889, Count: 1, Grade: 2, Kills: 3, ClearTimeMs: 0x0102);

        Assert.Equal(
            new byte[] { 0x01, 0x00, 0x08, 0x02, 0x89, 0x08, 0x01, 0x02, 0x03, 0x00, 0x02, 0x01, 0x00, 0x00 },
            QuestFrames.MonsterQuestList(new[] { entry }));
    }

    [Fact]
    public void TheHuntTimerCarriesRemainingAndLimit()
    {
        Assert.Equal(
            new byte[] { 0xE8, 0x03, 0x00, 0x00, 0x60, 0xEA, 0x00, 0x00 },
            QuestFrames.MonsterQuestTimer(1000, 60_000));
    }

    [Fact]
    public void TheKillCounterCarriesKillsThenTarget()
    {
        Assert.Equal(new byte[] { 0x02, 0x00, 0x05, 0x00 }, QuestFrames.MonsterQuestKills(2, 5));
    }

    [Fact]
    public void SuccessCarriesTheReturnOffer()
    {
        Assert.Equal(new byte[] { 0x08, 0x02, 0x01 }, QuestFrames.MonsterQuestSucceeded(0x0208, offersReturn: true));
        Assert.Equal(new byte[] { 0x08, 0x02, 0x00 }, QuestFrames.MonsterQuestSucceeded(0x0208, offersReturn: false));
    }

    [Fact]
    public void MonsterQuestRemovalAndTimeOutAreTheShortId()
    {
        Assert.Equal(new byte[] { 0x08, 0x02 }, QuestFrames.MonsterQuestRemoved(0x0208));
        Assert.Equal(new byte[] { 0x08, 0x02 }, QuestFrames.MonsterQuestTimedOut(0x0208));
    }

    [Fact]
    public void QuestListsCountTheirEntries()
    {
        Assert.Equal(
            new byte[] { 0x02, 0x00, 0x29, 0x23, 0x00, 0x00, 0x01, 0x00, 0x2A, 0x23, 0x00, 0x00, 0x03, 0x00 },
            QuestFrames.QuestList(new[] { new QuestListEntry(9001, 1), new QuestListEntry(9002, 3) }));
        Assert.Equal(new byte[] { 0x00, 0x00 }, QuestFrames.QuestList(Array.Empty<QuestListEntry>()));
        Assert.Equal(new byte[] { 0x01, 0x00, 0x29, 0x23, 0x00, 0x00 }, QuestFrames.QuestIdList(OneQuest));
    }

    [Fact]
    public void AnNpcMessageCarriesItsKindAndQuest()
    {
        Assert.Equal(
            new byte[] { 0x34, 0x12, 0x07, 0x00, (byte)'h', (byte)'i', 0, 0x01, 0x00, 0x08, 0x02 },
            QuestFrames.NpcMessage(0x1234, 7, "hi", kind: 1, questId: 0x0208));
    }

    [Fact]
    public void AMissingNpcMessageNamesTheNpcAndTheIndex()
    {
        Assert.Equal(new byte[] { 0x34, 0x12, 0x81, 0x00 }, QuestFrames.NpcMessageMissing(0x1234, 0x81));
    }
}
