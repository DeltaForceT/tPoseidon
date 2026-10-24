namespace tPoseidon.Core.Data.Tests;

public class InMemoryQuestStoreTests
{
    private static Quest MakeQuest(
        ulong charId = 0x10000001,
        byte type = 1,
        int questId = 1233,
        int monsterId = 0,
        byte completionCount = 0,
        short killCount = 0)
    {
        return new Quest
        {
            CharacterId = charId,
            QuestType = type,
            QuestId = questId,
            CompletionCount = completionCount,
            MonsterId = monsterId,
            KillCount = killCount,
            RecordTime = 0,
        };
    }

    [Fact]
    public void AddActive_ThenFindActive_ReturnsRow()
    {
        var store = new InMemoryQuestStore();
        var q = MakeQuest();
        store.AddActive(q);

        var found = store.FindActive(q.CharacterId, q.QuestType, q.QuestId, q.MonsterId);
        Assert.NotNull(found);
        Assert.Equal(q.QuestId, found.QuestId);
    }

    [Fact]
    public void AddActive_DuplicateKey_Throws()
    {
        var store = new InMemoryQuestStore();
        store.AddActive(MakeQuest());
        Assert.Throws<InvalidOperationException>(() => store.AddActive(MakeQuest()));
    }

    [Fact]
    public void GetActiveForCharacter_ReturnsAllForChar_NotOthers()
    {
        var store = new InMemoryQuestStore();
        store.AddActive(MakeQuest(charId: 1, questId: 1233));
        store.AddActive(MakeQuest(charId: 1, questId: 1234));
        store.AddActive(MakeQuest(charId: 2, questId: 1233));

        var charOne = store.GetActiveForCharacter(1);
        Assert.Equal(2, charOne.Count);

        var charTwo = store.GetActiveForCharacter(2);
        Assert.Single(charTwo);
        Assert.Equal(1233, charTwo[0].QuestId);

        var noChar = store.GetActiveForCharacter(99);
        Assert.Empty(noChar);
    }

    [Fact]
    public void CountActiveOfType_OnlyCountsMatchingType()
    {
        var store = new InMemoryQuestStore();
        store.AddActive(MakeQuest(type: 1, questId: 100));
        store.AddActive(MakeQuest(type: 1, questId: 101));
        store.AddActive(MakeQuest(type: 2, questId: 200, monsterId: 5));
        store.AddActive(MakeQuest(type: 6, questId: 300));

        Assert.Equal(2, store.CountActiveOfType(0x10000001, 1));
        Assert.Equal(1, store.CountActiveOfType(0x10000001, 2));
        Assert.Equal(1, store.CountActiveOfType(0x10000001, 6));
        Assert.Equal(0, store.CountActiveOfType(0x10000001, 7));
    }

    [Fact]
    public void RemoveActive_ExistingRow_ReturnsTrue_AndRemoves()
    {
        var store = new InMemoryQuestStore();
        store.AddActive(MakeQuest());

        Assert.True(store.RemoveActive(0x10000001, 1, 1233, 0));
        Assert.Null(store.FindActive(0x10000001, 1, 1233, 0));

        Assert.False(store.RemoveActive(0x10000001, 1, 1233, 0));
    }

    [Fact]
    public void UpdateMonsterProgress_OnlyUpdatesType2or5()
    {
        var store = new InMemoryQuestStore();
        store.AddActive(MakeQuest(type: 1, questId: 1233, monsterId: 0));
        store.AddActive(MakeQuest(type: 2, questId: 1234, monsterId: 292, killCount: 0));

        Assert.True(store.UpdateMonsterProgress(0x10000001, 1234, 292, killCount: 1, recordTime: 5));

        var monster = store.FindActive(0x10000001, 2, 1234, 292);
        Assert.NotNull(monster);
        Assert.Equal((short)1, monster.KillCount);
        Assert.Equal(5, monster.RecordTime);

        var normal = store.FindActive(0x10000001, 1, 1233, 0);
        Assert.NotNull(normal);
        Assert.Equal((short)0, normal.KillCount);
    }

    [Fact]
    public void UpdateMonsterProgress_NoMatchingRow_ReturnsFalse()
    {
        var store = new InMemoryQuestStore();
        Assert.False(store.UpdateMonsterProgress(0x10000001, 9999, 999, 1, 0));
    }

    [Fact]
    public void MultipleMonsterIds_SameQuestId_Coexist()
    {
        var store = new InMemoryQuestStore();
        store.AddActive(MakeQuest(type: 2, questId: 1234, monsterId: 100));
        store.AddActive(MakeQuest(type: 2, questId: 1234, monsterId: 200));

        Assert.NotNull(store.FindActive(0x10000001, 2, 1234, 100));
        Assert.NotNull(store.FindActive(0x10000001, 2, 1234, 200));
        Assert.Equal(2, store.GetActiveForCharacter(0x10000001).Count);
    }
}
