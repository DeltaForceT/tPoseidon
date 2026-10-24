using System.Data.Common;
using System.Globalization;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Gacha;
using tPoseidon.DrillEgg.Connectors;
using tPoseidon.DrillEgg.Prizes;
using tPoseidon.DrillEgg.Storage;

namespace tPoseidon.DrillEgg.Draws;

internal sealed record DrawOutcome(GachaDrawResult Result, long? DrawId, string? ItemName);

internal interface IDrawEngine
{
    Task<int> CouponsAsync(GachaPlayer player, CancellationToken ct);

    Task<DrawOutcome> DrawAsync(GachaPlayer player, short area, bool deep, string remote, CancellationToken ct);
}

internal sealed class PipelineDrawEngine : IDrawEngine
{
    private readonly ICouponConnector _coupons;
    private readonly IDeliveryConnector _delivery;
    private readonly PrizeCatalog _catalog;
    private readonly LocalStore _store;
    private readonly Func<IReadOnlyList<GachaPrize>, GachaPrize> _pick;
    private readonly TextWriter _log;

    public PipelineDrawEngine(ICouponConnector coupons, IDeliveryConnector delivery, PrizeCatalog catalog, LocalStore store, Func<IReadOnlyList<GachaPrize>, GachaPrize> pick, TextWriter log)
    {
        _coupons = coupons ?? throw new ArgumentNullException(nameof(coupons));
        _delivery = delivery ?? throw new ArgumentNullException(nameof(delivery));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _pick = pick ?? throw new ArgumentNullException(nameof(pick));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public Task<int> CouponsAsync(GachaPlayer player, CancellationToken ct) => _coupons.CountAsync(player, ct);

    public async Task<DrawOutcome> DrawAsync(GachaPlayer player, short area, bool deep, string remote, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(player);
        var cost = GachaRules.CouponsFor(deep);
        var pool = _catalog.Pool(area, deep);
        if (pool.Count == 0)
        {
            var have = await _coupons.CountAsync(player, ct).ConfigureAwait(false);
            return Refused(have < cost ? GachaDrawOutcome.NotEnoughCoupons : GachaDrawOutcome.EmptyPool, have);
        }
        var drawId = await _store.BeginDrawAsync(player, area, deep, ct).ConfigureAwait(false);
        var settled = false;
        try
        {
            var spend = await _coupons.SpendAsync(player, cost, drawId, ct).ConfigureAwait(false);
            if (spend is null)
            {
                await _store.DiscardDrawAsync(drawId, ct).ConfigureAwait(false);
                settled = true;
                return Refused(GachaDrawOutcome.NotEnoughCoupons, await _coupons.CountAsync(player, ct).ConfigureAwait(false));
            }
            Delivery delivery;
            bool delivered;
            await using (spend.ConfigureAwait(false))
            {
                var prize = _pick(pool);
                delivery = new Delivery(drawId, player, prize, _catalog.NameOf(prize.ItemIndex), area, deep);
                var joined = spend.Unit is { } unit && _delivery.Database is { } database && ReferenceEquals(unit.Database, database) ? unit : null;
                if (joined is not null) await _delivery.DeliverAsync(delivery, joined, ct).ConfigureAwait(false);
                delivered = joined is not null;
                await _store.RecordPrizeAsync(drawId, prize, delivery.ItemName, delivered ? DrawStatus.Delivered : DrawStatus.Owed, spend.Unit, ct).ConfigureAwait(false);
                await CommitAsync(spend, drawId, ct).ConfigureAwait(false);
                settled = true;
            }
            if (!delivered) await DeliverAsync(delivery, ct).ConfigureAwait(false);
            _log.WriteLine($"[drill-egg] draw {drawId.ToString(CultureInfo.InvariantCulture)} {player.Account} area {area.ToString(CultureInfo.InvariantCulture)}{(deep ? " deep" : string.Empty)}: {delivery.ItemName} ({delivery.Prize.ItemIndex.ToString(CultureInfo.InvariantCulture)}), {spend.CouponsLeft.ToString(CultureInfo.InvariantCulture)} coupon(s) left");
            return new DrawOutcome(new GachaDrawResult(GachaDrawOutcome.Won, delivery.Prize, 0, spend.CouponsLeft), drawId, delivery.ItemName);
        }
        finally
        {
            if (!settled) await _store.DiscardDrawAsync(drawId, CancellationToken.None).ConfigureAwait(false);
        }
    }

    public async Task<bool> DeliverAsync(Delivery delivery, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        try
        {
            await _delivery.DeliverAsync(delivery, null, ct).ConfigureAwait(false);
        }
        catch (ConnectorException e)
        {
            _log.WriteLine($"[drill-egg] draw {delivery.DrawId.ToString(CultureInfo.InvariantCulture)} for {delivery.Player.Account} is won but not delivered yet, it will be retried: {e.Message}");
            await _store.MarkFailedAsync(delivery.DrawId, e.Message, ct).ConfigureAwait(false);
            return false;
        }
        await _store.MarkDeliveredAsync(delivery.DrawId, ct).ConfigureAwait(false);
        return true;
    }

    private static async Task CommitAsync(ICouponSpend spend, long drawId, CancellationToken ct)
    {
        try
        {
            await spend.CommitAsync(ct).ConfigureAwait(false);
        }
        catch (DbException e)
        {
            throw new ConnectorException($"Draw {drawId.ToString(CultureInfo.InvariantCulture)} could not spend its coupons: {e.Message}", e);
        }
    }

    private static DrawOutcome Refused(GachaDrawOutcome outcome, int coupons) => new(new GachaDrawResult(outcome, null, 0, coupons), null, null);
}

internal sealed class StoreDrawEngine : IDrawEngine
{
    private readonly IGachaStore _gacha;
    private readonly PrizeCatalog _catalog;
    private readonly LocalStore _store;
    private readonly TextWriter _log;

