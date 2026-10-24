using tPoseidon.Core.Data;

namespace tPoseidon.Core.DataTests;

public class InMemoryUniqueIssueTests
{
    [Fact]
    public void KillsIssueOneItemWhenTheTargetIsReached()
    {
        var store = new InMemoryUniqueItemStore(
            new[] { new UniqueItemInfo(50001, UniqueItemRules.NormalUnique, 99999, 0, 0, 0, 100, 0, UniqueItemRules.Transferred, 0) },
            below => below - 98);
        var now = new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);
        Assert.False(store.AddCount(50001, 1, now));
        Assert.Equal(3, store.TargetOf(50001));
        Assert.False(store.AddCount(50001, 1, now));
        Assert.True(store.AddCount(50001, 1, now));
        Assert.False(store.AddCount(50001, 1, now));
        Assert.False(store.AddCount(99999, 1, now));
    }
}
