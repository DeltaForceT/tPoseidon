using System.Text;
using tPoseidon.Core.Net;
using tPoseidon.Core.Net.Trident;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Kr2014.SystemServer;

public sealed class ChatServer
{
    private delegate InstantChatOutbox? RoomHandler(InstantChatMember member, ReadOnlySpan<byte> body);

    public const ushort LobbyRoomId = InstantChatRooms.LobbyRoomId;

    private readonly SocialRoom _room;
    private readonly InstantChatRooms _instantRooms;
    private readonly ChannelChatRooms _channelRooms;
    private readonly GameServerLinks _games;
    private readonly Encoding _text;
    private readonly Action<string> _log;
    private readonly object _gate = new();
    private ushort _lastClientId;

    public ChatServer(SocialRoom room, InstantChatRooms instantRooms, ChannelChatRooms channelRooms, GameServerLinks games, Encoding text, Action<string> log)
    {
        _room = room ?? throw new ArgumentNullException(nameof(room));
        _instantRooms = instantRooms ?? throw new ArgumentNullException(nameof(instantRooms));
        _channelRooms = channelRooms ?? throw new ArgumentNullException(nameof(channelRooms));
        _games = games ?? throw new ArgumentNullException(nameof(games));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public void Register(OpcodeDispatcher dispatcher, TridentSessionContext ctx)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(ctx);
        dispatcher.Register((Core.Common.Opcode)WhisperFrames.EnterLobby, (_, _, ct) => EnterLobbyAsync(ctx, ct));
        dispatcher.Register((Core.Common.Opcode)WhisperFrames.Whisper, (_, payload, ct) => WhisperAsync(ctx, payload, ct));
        Room(dispatcher, ctx, InstantChatFrames.Create, (member, p) =>
        {
            if (ChannelChatFrames.TryReadKind(p, out var kind) && ChannelChatFrames.IsRoomKind(kind))
            {
                return ChannelChatFrames.TryReadCreate(p, _text, out var channelCreate) ? _channelRooms.Create(member, channelCreate) : null;
            }

            return InstantChatFrames.TryReadCreate(p, _text, out var create) ? _instantRooms.Create(member, create) : null;
        });
        Room(dispatcher, ctx, InstantChatFrames.Enter, (member, p) =>
        {
            if (ChannelChatFrames.TryReadKind(p, out var kind) && ChannelChatFrames.IsRoomKind(kind))
            {
                return ChannelChatFrames.TryReadEnter(p, _text, out var channelEnter) ? _channelRooms.Enter(member, channelEnter) : null;
            }

            return InstantChatFrames.TryReadEnter(p, _text, out var enter) ? _instantRooms.Enter(member, enter) : null;
        });
        Room(dispatcher, ctx, InstantChatFrames.Leave, (member, p) =>
            InstantChatFrames.TryReadRoom(p, out var roomId)
                ? InChannel(member, roomId) ? _channelRooms.Leave(member.CharacterId, roomId) : _instantRooms.Leave(member.CharacterId, roomId)
                : null);
        Room(dispatcher, ctx, InstantChatFrames.Chat, (member, p) =>
            InstantChatFrames.TryReadLine(p, _text, out var line)
                ? InChannel(member, line.RoomId) ? _channelRooms.Say(member.CharacterId, line) : _instantRooms.Say(member.CharacterId, line)
                : null);
        Room(dispatcher, ctx, InstantChatFrames.Close, (member, p) =>
            InstantChatFrames.TryReadRoom(p, out var roomId)
                ? InChannel(member, roomId) ? _channelRooms.Close(member.CharacterId, roomId) : _instantRooms.Close(member.CharacterId, roomId)
                : null);
        Room(dispatcher, ctx, InstantChatFrames.SetTitle, (member, p) =>
            InstantChatFrames.TryReadRoomAndText(p, _text, out var roomId, out var title)
                ? InChannel(member, roomId) ? _channelRooms.SetTitle(member.CharacterId, roomId, title) : _instantRooms.SetTitle(member.CharacterId, roomId, title)
                : null);
        Room(dispatcher, ctx, InstantChatFrames.SetMaxMembers, (member, p) =>
            InstantChatFrames.TryReadRoomAndValue(p, out var roomId, out var maxMembers)
                ? InChannel(member, roomId) ? _channelRooms.SetMaxMembers(member.CharacterId, roomId, maxMembers) : _instantRooms.SetMaxMembers(member.CharacterId, roomId, maxMembers)
                : null);
        Room(dispatcher, ctx, InstantChatFrames.SetPassword, (member, p) =>
            InstantChatFrames.TryReadRoomAndText(p, _text, out var roomId, out var password)
                ? InChannel(member, roomId) ? _channelRooms.SetPassword(member.CharacterId, roomId, password) : _instantRooms.SetPassword(member.CharacterId, roomId, password)
                : null);
        Room(dispatcher, ctx, InstantChatFrames.Kick, (member, p) =>
            InstantChatFrames.TryReadRoomAndValue(p, out var roomId, out var target)
                ? InChannel(member, roomId) ? _channelRooms.Kick(member.CharacterId, roomId, target) : _instantRooms.Kick(member.CharacterId, roomId, target)
                : null);
        Room(dispatcher, ctx, InstantChatFrames.RoomInfoRequest, (member, p) =>
            InstantChatFrames.TryReadRoom(p, out var roomId)
                ? _channelRooms.HasRoom(roomId) ? _channelRooms.Info(member.CharacterId, roomId) : _instantRooms.Info(member.CharacterId, roomId)
                : null);
        Room(dispatcher, ctx, ChannelChatFrames.List, (member, p) =>
            ChannelChatFrames.TryReadList(p, out var list) ? _channelRooms.List(member.CharacterId, list) : null);
        Room(dispatcher, ctx, ChannelChatFrames.Delegate, (member, p) =>
            InstantChatFrames.TryReadRoomAndValue(p, out var roomId, out var target) ? _channelRooms.Handover(member.CharacterId, roomId, target) : null);
        Room(dispatcher, ctx, ChannelChatFrames.Block, (member, p) =>
            ChannelChatFrames.TryReadBlock(p, _text, out var block) ? _channelRooms.Block(member.CharacterId, block) : null);
        Room(dispatcher, ctx, ChannelChatFrames.BlockList, (member, p) =>
            InstantChatFrames.TryReadRoom(p, out var roomId) ? _channelRooms.BlockList(member.CharacterId, roomId) : null);
        Room(dispatcher, ctx, ChannelChatFrames.Message, (member, p) =>
            ChannelChatFrames.TryReadMessage(p, _text, out var message) ? _channelRooms.Message(member.CharacterId, message) : null);
        Room(dispatcher, ctx, ChannelChatFrames.Search, (_, _) => new InstantChatOutbox());
        Room(dispatcher, ctx, ChannelChatFrames.Reserved, (_, _) => new InstantChatOutbox());
    }