    public StoreDrawEngine(IGachaStore gacha, PrizeCatalog catalog, LocalStore store, TextWriter log)
    {
        _gacha = gacha ?? throw new ArgumentNullException(nameof(gacha));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public Task<int> CouponsAsync(GachaPlayer player, CancellationToken ct)
    {
        var account = AccountOf(player);
        try
        {
            return Task.FromResult(_gacha.CouponCount(account));
        }
        catch (DbException e)
        {
            throw new ConnectorException($"The tPoseidon coupon count failed: {e.Message}", e);
        }
    }

    public async Task<DrawOutcome> DrawAsync(GachaPlayer player, short area, bool deep, string remote, CancellationToken ct)
    {
        var account = AccountOf(player);
        GachaDrawResult result;
        try
        {
            result = _gacha.Draw(new GachaDrawRequest(account, area, deep, remote), GachaPicker.Pick);
        }
        catch (DbException e)
        {
            throw new ConnectorException($"The tPoseidon draw failed: {e.Message}", e);
        }
        if (result.Prize is not { } prize) return new DrawOutcome(result, null, null);
        var name = _catalog.NameOf(prize.ItemIndex);
        var drawId = await _store.RecordDeliveredAsync(player, area, deep, prize, name, ct).ConfigureAwait(false);
        _log.WriteLine($"[drill-egg] draw {drawId.ToString(CultureInfo.InvariantCulture)} {player.Account} area {area.ToString(CultureInfo.InvariantCulture)}{(deep ? " deep" : string.Empty)}: {name} ({prize.ItemIndex.ToString(CultureInfo.InvariantCulture)}) into item box entry {result.ChargedItemUid.ToString(CultureInfo.InvariantCulture)}, {result.CouponsLeft.ToString(CultureInfo.InvariantCulture)} coupon(s) left");
        return new DrawOutcome(result, drawId, name);
    }

    private static uint AccountOf(GachaPlayer player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return player.AccountId is { } id and >= 0 and <= uint.MaxValue
            ? (uint)id
            : throw new ConnectorException($"Player {player.Account} has no tPoseidon account id.");
    }
}

internal sealed class DeliveryRetrier
{
    public const int DefaultRetrySeconds = 60;
    public const int Batch = 50;

    private readonly LocalStore _store;
    private readonly PipelineDrawEngine _engine;
    private readonly TextWriter _log;

    public DeliveryRetrier(LocalStore store, PipelineDrawEngine engine, TextWriter log)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task<int> RetryOnceAsync(CancellationToken ct)
    {
        var delivered = 0;
        foreach (var draw in await _store.OwedAsync(Batch, ct).ConfigureAwait(false))
        {
            if (await _engine.DeliverAsync(draw.ToDelivery(), ct).ConfigureAwait(false)) delivered++;
        }
        return delivered;
    }

    public async Task RunAsync(TimeSpan every, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(every);
        do
        {
            try
            {
                var delivered = await RetryOnceAsync(ct).ConfigureAwait(false);
                if (delivered > 0) _log.WriteLine($"[drill-egg] delivered {delivered.ToString(CultureInfo.InvariantCulture)} waiting prize(s)");
            }
            catch (DbException e)
            {
                _log.WriteLine($"[drill-egg] the waiting prizes could not be read from the local data file: {e.Message}");
            }
        }
        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false));
    }
}
