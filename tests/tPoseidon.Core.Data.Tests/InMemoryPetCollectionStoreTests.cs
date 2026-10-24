using tPoseidon.Core.Data;

namespace tPoseidon.Core.Data.Tests;

public sealed class InMemoryPetCollectionStoreTests
{
    [Fact]
    public void BagsAreKeptPerCharacterInCollectionOrder()
    {
        var store = new InMemoryPetCollectionStore();
        var first = store.Add(1, 500, 1, 2);
        var second = store.Add(1, 11640, 2, 1);
        var other = store.Add(2, 501, 1, 1);

        Assert.Equal(new[] { first, second }, store.Load(1));
        Assert.Equal(new[] { other }, store.Load(2));
        Assert.Empty(store.Load(3));
        Assert.NotEqual(first.CollectedUid, second.CollectedUid);
        Assert.Equal(new PetCollectedItem(first.CollectedUid, 500, 1, 2), first);
    }

    [Fact]
    public void RemoveOnlyTakesTheOwnersItem()
    {
        var store = new InMemoryPetCollectionStore();
        var item = store.Add(1, 500, 1, 2);

        Assert.False(store.Remove(2, item.CollectedUid));
        Assert.True(store.Remove(1, item.CollectedUid));
        Assert.False(store.Remove(1, item.CollectedUid));
        Assert.Empty(store.Load(1));
    }
}
