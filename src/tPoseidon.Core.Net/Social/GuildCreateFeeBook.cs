namespace tPoseidon.Core.Net.Social;

public sealed class GuildCreateFeeBook
{
    private const int RememberedOrders = 256;

    private readonly long _fee;
    private readonly Func<ulong, long, CancellationToken, Task<bool>> _take;
    private readonly Queue<(ushort OrderId, ulong CharacterId)> _seen = new();
    private readonly object _gate = new();

    public GuildCreateFeeBook(SystemServerLink link, long fee, Func<ulong, long, CancellationToken, Task<bool>> take)
    {
        ArgumentNullException.ThrowIfNull(link);
        ArgumentOutOfRangeException.ThrowIfNegative(fee);
        _fee = fee;
        _take = take ?? throw new ArgumentNullException(nameof(take));
        link.OrderReceived += ApplyAsync;
    }

    public async Task<bool> ApplyAsync(OfficerOrder order, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(order);
        if (order.OrderType != SystemLinkFrames.OrderGuildCreateFee || _fee == 0 || !Remember(order)) return false;
        return await _take(order.CharacterId, _fee, ct).ConfigureAwait(false);
    }

    private bool Remember(OfficerOrder order)
    {
        lock (_gate)
        {
            if (_seen.Contains((order.OrderId, order.CharacterId))) return false;
            _seen.Enqueue((order.OrderId, order.CharacterId));
            if (_seen.Count > RememberedOrders) _seen.Dequeue();
            return true;
        }
    }
}
