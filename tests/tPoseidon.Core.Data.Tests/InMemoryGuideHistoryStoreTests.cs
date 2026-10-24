using tPoseidon.Core.Data;

namespace tPoseidon.Core.DataTests;

public class InMemoryGuideHistoryStoreTests
{
    [Fact]
    public void GetForCharacter_NoRow_ReturnsNull()
    {
        var store = new InMemoryGuideHistoryStore();
        Assert.Null(store.GetForCharacter(0x1234));
    }

    [Fact]
    public void UpsertSystemGuide_FirstTime_InsertsZeroTutorial()
    {
        var store = new InMemoryGuideHistoryStore();
        store.UpsertSystemGuide(0x1234, "abc");

        var row = store.GetForCharacter(0x1234);
        Assert.NotNull(row);
        Assert.Equal("abc", row!.SystemGuide);
        Assert.Equal(0ul, row.TutorialGuide);
    }

    [Fact]
    public void UpsertTutorialGuide_FirstTime_InsertsEmptySystem()
    {
        var store = new InMemoryGuideHistoryStore();
        store.UpsertTutorialGuide(0x1234, 0xDEADBEEFCAFEBABE);

        var row = store.GetForCharacter(0x1234);
        Assert.NotNull(row);
        Assert.Equal(string.Empty, row!.SystemGuide);
        Assert.Equal(0xDEADBEEFCAFEBABE, row.TutorialGuide);
    }

    [Fact]
    public void Upsert_TwoFields_PreservesOtherOnUpdate()
    {
        var store = new InMemoryGuideHistoryStore();
        store.UpsertSystemGuide(0x1234, "system-A");
        store.UpsertTutorialGuide(0x1234, 0x42);

        var row = store.GetForCharacter(0x1234);
        Assert.Equal("system-A", row!.SystemGuide);
        Assert.Equal(0x42ul, row.TutorialGuide);

        store.UpsertSystemGuide(0x1234, "system-B");
        row = store.GetForCharacter(0x1234);
        Assert.Equal("system-B", row!.SystemGuide);
        Assert.Equal(0x42ul, row.TutorialGuide);

        store.UpsertTutorialGuide(0x1234, 0xFF);
        row = store.GetForCharacter(0x1234);
        Assert.Equal("system-B", row!.SystemGuide);
        Assert.Equal(0xFFul, row.TutorialGuide);
    }

    [Fact]
    public void UpsertSystemGuide_OverLimit_Throws()
    {
        var store = new InMemoryGuideHistoryStore();
        var oversize = new string('x', 1001);
        Assert.Throws<ArgumentException>(() => store.UpsertSystemGuide(0x1234, oversize));
    }

    [Fact]
    public void UpsertSystemGuide_AtLimit_OK()
    {
        var store = new InMemoryGuideHistoryStore();
        var atLimit = new string('x', 1000);
        store.UpsertSystemGuide(0x1234, atLimit);
        var row = store.GetForCharacter(0x1234);
        Assert.Equal(atLimit, row!.SystemGuide);
    }

    [Fact]
    public void Isolation_TwoCharacters_DoNotCollide()
    {
        var store = new InMemoryGuideHistoryStore();
        store.UpsertTutorialGuide(0x1111, 0x42);
        store.UpsertTutorialGuide(0x2222, 0x84);

        Assert.Equal(0x42ul, store.GetForCharacter(0x1111)!.TutorialGuide);
        Assert.Equal(0x84ul, store.GetForCharacter(0x2222)!.TutorialGuide);
    }
}
