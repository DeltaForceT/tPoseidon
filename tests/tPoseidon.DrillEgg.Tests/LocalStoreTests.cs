using tPoseidon.Core.Data;
using tPoseidon.DrillEgg.Connectors;
using tPoseidon.DrillEgg.Storage;

namespace tPoseidon.DrillEgg.Tests;

public sealed class LocalStoreTests
{
    private static readonly GachaPlayer Alice = new("Alice", null, null);
    private static readonly GachaPrize Egg = new(9, 1, 1, 0, 0, 1);

    [Fact]
    public async Task ANewPlayer_StartsWithTheStartingCoupons()
    {
        using var folder = new TempFolder();
        var store = await Fixtures.StoreAsync(folder);

        Assert.Equal(5, await store.CouponsAsync("Alice", 5, CancellationToken.None));
        Assert.Equal(5, await store.CouponsAsync("Alice", 99, CancellationToken.None));
    }

    [Fact]
    public async Task ASpend_TakesCouponsOnlyWhenCommitted()
    {
        using var folder = new TempFolder();
        var store = await Fixtures.StoreAsync(folder);

        await using (var rolledBack = await store.SpendAsync("Alice", 3, 2, CancellationToken.None))
        {
            Assert.Equal(1, rolledBack!.CouponsLeft);
        }
        Assert.Equal(3, await store.CouponsAsync("Alice", 3, CancellationToken.None));

        await using (var spend = await store.SpendAsync("Alice", 3, 2, CancellationToken.None))
        {
            await spend!.CommitAsync(CancellationToken.None);
        }
        Assert.Equal(1, await store.CouponsAsync("Alice", 3, CancellationToken.None));
        Assert.Null(await store.SpendAsync("Alice", 3, 2, CancellationToken.None));
        Assert.Equal(1, await store.CouponsAsync("Alice", 3, CancellationToken.None));
    }

    [Fact]
    public async Task Grants_AddAndTakeAwayButNeverGoBelowZero()
    {
        using var folder = new TempFolder();
        var store = await Fixtures.StoreAsync(folder);

        Assert.Equal(12, await store.GrantAsync("Bob", 2, 10, CancellationToken.None));
        Assert.Equal(0, await store.GrantAsync("Bob", 2, -50, CancellationToken.None));
    }

    [Fact]
    public async Task ParallelSpends_OfTheLastCoupon_LetOnlyOneThrough()
    {
        using var folder = new TempFolder();
        var store = await Fixtures.StoreAsync(folder);
        await store.CouponsAsync("Alice", 1, CancellationToken.None);

        async Task<bool> TryAsync()
        {
            var spend = await store.SpendAsync("Alice", 1, 1, CancellationToken.None);
            if (spend is null) return false;
            await using (spend)
            {
                await spend.CommitAsync(CancellationToken.None);
            }
            return true;
        }

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Task.Run(TryAsync)));

        Assert.Equal(1, results.Count(won => won));
        Assert.Equal(0, await store.CouponsAsync("Alice", 1, CancellationToken.None));
    }

    [Fact]
    public async Task TheDrawLog_FollowsADrawFromPendingToDelivered()
    {
        using var folder = new TempFolder();
        var store = await Fixtures.StoreAsync(folder);

        var id = await store.BeginDrawAsync(Alice, 62, true, CancellationToken.None);
        Assert.Equal(DrawStatus.Pending, (await store.FindAsync(id, CancellationToken.None))!.Status);

        await store.RecordPrizeAsync(id, Egg, "Crystal Egg", DrawStatus.Owed, null, CancellationToken.None);
        var owed = Assert.Single(await store.OwedAsync(10, CancellationToken.None));
        Assert.Equal("Crystal Egg", owed.ItemName);
        Assert.Equal(62, owed.Area);
        Assert.True(owed.Deep);

        await store.MarkFailedAsync(id, "down", CancellationToken.None);
        Assert.Equal("down", (await store.FindAsync(id, CancellationToken.None))!.LastError);

        await store.MarkDeliveredAsync(id, CancellationToken.None);
        var delivered = (await store.FindAsync(id, CancellationToken.None))!;
        Assert.Equal(DrawStatus.Delivered, delivered.Status);
        Assert.Equal(2, delivered.Attempts);
        Assert.Empty(await store.OwedAsync(10, CancellationToken.None));
    }

    [Fact]
    public async Task ADiscardedDraw_Disappears_ButADeliveredOneStays()
    {
        using var folder = new TempFolder();
        var store = await Fixtures.StoreAsync(folder);
        var pending = await store.BeginDrawAsync(Alice, 61, false, CancellationToken.None);
        var kept = await store.RecordDeliveredAsync(Alice, 61, false, Egg, "Crystal Egg", CancellationToken.None);

        await store.DiscardDrawAsync(pending, CancellationToken.None);
        await store.DiscardDrawAsync(kept, CancellationToken.None);

        Assert.Null(await store.FindAsync(pending, CancellationToken.None));
        Assert.NotNull(await store.FindAsync(kept, CancellationToken.None));
    }

    [Fact]
    public async Task PendingDraws_AreMarkedInterruptedOnStart()
    {
        using var folder = new TempFolder();
        var store = await Fixtures.StoreAsync(folder);
        var id = await store.BeginDrawAsync(Alice, 61, false, CancellationToken.None);

        var reopened = await Fixtures.StoreAsync(folder);

        Assert.Equal(1, await reopened.InterruptPendingAsync(CancellationToken.None));
        Assert.Equal(DrawStatus.Interrupted, (await reopened.FindAsync(id, CancellationToken.None))!.Status);
    }

    [Fact]
    public async Task History_ShowsTheNewestDrawsFirst()
    {
        using var folder = new TempFolder();
        var store = await Fixtures.StoreAsync(folder);
        await store.RecordDeliveredAsync(Alice, 61, false, Egg, "First", CancellationToken.None);
        await store.RecordDeliveredAsync(new GachaPlayer("Bob", 4, "Hero"), 62, false, Egg, "Second", CancellationToken.None);
        await store.RecordDeliveredAsync(Alice, 63, false, Egg, "Third", CancellationToken.None);

        Assert.Equal(["Third", "Second", "First"], (await store.HistoryAsync(null, 10, CancellationToken.None)).Select(d => d.ItemName));
        var bob = Assert.Single(await store.HistoryAsync("Bob", 10, CancellationToken.None));
        Assert.Equal(4, bob.Player.AccountId);
        Assert.Equal("Hero", bob.Player.Character);
    }
}
