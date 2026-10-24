using tPoseidon.Core.Data;
using tPoseidon.DrillEgg.Connectors;
using tPoseidon.DrillEgg.Draws;
using tPoseidon.DrillEgg.Prizes;
using tPoseidon.DrillEgg.Storage;

namespace tPoseidon.DrillEgg.Tests;

public sealed class DrawEngineTests
{
    private static readonly GachaPlayer Alice = new("Alice", 7, null);

    private static PrizeCatalog Catalog(TempFolder folder, string prizes = Fixtures.Prizes) =>
        PrizeCatalog.From(Fixtures.Settings(folder.Path, prizes).Settings);

    private static GachaPrize First(IReadOnlyList<GachaPrize> pool) => pool[0];

    [Fact]
    public async Task FreePlay_AlwaysWinsAndSpendsNothing()
    {
        using var folder = new TempFolder();
        var store = await Fixtures.StoreAsync(folder);
        var engine = new PipelineDrawEngine(new FreeCoupons(), new LocalDelivery(), Catalog(folder), store, First, TextWriter.Null);

        var outcome = await engine.DrawAsync(Alice, 61, true, "127.0.0.1", CancellationToken.None);

        Assert.Equal(GachaDrawOutcome.Won, outcome.Result.Outcome);
        Assert.Equal(1, outcome.Result.Prize!.ItemIndex);
        Assert.Equal("Small Healing Potion", outcome.ItemName);
        Assert.Equal(FreeCoupons.Shown, outcome.Result.CouponsLeft);
        Assert.Equal(DrawStatus.Delivered, (await store.FindAsync(outcome.DrawId!.Value, CancellationToken.None))!.Status);
    }

    [Fact]
    public async Task LocalCoupons_RunOutAndThenRefuseWithoutLoggingADraw()
    {
        using var folder = new TempFolder();
        var store = await Fixtures.StoreAsync(folder);
        var engine = new PipelineDrawEngine(new LocalCoupons(store, 3), new LocalDelivery(), Catalog(folder), store, First, TextWriter.Null);

        var deep = await engine.DrawAsync(Alice, 62, true, "-", CancellationToken.None);
        var normal = await engine.DrawAsync(Alice, 62, false, "-", CancellationToken.None);
        var refused = await engine.DrawAsync(Alice, 62, false, "-", CancellationToken.None);

        Assert.Equal(1, deep.Result.CouponsLeft);
        Assert.Equal(0, normal.Result.CouponsLeft);
        Assert.Equal(GachaDrawOutcome.NotEnoughCoupons, refused.Result.Outcome);
        Assert.Null(refused.Result.Prize);
        Assert.Equal(2, (await store.HistoryAsync(null, 10, CancellationToken.None)).Count);
    }

    [Fact]
    public async Task ADeepDrill_NeedsTwoCoupons()
    {
        using var folder = new TempFolder();
        var store = await Fixtures.StoreAsync(folder);
        var engine = new PipelineDrawEngine(new LocalCoupons(store, 1), new LocalDelivery(), Catalog(folder), store, First, TextWriter.Null);

        var refused = await engine.DrawAsync(Alice, 61, true, "-", CancellationToken.None);

        Assert.Equal(GachaDrawOutcome.NotEnoughCoupons, refused.Result.Outcome);
        Assert.Equal(1, refused.Result.CouponsLeft);
    }

    [Fact]
    public async Task AnEmptyPool_SpendsNothing_AndReportsCouponsFirst()
    {
        using var folder = new TempFolder();
        var store = await Fixtures.StoreAsync(folder);
        var catalog = Catalog(folder);
        var rich = new PipelineDrawEngine(new LocalCoupons(store, 5), new LocalDelivery(), catalog, store, First, TextWriter.Null);
        var poor = new PipelineDrawEngine(new LocalCoupons(store, 0), new LocalDelivery(), catalog, store, First, TextWriter.Null);

        Assert.Equal(GachaDrawOutcome.EmptyPool, (await rich.DrawAsync(Alice, 60, false, "-", CancellationToken.None)).Result.Outcome);
        Assert.Equal(GachaDrawOutcome.NotEnoughCoupons, (await poor.DrawAsync(new GachaPlayer("Poor", null, null), 60, false, "-", CancellationToken.None)).Result.Outcome);
        Assert.Equal(5, await store.CouponsAsync("Alice", 5, CancellationToken.None));
    }

    [Fact]
    public async Task AFailedDeliveryInTheSameDatabase_KeepsTheCoupons()
    {
        using var folder = new TempFolder();
        var store = await Fixtures.StoreAsync(folder);
        var game = Fixtures.Sqlite(folder.File("game.db"));
        await Fixtures.ExecuteAsync(game, "CREATE TABLE wallet (account TEXT, coupons INTEGER); INSERT INTO wallet VALUES ('Alice', 2); CREATE TABLE gifts (account TEXT, item INTEGER CHECK (item <> 1), draw INTEGER);");
        var coupons = new SqlCoupons(game, "SELECT coupons FROM wallet WHERE account = @account", "UPDATE wallet SET coupons = coupons - @cost WHERE account = @account AND coupons >= @cost");
        var delivery = new SqlDelivery(game, "INSERT INTO gifts VALUES (@account, @itemId, @drawId)");
        var engine = new PipelineDrawEngine(coupons, delivery, Catalog(folder), store, First, TextWriter.Null);

        var error = await Assert.ThrowsAsync<ConnectorException>(() => engine.DrawAsync(Alice, 61, false, "-", CancellationToken.None));

        Assert.Contains("CHECK constraint failed", error.Message, StringComparison.Ordinal);
        Assert.Equal(2, await Fixtures.ScalarAsync(game, "SELECT coupons FROM wallet"));
        Assert.Equal(0, await Fixtures.ScalarAsync(game, "SELECT COUNT(*) FROM gifts"));
        Assert.Empty(await store.HistoryAsync(null, 10, CancellationToken.None));
    }

