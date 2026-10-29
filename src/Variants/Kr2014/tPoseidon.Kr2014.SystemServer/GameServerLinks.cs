using System.Collections.Concurrent;
using tPoseidon.Core.Net.S2;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Kr2014.SystemServer;

public sealed class GameServerLinks
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private readonly ConcurrentDictionary<ulong, (S2SessionContext Link, ushort Zone)> _whereabouts = new();
    private readonly ConcurrentDictionary<ulong, (ushort From, ushort To)> _transit = new();
    private readonly ConcurrentDictionary<(ulong CharacterId, ushort OrderType), (ushort AgencyId, ushort OrderId)> _outstanding = new();
    private readonly ConcurrentDictionary<S2SessionContext, byte> _links = new();
    private readonly string _keyTablePath;
    private readonly Action<string> _log;
    private int _lastOrderId;

    public GameServerLinks(string keyTablePath, Action<string> log)
    {
        _keyTablePath = keyTablePath ?? throw new ArgumentNullException(nameof(keyTablePath));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public Func<ulong, ushort, CancellationToken, Task>? OnEnterZone { get; set; }

    public Func<ulong, CancellationToken, Task>? OnLeaveZone { get; set; }

    public Action<S2SessionContext>? OnConnected { get; set; }

    public Func<S2SessionContext, CancellationToken, Task>? OnLinked { get; set; }

    public Func<PartyQuestBegin, bool>? OnBeginPartyQuest { get; set; }

    public Func<ulong, ushort, CancellationToken, Task>? OnPartyQuestKill { get; set; }

    public Action<ulong, uint>? OnCancelPartyQuest { get; set; }

    public Func<ulong, bool, CancellationToken, Task>? OnDeadChanged { get; set; }

    public Func<ulong, uint, CancellationToken, Task>? OnLevelChanged { get; set; }

    public Func<ulong, byte, byte, CancellationToken, Task>? OnKindChanged { get; set; }

    public Func<ulong, string, string, CancellationToken, Task>? OnRenamed { get; set; }

    public Func<ulong, ushort, string, CancellationToken, Task>? OnMegaphone { get; set; }

    public System.Text.Encoding Text { get; init; } = System.Text.Encoding.UTF8;

    public Func<ushort, ReadOnlyMemory<byte>, CancellationToken, Task>? OnCoupleMessage { get; set; }

    public Func<OfficerOrder, CancellationToken, Task>? OnReport { get; set; }

    public Func<ReadOnlyMemory<byte>, CancellationToken, Task>? OnGuildMatchResult { get; set; }

    public Func<ReadOnlyMemory<byte>, CancellationToken, Task>? OnGuildRoomInvalid { get; set; }

    public Func<S2SessionContext, ReadOnlyMemory<byte>, CancellationToken, Task>? OnGameMasterChat { get; set; }

    public ushort? ZoneOf(ulong characterId) => _whereabouts.TryGetValue(characterId, out var where) ? where.Zone : null;

    public IReadOnlyList<ushort> Zones() => _whereabouts.Values.Select(where => where.Zone).ToArray();

    public FriendZoneInfo? ZoneInfoOf(ulong characterId)
    {
        if (_transit.TryGetValue(characterId, out var moving)) return FriendZoneInfo.Moving(moving.From, moving.To);
        return _whereabouts.TryGetValue(characterId, out var where) ? FriendZoneInfo.Settled(where.Zone) : null;
    }

    public async Task RunAsync(string host, int port, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            S2SessionContext? link = null;
            try
            {
                await S2Client.RunAsync(host, port, _keyTablePath, "[sserver1]", async (ctx, innerCt) =>
                {
                    link = ctx;
                    Register(ctx);
                    RegisterPartyQuests(ctx);
                    RegisterCouple(ctx);
                    RegisterReports(ctx);
                    RegisterNames(ctx);
                    RegisterGuildGames(ctx);
                    RegisterGameMasters(ctx);
                    OnConnected?.Invoke(ctx);
                    await ctx.SendFrameAsync(SystemLinkFrames.SystemHello, Array.Empty<byte>(), (byte)Random.Shared.Next(1, 256), innerCt).ConfigureAwait(false);
                    _links[ctx] = 0;
                    _log($"[game-link] connected to GameServer {host}:{port}");
                    if (OnLinked is { } linked) await linked(ctx, innerCt).ConfigureAwait(false);
                }, ct).ConfigureAwait(false);
                _log($"[game-link] GameServer {host}:{port} closed the link");
            }
            catch (Exception ex) when (ex is System.Net.Sockets.SocketException or IOException)
            {
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            finally
            {
                if (link is not null)
                {
                    _links.TryRemove(link, out _);
                    Forget(link);
                }
            }
            try
            {
                await Task.Delay(RetryDelay, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    public async Task<bool> SendAsync(PartyOrder order, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(order);
        if (!_whereabouts.TryGetValue(order.CharacterId, out var where)) return false;
        var key = (order.CharacterId, order.OrderType);
        if (_outstanding.TryRemove(key, out var previous))
        {
            await SendOrderAsync(where, new OfficerOrder(previous.AgencyId, NextOrderId(), SystemLinkFrames.OrderCancel, order.CharacterId, SystemLinkFrames.CancelData(previous.OrderId)), ct).ConfigureAwait(false);
        }
        if (order.Data is null) return true;
        var orderId = NextOrderId();
        _outstanding[key] = (order.AgencyId, orderId);
        await SendOrderAsync(where, new OfficerOrder(order.AgencyId, orderId, order.OrderType, order.CharacterId, order.Data), ct).ConfigureAwait(false);
        return true;
    }

    public async Task<ushort?> SendReportedAsync(ulong characterId, ushort orderType, byte[] data, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (!_whereabouts.TryGetValue(characterId, out var where)) return null;
        var orderId = NextOrderId();
        await SendOrderAsync(where, new OfficerOrder(0, orderId, orderType, characterId, data), ct).ConfigureAwait(false);
        return orderId;
    }

    public async Task SendOnceAsync(ulong characterId, ushort orderType, byte[] data, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (!_whereabouts.TryGetValue(characterId, out var where)) return;
        await SendOrderAsync(where, new OfficerOrder(0, NextOrderId(), orderType, characterId, data), ct).ConfigureAwait(false);
    }

    public async Task SendToZoneAsync(ushort zone, ushort orderType, byte[] data, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(data);
        var links = _whereabouts.Values.Where(where => where.Zone == zone).Select(where => where.Link).Distinct().ToArray();
        foreach (var link in links)
        {
            await SendOrderAsync((link, zone), new OfficerOrder(0, NextOrderId(), orderType, 0, data), ct).ConfigureAwait(false);
        }
    }

    private void RegisterGuildGames(S2SessionContext link)
    {
        link.Dispatcher.Register(GuildGameOrders.MatchResult, (_, payload, ct) =>
            OnGuildMatchResult is { } handle ? handle(payload, ct) : Task.CompletedTask);
        link.Dispatcher.Register(GuildGameOrders.RoomInvalid, (_, payload, ct) =>
            OnGuildRoomInvalid is { } handle ? handle(payload, ct) : Task.CompletedTask);
    }

    private void RegisterGameMasters(S2SessionContext link)
    {
        link.Dispatcher.Register(GameMasterFrames.LinkChatCommand, (_, payload, ct) =>
            OnGameMasterChat is { } handle ? handle(link, payload, ct) : Task.CompletedTask);
        link.Dispatcher.Register(GameMasterFrames.LinkTeleportRelay, (_, payload, ct) =>
        {
            if (payload.Length < GameMasterFrames.ChrToChrLength || !GameMasterFrames.TryReadChrToChr(payload.Span, out var who, out ulong _))
            {
                _log($"[game-link] teleport relay dropped: {payload.Length} B");
                return Task.CompletedTask;
            }
            return SendAsync(new PartyOrder(who, GameMasterFrames.OrderTeleportToChr, 0, payload.ToArray()), ct);
        });
    }

    private void Register(S2SessionContext link)
    {
        link.Dispatcher.Register(SystemLinkFrames.EnterZone, async (_, payload, ct) =>
        {
            var (characterId, zone) = SystemLinkFrames.ReadCharacterAndZone(payload.Span);
            _whereabouts[characterId] = (link, zone);
            _transit.TryRemove(characterId, out var transitEnded);
            ForgetOrders(characterId);
            if (OnEnterZone is not null) await OnEnterZone(characterId, zone, ct).ConfigureAwait(false);
        });
        link.Dispatcher.Register(SystemLinkFrames.LeaveZone, async (_, payload, ct) =>
        {
            var (characterId, nextZone) = SystemLinkFrames.ReadCharacterAndZone(payload.Span);
            if (_whereabouts.TryGetValue(characterId, out var current)) _transit[characterId] = (current.Zone, nextZone);
            if (OnLeaveZone is not null) await OnLeaveZone(characterId, ct).ConfigureAwait(false);
        });
        link.Dispatcher.Register(SystemLinkFrames.LoggedOut, async (opcode, payload, ct) =>
        {
            var characterId = SystemLinkFrames.ReadCharacter(payload.Span);
            if (_whereabouts.TryGetValue(characterId, out var where) && ReferenceEquals(where.Link, link))
            {
                _whereabouts.TryRemove(characterId, out _);
                _transit.TryRemove(characterId, out var transitEnded);
                ForgetOrders(characterId);
            }
            if (OnLeaveZone is not null) await OnLeaveZone(characterId, ct).ConfigureAwait(false);
        });
    }

    public async Task BroadcastAsync(ushort opcode, byte[] body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        foreach (var link in _links.Keys.ToArray()) await SendToLinkAsync(link, opcode, body, ct).ConfigureAwait(false);
    }

    public async Task SendToLinkAsync(S2SessionContext link, ushort opcode, byte[] body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(body);
        try
        {
            await link.SendFrameAsync(opcode, body, (byte)Random.Shared.Next(1, 256), ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ObjectDisposedException or IOException or System.Net.Sockets.SocketException)
        {
            _log($"[game-link] message 0x{opcode:X4} to {link.Remote} not delivered: {ex.Message}");
        }
    }

    public async Task SendDirectAsync(ulong characterId, ushort opcode, byte[] body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (!_whereabouts.TryGetValue(characterId, out var where))
        {
            _log($"[game-link] message 0x{opcode:X4} for 0x{characterId:X} skipped: the character is not on a game server");
            return;
        }
        try
        {
            await where.Link.SendFrameAsync(opcode, body, (byte)Random.Shared.Next(1, 256), ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ObjectDisposedException or IOException or System.Net.Sockets.SocketException)
        {
            _log($"[game-link] message 0x{opcode:X4} for 0x{characterId:X} not delivered: {ex.Message}");
        }
    }

    private void RegisterReports(S2SessionContext link)
    {
        link.Dispatcher.Register(SystemLinkFrames.Report, (_, payload, ct) =>
        {
            var (_, report) = SystemLinkFrames.DecodeOrder(payload.Span);
            return OnReport is { } handle ? handle(report, ct) : Task.CompletedTask;
        });
    }

    private void RegisterNames(S2SessionContext link)
    {
        link.Dispatcher.Register(SystemLinkFrames.CharacterRenamed, async (opcode, payload, ct) =>
        {
            var (characterId, oldName, newName) = SystemLinkFrames.ReadCharacterRenamed(payload.Span, Text);
            _log($"[game-link] 0x{characterId:X} renamed '{oldName}' to '{newName}'");
            if (OnRenamed is not null) await OnRenamed(characterId, oldName, newName, ct).ConfigureAwait(false);
        });
        link.Dispatcher.Register(SystemLinkFrames.Megaphone, async (opcode, payload, ct) =>
        {
            var (characterId, style, message) = SystemLinkFrames.ReadMegaphone(payload.Span, Text);
            if (OnMegaphone is not null) await OnMegaphone(characterId, style, message, ct).ConfigureAwait(false);
        });
    }

    private void RegisterCouple(S2SessionContext link)
    {
        foreach (var opcode in new[] { CoupleSocialFrames.LinkMakeCouple, CoupleSocialFrames.LinkCoupleInfoRequest, CoupleSocialFrames.LinkMarriageOption, WeddingHallFrames.LinkEndWedding })
        {
            link.Dispatcher.Register(opcode, (received, payload, ct) =>
                OnCoupleMessage is { } handle ? handle((ushort)received, payload, ct) : Task.CompletedTask);
        }
    }

    private void RegisterPartyQuests(S2SessionContext link)
    {
        link.Dispatcher.Register(SystemLinkFrames.BeginPartyQuest, async (opcode, payload, ct) =>
        {
            var begin = SystemLinkFrames.DecodeBeginPartyQuest(payload.Span);
            var begun = OnBeginPartyQuest?.Invoke(begin) ?? false;
            _log($"[game-link] party {begin.PartyId} quest {begin.QuestId} {(begun ? "begun" : "refused")} for 0x{begin.CharacterId:X}");
            await link.SendFrameAsync(
                begun ? SystemLinkFrames.PartyQuestBegun : SystemLinkFrames.PartyQuestNotBegun,
                SystemLinkFrames.ZoneAndCharacter(begin.Zone, begin.CharacterId),
                (byte)Random.Shared.Next(1, 256), ct).ConfigureAwait(false);
        });
        link.Dispatcher.Register(SystemLinkFrames.PartyQuestKill, async (opcode, payload, ct) =>
        {
            var (characterId, monsterId) = SystemLinkFrames.ReadKillReport(payload.Span);
            if (OnPartyQuestKill is not null) await OnPartyQuestKill(characterId, monsterId, ct).ConfigureAwait(false);
        });
        link.Dispatcher.Register(SystemLinkFrames.CharacterTypeChanged, async (opcode, payload, ct) =>
        {
            var (characterId, type, job) = SystemLinkFrames.ReadCharacterTypeChanged(payload.Span);
            if (OnKindChanged is not null) await OnKindChanged(characterId, type, job, ct).ConfigureAwait(false);
        });
        link.Dispatcher.Register(SystemLinkFrames.LevelChanged, async (opcode, payload, ct) =>
        {
            if (payload.Length < 12) return;
            var (characterId, level) = SystemLinkFrames.ReadCharacterAndLevel(payload.Span);
            if (level > 0 && OnLevelChanged is not null) await OnLevelChanged(characterId, level, ct).ConfigureAwait(false);
        });
        link.Dispatcher.Register(SystemLinkFrames.Died, async (opcode, payload, ct) =>
        {
            if (OnDeadChanged is not null) await OnDeadChanged(SystemLinkFrames.ReadCharacter(payload.Span), true, ct).ConfigureAwait(false);
        });
        link.Dispatcher.Register(SystemLinkFrames.Revived, async (opcode, payload, ct) =>
        {
            if (OnDeadChanged is not null) await OnDeadChanged(SystemLinkFrames.ReadCharacter(payload.Span), false, ct).ConfigureAwait(false);
        });
        link.Dispatcher.Register(SystemLinkFrames.CancelPartyQuest, (opcode, payload, ct) =>
        {
            var (characterId, questId) = SystemLinkFrames.ReadCharacterAndQuest(payload.Span);
            OnCancelPartyQuest?.Invoke(characterId, questId);
            return Task.CompletedTask;
        });
    }

    private void Forget(S2SessionContext link)
    {
        foreach (var (characterId, where) in _whereabouts)
        {
            if (!ReferenceEquals(where.Link, link)) continue;
            _whereabouts.TryRemove(characterId, out _);
            _transit.TryRemove(characterId, out var transitEnded);
            ForgetOrders(characterId);
        }
    }

    private void ForgetOrders(ulong characterId)
    {
        foreach (var key in _outstanding.Keys.Where(k => k.CharacterId == characterId).ToArray()) _outstanding.TryRemove(key, out _);
    }

    private async Task SendOrderAsync((S2SessionContext Link, ushort Zone) where, OfficerOrder order, CancellationToken ct)
    {
        try
        {
            await where.Link.SendFrameAsync(SystemLinkFrames.Order, SystemLinkFrames.EncodeOrder(where.Zone, order), (byte)Random.Shared.Next(1, 256), ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ObjectDisposedException or IOException or System.Net.Sockets.SocketException)
        {
            _log($"[game-link] order 0x{order.OrderType:X2} for 0x{order.CharacterId:X} not delivered: {ex.Message}");
        }
    }

    private ushort NextOrderId()
    {
        var next = Interlocked.Increment(ref _lastOrderId) & 0xFFFF;
        return next == 0 ? NextOrderId() : (ushort)next;
    }
}
