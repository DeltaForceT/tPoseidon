using tPoseidon.DrillEgg.Storage;

namespace tPoseidon.DrillEgg.Connectors;

internal static class PlayerNames
{
    public const int MaxLength = 24;
    public const string Rule = "1 to 24 letters, digits, spaces, dots, dashes or underscores";

    public static bool IsValid(string name) =>
        !string.IsNullOrWhiteSpace(name)
        && name.Length <= MaxLength
        && name.Trim().Length == name.Length
        && name.All(c => char.IsLetterOrDigit(c) || c is ' ' or '.' or '-' or '_');
}

internal sealed class LocalPlayers : IPlayerConnector
{
    public const string DefaultName = "Player";

    private readonly string _single;

    private LocalPlayers(SignInKind kind, string single)
    {
        Kind = kind;
        _single = single;
    }

    public SignInKind Kind { get; }

    public static LocalPlayers Single(string? name) => new(SignInKind.Automatic, name ?? DefaultName);

    public static LocalPlayers Named() => new(SignInKind.Name, DefaultName);

    public Task<GachaPlayer?> SignInAsync(SignInRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (Kind == SignInKind.Automatic) return Task.FromResult<GachaPlayer?>(new GachaPlayer(_single, null, null));
        var name = request.Account?.Trim();
        return Task.FromResult(name is not null && PlayerNames.IsValid(name) ? new GachaPlayer(name, null, null) : null);
    }
}

internal sealed class FreeCoupons : ICouponConnector
{
    public const int Shown = 99;

    public Task<int> CountAsync(GachaPlayer player, CancellationToken ct) => Task.FromResult(Shown);

    public Task<ICouponSpend?> SpendAsync(GachaPlayer player, int cost, long drawId, CancellationToken ct) =>
        Task.FromResult<ICouponSpend?>(new FreeSpend());

    private sealed class FreeSpend : ICouponSpend
    {
        public int CouponsLeft => Shown;

        public SqlUnit? Unit => null;

        public Task CommitAsync(CancellationToken ct) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

internal sealed class LocalCoupons : ICouponConnector
{
    public const int DefaultStartingCoupons = 10;

    private readonly LocalStore _store;
    private readonly int _starting;

    public LocalCoupons(LocalStore store, int starting)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        ArgumentOutOfRangeException.ThrowIfNegative(starting);
        _starting = starting;
    }

    public Task<int> CountAsync(GachaPlayer player, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(player);
        return _store.CouponsAsync(player.Account, _starting, ct);
    }

    public Task<ICouponSpend?> SpendAsync(GachaPlayer player, int cost, long drawId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(player);
        return _store.SpendAsync(player.Account, _starting, cost, ct);
    }

    public Task<int> GrantAsync(string account, int count, CancellationToken ct) => _store.GrantAsync(account, _starting, count, ct);
}

internal sealed class LocalDelivery : IDeliveryConnector
{
    public SqlDatabase? Database => null;

    public Task DeliverAsync(Delivery delivery, SqlUnit? joined, CancellationToken ct) => Task.CompletedTask;
}