    public Action<ulong, ulong>? Whispered { get; set; }

    public Task ClosedAsync(SocialMember member, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(member);
        var characterId = member.Character.CharacterId;
        var outbox = _instantRooms.Disconnected(characterId);
        Merge(outbox, _channelRooms.Released(characterId, clientAlive: false));
        return DeliverAsync("disconnect", outbox, ct);
    }

    public Task ReportAsync(OfficerOrder report, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (report.OrderType != InstantChatFrames.ReportLevel || !InstantChatFrames.TryReadLevelReport(report.Data, out var level))
        {
            return Task.CompletedTask;
        }

        var outbox = _instantRooms.LevelReported(report.CharacterId, level);
        Merge(outbox, _channelRooms.LevelReported(report.CharacterId, level));
        return DeliverAsync($"level {level} of 0x{report.CharacterId:X}", outbox, ct);
    }

    public bool RemoveRoomOf(ulong characterId)
    {
        if (_instantRooms.RemoveByGameMaster(characterId) is not { } outbox) return false;
        _ = DeliverLoggedAsync("gm remove", outbox);
        return true;
    }

    private void Room(OpcodeDispatcher dispatcher, TridentSessionContext ctx, ushort opcode,
        RoomHandler handler)
    {
        dispatcher.Register((Core.Common.Opcode)opcode, async (_, payload, ct) =>
        {
            if (_room.Find(ctx) is not { ChatClientId: { } connectionId } member) return;
            var outbox = handler(MemberOf(member, connectionId), payload.Span);
            if (outbox is null)
            {
                _log($"[chat] 0x{opcode:X4} from '{member.Character.Name}': {payload.Length} B is not a valid request");
                return;
            }
            await DeliverAsync($"0x{opcode:X4}", outbox, ct).ConfigureAwait(false);
        });
    }

    private bool InChannel(InstantChatMember member, ushort roomId) => _channelRooms.RoomOf(member.CharacterId) == roomId;

    private static void Merge(InstantChatOutbox target, InstantChatOutbox extra)
    {
        target.Messages.AddRange(extra.Messages);
        target.Orders.AddRange(extra.Orders);
        target.OnceOrders.AddRange(extra.OnceOrders);
        target.Notes.AddRange(extra.Notes);
    }

    private static InstantChatMember MemberOf(SocialMember member, ushort connectionId) =>
        new(member.Character.CharacterId, connectionId, member.Character.Name, member.CharacterType);

