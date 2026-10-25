using tPoseidon.Core.Common;
using tPoseidon.Core.Net.Social;

internal sealed class FriendPositionController
{
    private readonly SystemServerLink _link;
    private readonly Func<ulong, InGameSessionState?> _playerById;
    private readonly string _logTag;
    private readonly Random _rng = new();

    public FriendPositionController(SystemServerLink link, Func<ulong, InGameSessionState?> playerById, string logTag)
    {
        _link = link ?? throw new ArgumentNullException(nameof(link));
        _playerById = playerById ?? throw new ArgumentNullException(nameof(playerById));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
        _link.OrderReceived += OrderAsync;
    }

    private Task OrderAsync(OfficerOrder order, CancellationToken ct) => order.OrderType switch
    {
        FriendFrames.OrderObservePosition => ObserveAsync(order, ct),
        FriendFrames.OrderReturnPosition => ReturnAsync(order, ct),
        _ => Task.CompletedTask,
    };

    private async Task ObserveAsync(OfficerOrder order, CancellationToken ct)
    {
        if (!FriendFrames.TryReadObserveOrder(order.Data, out var requesterId, out var intervalMs)) return;
        if (_playerById(order.CharacterId) is null || _playerById(requesterId) is null) return;
        Console.WriteLine($"{_logTag} [friend] 0x{requesterId:X} follows the position of 0x{order.CharacterId:X}");
        await ShowPositionAsync(order.CharacterId, requesterId, ct).ConfigureAwait(false);
        _ = ShowPositionAfterAsync(order.CharacterId, requesterId, TimeSpan.FromMilliseconds(intervalMs), ct);
    }

    private async Task ShowPositionAfterAsync(ulong targetId, ulong requesterId, TimeSpan delay, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay, ct).ConfigureAwait(false);
            await ShowPositionAsync(targetId, requesterId, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"{_logTag} [friend] position of 0x{targetId:X} for 0x{requesterId:X} stopped: the link closed");
        }
        catch (Exception ex) when (ex is ObjectDisposedException or IOException or System.Net.Sockets.SocketException)
        {
            Console.WriteLine($"{_logTag} [friend] position of 0x{targetId:X} for 0x{requesterId:X} not delivered: {ex.Message}");
        }
    }

    private async Task ShowPositionAsync(ulong targetId, ulong requesterId, CancellationToken ct)
    {
        if (_playerById(targetId) is not { } target || _playerById(requesterId) is not { } requester) return;
        var payload = FriendFrames.Tracked(
            (ushort)(target.PlayerX / tPoseidon.Core.LibConfig.ZoneGeometry.CellSize),
            (ushort)(target.PlayerY / tPoseidon.Core.LibConfig.ZoneGeometry.CellSize),
            target.MapId,
            targetId);
        await SendAsync(requester, (Opcode)FriendFrames.PositionTrack, payload, ct).ConfigureAwait(false);
    }

    private Task ReturnAsync(OfficerOrder order, CancellationToken ct)
    {
        var location = _playerById(order.CharacterId) is { } target
            ? new FriendLocation(target.PlayerX, target.PlayerY, target.MapId)
            : default;
        Console.WriteLine($"{_logTag} [friend] position of 0x{order.CharacterId:X} for the zone move: zone {location.Zone} ({location.X},{location.Y})");
        return _link.ReportAsync(order with { OrderType = FriendFrames.ReportPosition, Data = FriendFrames.Location(location) }, ct);
    }

    private Task SendAsync(InGameSessionState player, Opcode opcode, byte[] payload, CancellationToken ct)
    {
        byte randKey;
        lock (_rng) randKey = (byte)_rng.Next(1, 256);
        return player.SendFrameAsync(opcode, payload, randKey, ct);
    }
}
