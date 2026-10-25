using System.Collections.Concurrent;
using System.Net;
using tPoseidon.Core.Net.S2;

namespace tPoseidon.Core.Net.Social;

public sealed class SystemServerLink
{
    private readonly ConcurrentDictionary<ulong, (ushort OrderId, PartyMirror Mirror)> _mirrors = new();
    private readonly ConcurrentDictionary<S2SessionContext, ulong> _present = new();
    private readonly Func<IPAddress, bool> _isTrusted;
    private readonly Action<string> _log;
    private S2SessionContext? _system;

    public SystemServerLink(Func<IPAddress, bool> isTrusted, Action<string> log)
    {
        _isTrusted = isTrusted ?? throw new ArgumentNullException(nameof(isTrusted));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public bool IsConnected => _system is { IsConnected: true };

    public event Func<OfficerOrder, CancellationToken, Task>? OrderReceived;

    public event Func<ushort, OfficerOrder, CancellationToken, Task>? ZonedOrderReceived;

    public event Func<ushort, ReadOnlyMemory<byte>, CancellationToken, Task>? MessageReceived;

    public void Register(S2SessionContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ctx.Dispatcher.Register(SystemLinkFrames.SystemHello, (_, _, _) =>
        {
            if (ctx.Remote is not { } remote || !_isTrusted(remote.Address))
            {
                _log($"[system-link] refused 0x{SystemLinkFrames.SystemHello:X4} from {ctx.Remote}");
                return ctx.Session.DisposeAsync().AsTask();
            }
            _system = ctx;
            _log($"[system-link] SystemServer connected from {remote}");
            return Task.CompletedTask;
        });
        ctx.Dispatcher.Register(SystemLinkFrames.Order, (_, payload, ct) =>
        {
            if (!ReferenceEquals(_system, ctx)) return Task.CompletedTask;
            var (zone, order) = SystemLinkFrames.DecodeOrder(payload.Span);
            return HandleAsync(zone, order, ct);
        });
        ctx.Dispatcher.Register(GameMasterFrames.LinkChatReply, (_, payload, ct) =>
        {
            if (!ReferenceEquals(_system, ctx)) return Task.CompletedTask;
            return MessageReceived is { } received ? received(GameMasterFrames.LinkChatReply, payload, ct) : Task.CompletedTask;
        });
        ctx.Dispatcher.Register(GameMasterFrames.KickCharacter, (_, payload, ct) =>
        {
            if (!ReferenceEquals(_system, ctx)) return Task.CompletedTask;
            return MessageReceived is { } received ? received(GameMasterFrames.KickCharacter, payload, ct) : Task.CompletedTask;
        });
        ctx.Dispatcher.Register(AngelFrames.Portal, (_, payload, ct) =>
        {
            if (!ReferenceEquals(_system, ctx)) return Task.CompletedTask;
            return MessageReceived is { } received ? received(AngelFrames.Portal, payload, ct) : Task.CompletedTask;
        });
        foreach (var rateMessage in RateMessages)
        {
            ctx.Dispatcher.Register(rateMessage, (_, payload, ct) =>
            {
                if (!ReferenceEquals(_system, ctx)) return Task.CompletedTask;
                return MessageReceived is { } received ? received(rateMessage, payload, ct) : Task.CompletedTask;
            });
        }
    }

    private static readonly ushort[] RateMessages = { GameMasterEventFrames.ExpApply, GameMasterEventFrames.AbilityApply };

    public static Func<IPAddress, bool> InternalAddresses(string internalHost)
    {
        var known = Dns.GetHostAddresses(internalHost);
        return address => IPAddress.IsLoopback(address) || known.Contains(address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address);
    }

    public async Task ClosedAsync(S2SessionContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (ReferenceEquals(_system, ctx))
        {
            _system = null;
            _mirrors.Clear();
            _log("[system-link] SystemServer disconnected; party mirrors cleared");
            return;
        }
        if (_present.TryRemove(ctx, out var characterId))
        {
            _mirrors.TryRemove(characterId, out _);
            await SendAsync(SystemLinkFrames.LoggedOut, SystemLinkFrames.Character(characterId), ct).ConfigureAwait(false);
        }
    }

    public PartyMirror? MirrorOf(ulong characterId) =>
        _mirrors.TryGetValue(characterId, out var entry) ? entry.Mirror : null;

    public Task EnteredZoneAsync(S2SessionContext ctx, ulong characterId, ushort zone, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        _present[ctx] = characterId;
        return SendAsync(SystemLinkFrames.EnterZone, SystemLinkFrames.CharacterAndZone(characterId, zone), ct);
    }

    public Task LeftZoneAsync(ulong characterId, ushort nextZone, CancellationToken ct) =>
        SendAsync(SystemLinkFrames.LeaveZone, SystemLinkFrames.CharacterAndZone(characterId, nextZone), ct);

    public Task LevelChangedAsync(ulong characterId, uint level, CancellationToken ct) =>
        SendAsync(SystemLinkFrames.LevelChanged, SystemLinkFrames.CharacterAndLevel(characterId, level), ct);

    public Task DiedAsync(ulong characterId, CancellationToken ct) =>
        SendAsync(SystemLinkFrames.Died, SystemLinkFrames.Character(characterId), ct);

    public Task RevivedAsync(ulong characterId, CancellationToken ct) =>
        SendAsync(SystemLinkFrames.Revived, SystemLinkFrames.Character(characterId), ct);

    public Task ReportAsync(OfficerOrder order, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(order);
        return SendAsync(SystemLinkFrames.Report, SystemLinkFrames.EncodeOrder(0, order), ct);
    }

    public Task SendAsync(ushort opcode, byte[] body, CancellationToken ct) => TrySendAsync(opcode, body, ct);

    public async Task<bool> TrySendAsync(ushort opcode, byte[] body, CancellationToken ct)
    {
        if (_system is not { } system) return false;
        try
        {
            await system.SendUnencryptedFrameAsync(opcode, body, ct).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is ObjectDisposedException or IOException or System.Net.Sockets.SocketException)
        {
            _log($"[system-link] 0x{opcode:X4} not delivered: {ex.Message}");
            return false;
        }
    }

    private async Task HandleAsync(ushort zone, OfficerOrder order, CancellationToken ct)
    {
        switch (order.OrderType)
        {
            case SystemLinkFrames.OrderCancel:
                var cancelled = SystemLinkFrames.ReadCancelledOrderId(order.Data);
                if (_mirrors.TryGetValue(order.CharacterId, out var current) && current.OrderId == cancelled)
                {
                    _mirrors.TryRemove(order.CharacterId, out _);
                }
                if (OrderReceived is { } cancelReceived) await cancelReceived(order, ct).ConfigureAwait(false);
                break;
            case SystemLinkFrames.OrderNotifyPartyPlayer:
                var mirror = SystemLinkFrames.DecodeMirror(order.Data);
                _mirrors[mirror.RecipientCharacterId] = (order.OrderId, mirror);
                break;
            case SystemLinkFrames.OrderUpdateQuest:
            case SystemLinkFrames.OrderCancelQuest:
            case SystemLinkFrames.OrderShareExp:
            case SystemLinkFrames.OrderRecoverQuest:
            case SystemLinkFrames.OrderTypeBonus:
            case SystemLinkFrames.OrderGuildName:
            case PartyBoardFrames.OrderHeadNotice:
            case AngelFrames.OrderNewbiePosition:
            case AngelFrames.OrderAngelMove:
            case AngelFrames.OrderAngelReward:
            case GuildGameOrders.OrderEnterMatch:
            case GuildGameOrders.OrderResultNotice:
            case GuildGameOrders.OrderLobbyNotice:
            case GuildGameOrders.OrderStartNotice:
            case CardBattleFrames.OrderStatusUpdate:
            case WeddingHallFrames.OrderWarpMaster:
            case WeddingHallFrames.OrderJoinMember:
            case WeddingHallFrames.OrderWarpMember:
            case WeddingHallFrames.OrderDress:
            case GameMasterFrames.OrderSetGmAttribute:
            case GameMasterFrames.OrderTeleportToZone:
            case GameMasterFrames.OrderTeleportToChr:
            case GameMasterFrames.OrderCharInfo:
            case InstantChatFrames.OrderMyNotice:
            case InstantChatFrames.OrderReportLevel:
            case InstantChatFrames.OrderNoticeState:
            case FriendFrames.OrderObservePosition:
            case FriendFrames.OrderReturnPosition:
                if (OrderReceived is { } received) await received(order, ct).ConfigureAwait(false);
                break;
            case GameMasterFrames.OrderNpcOnOff:
            case GameMasterFrames.OrderNpcRefresh:
                if (ZonedOrderReceived is { } zonedReceived) await zonedReceived(zone, order, ct).ConfigureAwait(false);
                break;
            case var gameMasterOrder when GameMasterFrames.IsGameServerOrder(gameMasterOrder):
                if (OrderReceived is { } gameMasterReceived) await gameMasterReceived(order, ct).ConfigureAwait(false);
                break;
            default:
                _log($"[system-link] order type 0x{order.OrderType:X2} for 0x{order.CharacterId:X} not handled");
                break;
        }
    }
}