    private async Task DeliverLoggedAsync(string what, InstantChatOutbox outbox)
    {
        try
        {
            await DeliverAsync(what, outbox, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ObjectDisposedException or IOException or System.Net.Sockets.SocketException)
        {
            _log($"[chat] {what}: delivery stopped: {ex.Message}");
        }
    }

    private async Task DeliverAsync(string what, InstantChatOutbox outbox, CancellationToken ct)
    {
        foreach (var note in outbox.Notes) _log($"[chat] {what}: {note}");
        foreach (var message in outbox.Messages)
        {
            if (_room.Find(message.To) is { } recipient) await NotifyAsync(recipient, message.Opcode, message.Payload, ct).ConfigureAwait(false);
        }
        foreach (var order in outbox.Orders) await _games.SendAsync(order, ct).ConfigureAwait(false);
        foreach (var order in outbox.OnceOrders) await _games.SendOnceAsync(order.CharacterId, order.OrderType, order.Data!, ct).ConfigureAwait(false);
    }

    private async Task EnterLobbyAsync(TridentSessionContext ctx, CancellationToken ct)
    {
        if (_room.Find(ctx) is not { } member) return;
        await DeliverAsync("lobby re-entry", _channelRooms.Released(member.Character.CharacterId, clientAlive: true), ct).ConfigureAwait(false);
        ushort clientId;
        lock (_gate)
        {
            var used = _room.Members.Where(m => !ReferenceEquals(m, member)).Select(m => m.ChatClientId).OfType<ushort>().ToHashSet();
            clientId = _lastClientId;
            do clientId++;
            while (used.Contains(clientId));
            _lastClientId = clientId;
            member.ChatClientId = clientId;
        }
        await SendAsync(ctx, WhisperFrames.LobbyEntered, WhisperFrames.Entered(clientId, LobbyRoomId), ct).ConfigureAwait(false);
        _log($"[chat] 0x{member.Character.CharacterId:X} '{member.Character.Name}' entered the lobby as client {clientId}");
    }

    private async Task WhisperAsync(TridentSessionContext ctx, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (_room.Find(ctx) is not { ChatClientId: { } senderId } sender) return;
        if (!WhisperFrames.TryRead(payload.Span, _text, out var request)) return;
        if (request.RoomId != LobbyRoomId && _instantRooms.RoomOf(sender.Character.CharacterId) != request.RoomId && _channelRooms.RoomOf(sender.Character.CharacterId) != request.RoomId) return;
        var roomId = request.RoomId;
        if (request.ByName)
        {
            var target = TargetByName(roomId, request.TargetName);
            if (target is null)
            {
                await SendAsync(ctx, WhisperFrames.WhisperTargetMissing, WhisperFrames.ToSenderByName(roomId, request.TargetName, _text), ct).ConfigureAwait(false);
                return;
            }
            await SendAsync(ctx, WhisperFrames.WhisperSent, WhisperFrames.ToSenderByName(roomId, request.TargetName, _text), ct).ConfigureAwait(false);
            await NotifyAsync(target, WhisperFrames.WhisperReceived, WhisperFrames.ToTargetByName(roomId, sender.Character.Name, request.Text, _text), ct).ConfigureAwait(false);
            _log($"[chat] '{sender.Character.Name}' whispers to '{target.Character.Name}' (room {roomId})");
            Whispered?.Invoke(sender.Character.CharacterId, target.Character.CharacterId);
            return;
        }
        var byId = TargetById(roomId, request.TargetClientId);
        if (byId is null)
        {
            await SendAsync(ctx, WhisperFrames.WhisperTargetMissing, WhisperFrames.ToSenderById(roomId, request.TargetClientId), ct).ConfigureAwait(false);
            return;
        }
        await NotifyAsync(byId, WhisperFrames.WhisperReceived, WhisperFrames.ToTargetById(roomId, senderId, request.Text, _text), ct).ConfigureAwait(false);
        await SendAsync(ctx, WhisperFrames.WhisperSent, WhisperFrames.ToSenderById(roomId, request.TargetClientId), ct).ConfigureAwait(false);
        Whispered?.Invoke(sender.Character.CharacterId, byId.Character.CharacterId);
    }

    private SocialMember? TargetByName(ushort roomId, string name)
    {
        if (roomId != LobbyRoomId)
        {
            var member = _channelRooms.MemberByName(roomId, name) ?? _instantRooms.MemberByName(roomId, name);
            return member is null ? null : _room.Find(member.CharacterId);
        }

        return _room.Members.FirstOrDefault(m => m.ChatClientId is not null
            && string.Equals(m.Character.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private SocialMember? TargetById(ushort roomId, ushort connectionId)
    {
        if (roomId != LobbyRoomId)
        {
            var member = _channelRooms.MemberById(roomId, connectionId) ?? _instantRooms.MemberById(roomId, connectionId);
            return member is null ? null : _room.Find(member.CharacterId);
        }

        return _room.Members.FirstOrDefault(m => m.ChatClientId == connectionId);
    }

    private async Task NotifyAsync(SocialMember recipient, ushort opcode, byte[] payload, CancellationToken ct)
    {
        try
        {
            await SendAsync(recipient.Context, opcode, payload, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ObjectDisposedException or IOException or System.Net.Sockets.SocketException)
        {
            _log($"[chat] 0x{opcode:X4} to 0x{recipient.Character.CharacterId:X} not delivered: {ex.Message}");
        }
    }

    private static Task SendAsync(TridentSessionContext ctx, ushort opcode, byte[] payload, CancellationToken ct) =>
        ctx.SendFrameAsync(opcode, payload, (byte)Random.Shared.Next(1, 256), ct);
}
