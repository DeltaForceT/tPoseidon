namespace tPoseidon.Core.Data.Tests;

public class InMemoryQuestHistoryStoreTests
{
    private const ulong CharacterId = 0x10000001;
    private const byte ScenarioType = 6;
    private const int HeidiQuestId = 1233;

    private static QuestHistory MakeRow(byte comCount = 1, int monsterId = 0) => new()
    {
        CharacterId = CharacterId,
        QuestType = ScenarioType,
        QuestId = HeidiQuestId,
        CompletionCount = comCount,
        MonsterId = monsterId,
        KillCount = 0,
        RecordTime = 0,
        CompletedAt = new DateTimeOffset(2026, 4, 30, 12, 0, 0, TimeSpan.Zero),
    };

    [Fact]
    public void Find_ReturnsNull_WhenNoMatchingRow()
    {
        var store = new InMemoryQuestHistoryStore();
        Assert.Null(store.Find(CharacterId, ScenarioType, HeidiQuestId, monsterId: 0));
    }

    [Fact]
    public void Insert_PersistsRow_FindReturnsIt()
    {
        var store = new InMemoryQuestHistoryStore();
        var row = MakeRow();
        store.Insert(row);

        var found = store.Find(CharacterId, ScenarioType, HeidiQuestId, monsterId: 0);
        Assert.Equal(row, found);
    }

    [Fact]
    public void Insert_DuplicateKey_Throws()
    {
        var store = new InMemoryQuestHistoryStore();
        store.Insert(MakeRow());
        Assert.Throws<InvalidOperationException>(() => store.Insert(MakeRow()));
    }

    [Fact]
    public void Update_ReturnsTrue_WhenRowExists()
    {
        var store = new InMemoryQuestHistoryStore();
        store.Insert(MakeRow());

        var updated = store.Update(MakeRow(comCount: 2));

        Assert.True(updated);
        Assert.Equal((byte)2,
            store.Find(CharacterId, ScenarioType, HeidiQuestId, monsterId: 0)!.CompletionCount);
    }

    [Fact]
    public void Update_ReturnsFalse_WhenRowMissing()
    {
        var store = new InMemoryQuestHistoryStore();
        Assert.False(store.Update(MakeRow(comCount: 5)));
    }

    [Fact]
    public void GetForCharacter_ReturnsRowsOrderedByCompletedAtDesc()
    {
        var store = new InMemoryQuestHistoryStore();
        var older = MakeRow() with
        {
            QuestId = 100,
            CompletedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };
        var newer = MakeRow() with
        {
            QuestId = 200,
            CompletedAt = new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero),
        };
        store.Insert(older);
        store.Insert(newer);

        var rows = store.GetForCharacter(CharacterId);
        Assert.Equal(2, rows.Count);
        Assert.Equal(200, rows[0].QuestId);
        Assert.Equal(100, rows[1].QuestId);
    }

    [Fact]
    public void AppendLog_PreservesInsertionOrder()
    {
        var store = new InMemoryQuestHistoryStore();
        store.AppendLog(MakeRow(comCount: 1));
        store.AppendLog(MakeRow(comCount: 2));
        store.AppendLog(MakeRow(comCount: 3));

        var snapshot = store.SnapshotLog();
        Assert.Equal(new byte[] { 1, 2, 3 }, snapshot.Select(r => r.CompletionCount).ToArray());
    }

    [Fact]
    public void DifferentMonsterIds_AreSeparateHistoryRows()
    {
        var store = new InMemoryQuestHistoryStore();
        store.Insert(MakeRow(monsterId: 0));
        store.Insert(MakeRow(monsterId: 42));

        Assert.NotNull(store.Find(CharacterId, ScenarioType, HeidiQuestId, monsterId: 0));
        Assert.NotNull(store.Find(CharacterId, ScenarioType, HeidiQuestId, monsterId: 42));
    }

    [Fact]
    public void CompletedToday_ReturnsTrue_WhenSameDayAsReference()
    {
        var store = new InMemoryQuestHistoryStore();
        var today = new DateTimeOffset(2026, 4, 30, 15, 0, 0, TimeSpan.Zero);
        store.Insert(MakeRow() with { CompletedAt = today });

        Assert.True(store.CompletedToday(CharacterId, ScenarioType, HeidiQuestId,
            monsterId: 0, referenceUtc: today.UtcDateTime));
    }

    [Fact]
    public void CompletedToday_ReturnsFalse_WhenYesterday()
    {
        var store = new InMemoryQuestHistoryStore();
        var yesterday = new DateTimeOffset(2026, 4, 29, 23, 59, 0, TimeSpan.Zero);
        var today = new DateTimeOffset(2026, 4, 30, 0, 1, 0, TimeSpan.Zero);
        store.Insert(MakeRow() with { CompletedAt = yesterday });

        Assert.False(store.CompletedToday(CharacterId, ScenarioType, HeidiQuestId,
            monsterId: 0, referenceUtc: today.UtcDateTime));
    }

    [Fact]
    public void CompletedToday_ReturnsFalse_WhenNoHistoryRow()
    {
        var store = new InMemoryQuestHistoryStore();
        Assert.False(store.CompletedToday(CharacterId, ScenarioType, HeidiQuestId,
            monsterId: 0, referenceUtc: DateTime.UtcNow));
    }

    [Fact]
    public void HasCompletedAtLeast_ReturnsTrue_WhenThresholdZero()
    {
        var store = new InMemoryQuestHistoryStore();
        Assert.True(store.HasCompletedAtLeast(CharacterId, ScenarioType, HeidiQuestId,
            monsterId: 0, threshold: 0));
    }

    [Fact]
    public void HasCompletedAtLeast_ReturnsFalse_WhenNoHistoryRow()
    {
        var store = new InMemoryQuestHistoryStore();
        Assert.False(store.HasCompletedAtLeast(CharacterId, ScenarioType, HeidiQuestId,
            monsterId: 0, threshold: 1));
    }

    [Fact]
    public void HasCompletedAtLeast_ReturnsTrue_WhenComCountMeetsThreshold()
    {
        var store = new InMemoryQuestHistoryStore();
        store.Insert(MakeRow(comCount: 3));
        Assert.True(store.HasCompletedAtLeast(CharacterId, ScenarioType, HeidiQuestId,
            monsterId: 0, threshold: 3));
    }

    [Fact]
    public void HasCompletedAtLeast_ReturnsFalse_WhenComCountBelowThreshold()
    {
        var store = new InMemoryQuestHistoryStore();
        store.Insert(MakeRow(comCount: 2));
        Assert.False(store.HasCompletedAtLeast(CharacterId, ScenarioType, HeidiQuestId,
            monsterId: 0, threshold: 5));
    }
}