    [Fact]
    public async Task ADeliveryInTheSameDatabase_CommitsWithTheSpend()
    {
        using var folder = new TempFolder();
        var store = await Fixtures.StoreAsync(folder);
        var game = Fixtures.Sqlite(folder.File("game.db"));
        await Fixtures.ExecuteAsync(game, "CREATE TABLE wallet (account TEXT, coupons INTEGER); INSERT INTO wallet VALUES ('Alice', 2); CREATE TABLE gifts (account_id INTEGER, item INTEGER, amount INTEGER, draw INTEGER, deep INTEGER);");
        var coupons = new SqlCoupons(game, "SELECT coupons FROM wallet WHERE account = @account", "UPDATE wallet SET coupons = coupons - @cost WHERE account = @account AND coupons >= @cost");
        var delivery = new SqlDelivery(game, "INSERT INTO gifts VALUES (@accountId, @itemId, @count, @drawId, @deep)");
        var engine = new PipelineDrawEngine(coupons, delivery, Catalog(folder), store, First, TextWriter.Null);

        var outcome = await engine.DrawAsync(Alice, 63, true, "-", CancellationToken.None);

        Assert.Equal(0, outcome.Result.CouponsLeft);
        Assert.Equal(0, await Fixtures.ScalarAsync(game, "SELECT coupons FROM wallet"));
        Assert.Equal(outcome.DrawId, await Fixtures.ScalarAsync(game, "SELECT draw FROM gifts WHERE account_id = 7 AND item = 1 AND amount = 1 AND deep = 1"));
        Assert.Equal(DrawStatus.Delivered, (await store.FindAsync(outcome.DrawId!.Value, CancellationToken.None))!.Status);
    }

    [Fact]
    public async Task AFailedDeliveryElsewhere_IsOwedAndRetried()
    {
        using var folder = new TempFolder();
        var store = await Fixtures.StoreAsync(folder);
        var delivery = new FailingDelivery(null);
        var engine = new PipelineDrawEngine(new LocalCoupons(store, 1), delivery, Catalog(folder), store, First, TextWriter.Null);
        var retrier = new DeliveryRetrier(store, engine, TextWriter.Null);

        var outcome = await engine.DrawAsync(Alice, 64, false, "-", CancellationToken.None);

        Assert.Equal(GachaDrawOutcome.Won, outcome.Result.Outcome);
        Assert.Equal(0, await store.CouponsAsync("Alice", 1, CancellationToken.None));
        var owed = Assert.Single(await store.OwedAsync(10, CancellationToken.None));
        Assert.Equal("the game server is down", owed.LastError);
        Assert.Equal(0, await retrier.RetryOnceAsync(CancellationToken.None));

        delivery.Fail = false;
        Assert.Equal(1, await retrier.RetryOnceAsync(CancellationToken.None));

        var delivered = Assert.Single(delivery.Delivered);
        Assert.Equal(outcome.DrawId, delivered.DrawId);
        Assert.Equal("Small Healing Potion", delivered.ItemName);
        Assert.Equal(7, delivered.Player.AccountId);
        Assert.Empty(await store.OwedAsync(10, CancellationToken.None));
    }

    [Fact]
    public async Task TheEmulatorEngine_DrawsThroughItsStoreAndLogsTheWin()
    {
        using var folder = new TempFolder();
        var store = await Fixtures.StoreAsync(folder);
        var gacha = new FakeGachaStore { Coupons = 2 };
        var engine = new StoreDrawEngine(gacha, Catalog(folder), store, TextWriter.Null);

        Assert.Equal(2, await engine.CouponsAsync(Alice, CancellationToken.None));
        var won = await engine.DrawAsync(Alice, 62, true, "10.0.0.5", CancellationToken.None);
        var refused = await engine.DrawAsync(Alice, 62, false, "10.0.0.5", CancellationToken.None);

        Assert.Equal(5001, won.Result.Prize!.ItemIndex);
        Assert.Equal("Item 5001", won.ItemName);
        Assert.Equal(GachaDrawOutcome.NotEnoughCoupons, refused.Result.Outcome);
        Assert.Equal(new GachaDrawRequest(7, 62, true, "10.0.0.5"), gacha.Draws[0]);
        Assert.Single(await store.HistoryAsync("Alice", 10, CancellationToken.None));
    }

    [Fact]
    public async Task TheEmulatorEngine_NeedsAnAccountId()
    {
        using var folder = new TempFolder();
        var engine = new StoreDrawEngine(new FakeGachaStore(), Catalog(folder), await Fixtures.StoreAsync(folder), TextWriter.Null);

        await Assert.ThrowsAsync<ConnectorException>(() => engine.CouponsAsync(new GachaPlayer("Nobody", null, null), CancellationToken.None));
    }
}
