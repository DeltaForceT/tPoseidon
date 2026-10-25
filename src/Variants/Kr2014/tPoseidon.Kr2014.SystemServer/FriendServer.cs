using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.Net;
using tPoseidon.Core.Net.S2;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Kr2014.SystemServer;

public sealed class FriendServer
{
    private const byte NewFriendGroup = 0;

    private readonly IFriendStore _friends;
    private readonly SocialRoom _room;
    private readonly Encoding _text;
    private readonly int _offlineMessageLimit;
    private readonly Action<string> _log;

    public FriendServer(IFriendStore friends, SocialRoom room, Encoding text, int offlineMessageLimit, Action<string> log)
    {
        _friends = friends ?? throw new ArgumentNullException(nameof(friends));
        _room = room ?? throw new ArgumentNullException(nameof(room));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _offlineMessageLimit = offlineMessageLimit;
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public void Register(OpcodeDispatcher dispatcher, S2SessionContext ctx)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(ctx);
        dispatcher.Register((Core.Common.Opcode)FriendFrames.SessionOpen, (_, _, ct) => LoginAsync(ctx, ct));
        dispatcher.Register((Core.Common.Opcode)FriendFrames.Logout, (_, _, ct) => LogoutAsync(ctx, ct));
        dispatcher.Register((Core.Common.Opcode)FriendFrames.ChangeGroup, (_, payload, ct) => ChangeGroupAsync(ctx, payload, ct));
        dispatcher.Register((Core.Common.Opcode)FriendFrames.RenameGroup, (_, payload, _) => RenameGroup(ctx, payload));
        dispatcher.Register((Core.Common.Opcode)FriendFrames.OfflineMessage, (_, payload, ct) => OfflineMessageAsync(ctx, payload, ct));
        dispatcher.Register((Core.Common.Opcode)FriendFrames.RequestOption, (_, payload, _) => StoreRequestOption(ctx, payload));
        dispatcher.Register((Core.Common.Opcode)FriendFrames.AddRequest, (_, payload, ct) => AddRequestAsync(ctx, payload, ct));
        dispatcher.Register((Core.Common.Opcode)FriendFrames.Accept, (_, payload, ct) => AcceptAsync(ctx, payload, ct));
        dispatcher.Register((Core.Common.Opcode)FriendFrames.Refuse, (_, payload, ct) => RefuseAsync(ctx, payload, ct));
        dispatcher.Register((Core.Common.Opcode)FriendFrames.CancelRequest, (_, payload, ct) => CancelRequestAsync(ctx, payload, ct));
        dispatcher.Register((Core.Common.Opcode)FriendFrames.Delete, (_, payload, ct) => DeleteAsync(ctx, payload, ct));
        dispatcher.Register((Core.Common.Opcode)FriendFrames.DeleteOffline, (_, payload, ct) => DeleteOfflineAsync(ctx, payload, ct));
        dispatcher.Register((Core.Common.Opcode)FriendFrames.RequestSignalOn, (_, payload, ct) => RelayAsync(ctx, payload, FriendFrames.RequestSignalOnRelayed, ct));
        dispatcher.Register((Core.Common.Opcode)FriendFrames.RequestSignalOff, (_, payload, ct) => RelayAsync(ctx, payload, FriendFrames.RequestSignalOffRelayed, ct));
        dispatcher.Register((Core.Common.Opcode)FriendFrames.RequestWaitRelay, (_, payload, ct) => RelayAsync(ctx, payload, FriendFrames.RequestSent, ct));
        dispatcher.Register((Core.Common.Opcode)FriendFrames.MessageBlockRelay, (_, payload, ct) => RelayAsync(ctx, payload, FriendFrames.MessageBlocked, ct));
        dispatcher.Register((Core.Common.Opcode)FriendFrames.RequestShown, (_, payload, ct) => RequestShownAsync(ctx, payload, ct));
        dispatcher.Register((Core.Common.Opcode)FriendFrames.PositionRequest, (_, payload, ct) => PositionAsync(ctx, payload, ct));
        dispatcher.Register((Core.Common.Opcode)FriendFrames.ChangeZoneToRequest, (_, payload, ct) => ChangeZoneToAsync(ctx, payload, ct));
        dispatcher.Register((Core.Common.Opcode)FriendFrames.OnlineMessage, (_, payload, ct) => OnlineMessageAsync(ctx, payload, ct));
    }

    public Task ClosedAsync(SocialMember member, CancellationToken ct) => AnnounceOfflineAsync(member, ct);

    private async Task LoginAsync(S2SessionContext ctx, CancellationToken ct)
    {
        if (_room.Find(ctx) is not { } member) return;
        var owner = member.Character.CharacterId;
        var groups = _friends.GetGroups(owner);
        if (groups.Count == 0)
        {
            _friends.CreateDefaultGroups(owner);
            groups = _friends.GetGroups(owner);
        }
        var friends = _friends.GetFriends(owner).Take(FriendRules.MostFriends)
            .Select(entry => new FriendListed(entry, _room.Find(entry.FriendCharacterId) is not null))
            .ToArray();
        var removedBy = _friends.TakeDeletedNotifications(owner).Take(FriendRules.MostFriends).ToArray();
        var messages = _friends.TakeMessages(owner);
        await SendAsync(ctx, FriendFrames.ListOpcode(friends.Length), FriendFrames.List(removedBy, groups, friends, messages, _text), ct).ConfigureAwait(false);
        member.FriendLoggedIn = true;
        _log($"[friend] 0x{owner:X} '{member.Character.Name}' logged in: {friends.Length} friends ({friends.Count(f => f.Online)} online), {groups.Count} groups, {removedBy.Length} removals, {messages.Count} messages");
        foreach (var friend in friends.Where(f => f.Online))
        {
            if (_room.Find(friend.Entry.FriendCharacterId) is { FriendLoggedIn: true } online)
            {
                await NotifyAsync(online, FriendFrames.FriendOnline, FriendFrames.Character(owner), ct).ConfigureAwait(false);
            }
        }
    }

    private async Task LogoutAsync(S2SessionContext ctx, CancellationToken ct)
    {
        if (_room.Find(ctx) is not { FriendLoggedIn: true } member) return;
        await AnnounceOfflineAsync(member, ct).ConfigureAwait(false);
        member.FriendLoggedIn = false;
        member.FriendRequestPending = false;
    }

    private async Task AnnounceOfflineAsync(SocialMember member, CancellationToken ct)
    {
        if (!member.FriendLoggedIn) return;
        var owner = member.Character.CharacterId;
        foreach (var friend in _friends.GetFriends(owner))
        {
            if (_room.Find(friend.FriendCharacterId) is { FriendLoggedIn: true } online)
            {
                await NotifyAsync(online, FriendFrames.FriendOffline, FriendFrames.Character(owner), ct).ConfigureAwait(false);
            }
        }
    }

    private async Task ChangeGroupAsync(S2SessionContext ctx, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (_room.Find(ctx) is not { FriendLoggedIn: true } member) return;
        var (friendId, groupNum, value) = ReadGroupChange(payload.Span);
        var changed = groupNum < FriendRules.GroupCount && _friends.SetFriendGroup(member.Character.CharacterId, friendId, groupNum);
        await SendAsync(
            ctx,
            changed ? FriendFrames.GroupChanged : FriendFrames.GroupChangeRefused,
            FriendFrames.GroupChange(friendId, groupNum, changed ? value : FriendFrames.GroupChangeRefusedCode),
            ct).ConfigureAwait(false);
    }

    private Task RenameGroup(S2SessionContext ctx, ReadOnlyMemory<byte> payload)
    {
        if (_room.Find(ctx) is not { FriendLoggedIn: true } member) return Task.CompletedTask;
        var reader = new PacketReader(payload.Span);
        var groupNum = reader.ReadUInt32();
        var name = reader.ReadNulTerminated(_text);
        if (groupNum >= FriendRules.GroupCount || _text.GetByteCount(name) > FriendRules.GroupNameLength) return Task.CompletedTask;
        if (!_friends.RenameGroup(member.Character.CharacterId, (byte)groupNum, name))
        {
            _log($"[friend] 0x{member.Character.CharacterId:X} could not rename group {groupNum}");
        }
        return Task.CompletedTask;
    }

    private async Task OfflineMessageAsync(S2SessionContext ctx, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (_room.Find(ctx) is not { FriendLoggedIn: true } member) return;
        var (target, text) = ReadCharacterAndText(payload.Span);
        var content = FriendRules.Truncate(text, FriendRules.MessageContentLength, _text);
        if (_friends.CountMessages(target) >= _offlineMessageLimit)
        {
            await SendAsync(ctx, FriendFrames.OfflineMessagesFull, Array.Empty<byte>(), ct).ConfigureAwait(false);
            return;
        }
        var stored = _friends.SendMessage(target, member.Character.CharacterId, 0, content);
        await SendAsync(ctx, stored ? FriendFrames.OfflineMessageStored : FriendFrames.OfflineMessageFailed, Array.Empty<byte>(), ct).ConfigureAwait(false);
        if (stored) MemoSent?.Invoke(member.Character.CharacterId, target);
    }

    public Action<ulong, ulong>? MemoSent { get; set; }

    public Func<ulong, ushort?>? ZoneOf { get; set; }

    public Func<ulong, bool>? IsGameMaster { get; set; }

    private async Task RelayAsync(S2SessionContext ctx, ReadOnlyMemory<byte> payload, ushort opcode, CancellationToken ct)
    {
        if (_room.Find(ctx) is not { FriendLoggedIn: true } || payload.Length < sizeof(ulong)) return;
        var targetId = new PacketReader(payload.Span).ReadUInt64();
        if (_room.Find(targetId) is { FriendLoggedIn: true } target)
        {
            await NotifyAsync(target, opcode, Array.Empty<byte>(), ct).ConfigureAwait(false);
        }
    }

    private async Task RequestShownAsync(S2SessionContext ctx, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (_room.Find(ctx) is not { FriendLoggedIn: true } member || payload.Length < sizeof(ulong)) return;
        var requesterId = new PacketReader(payload.Span).ReadUInt64();
        member.FriendRequestPending = false;
        if (_room.Find(requesterId) is not { FriendLoggedIn: true } requester) return;
        requester.FriendRequestPending = false;
        await NotifyAsync(requester, FriendFrames.RequestShownTo, FriendFrames.Name(member.Character.Name, _text), ct).ConfigureAwait(false);
    }

    private async Task PositionAsync(S2SessionContext ctx, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (_room.Find(ctx) is not { FriendLoggedIn: true } member || payload.Length < sizeof(ulong)) return;
        var friendId = new PacketReader(payload.Span).ReadUInt64();
        if (!_friends.GetFriends(member.Character.CharacterId).Any(f => f.FriendCharacterId == friendId)) return;
        if (_room.Find(friendId) is not { FriendLoggedIn: true } || ZoneOf?.Invoke(friendId) is not { } zone)
        {
            await SendAsync(ctx, FriendFrames.CounterpartGone, Array.Empty<byte>(), ct).ConfigureAwait(false);
            return;
        }
        await SendAsync(ctx, FriendFrames.PositionZone, FriendFrames.Zone(zone), ct).ConfigureAwait(false);
    }

    private async Task ChangeZoneToAsync(S2SessionContext ctx, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (_room.Find(ctx) is not { FriendLoggedIn: true } member || payload.Length < sizeof(ulong)) return;
        var friendId = new PacketReader(payload.Span).ReadUInt64();
        if (!_friends.GetFriends(member.Character.CharacterId).Any(f => f.FriendCharacterId == friendId)) return;
        if (_room.Find(friendId) is not { FriendLoggedIn: true } || ZoneOf?.Invoke(friendId) is not { } friendZone)
        {
            await SendAsync(ctx, FriendFrames.NotOnline, Array.Empty<byte>(), ct).ConfigureAwait(false);
            return;
        }
        var own = ZoneOf?.Invoke(member.Character.CharacterId);
        await SendAsync(ctx, FriendFrames.ChangeZoneToAnswer, FriendFrames.ZoneMoveAnswer(FriendDecisions.ZoneMove(own, friendZone)), ct).ConfigureAwait(false);
    }

    private Task StoreRequestOption(S2SessionContext ctx, ReadOnlyMemory<byte> payload)
    {
        if (_room.Find(ctx) is not { } member || payload.Length < 2) return Task.CompletedTask;
        member.FriendRequestOption = new PacketReader(payload.Span).ReadUInt16();
        return Task.CompletedTask;
    }

    private async Task AddRequestAsync(S2SessionContext ctx, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (_room.Find(ctx) is not { FriendLoggedIn: true } member) return;
        var name = new PacketReader(payload.Span).ReadNulTerminated(_text);
        var owner = member.Character.CharacterId;
        var found = _friends.FindCharacter(name);
        if (found is null)
        {
            await SendAsync(ctx, FriendFrames.NotFound, Array.Empty<byte>(), ct).ConfigureAwait(false);
            return;
        }
        if (found.FriendCharacterId == owner)
        {
            await SendAsync(ctx, FriendFrames.RequestedSelf, Array.Empty<byte>(), ct).ConfigureAwait(false);
            return;
        }
        if (_friends.GetFriends(owner).Count >= FriendRules.MostFriends)
        {
            await SendAsync(ctx, FriendFrames.OwnListFull, Array.Empty<byte>(), ct).ConfigureAwait(false);
            return;
        }
        var target = _room.Find(found.FriendCharacterId);
        if (target is not null && _friends.GetFriends(target.Character.CharacterId).Count >= FriendRules.MostFriends)
        {
            await SendAsync(ctx, FriendFrames.TargetListFull, Array.Empty<byte>(), ct).ConfigureAwait(false);
            return;
        }
        if (target is null)
        {
            await SendAsync(ctx, FriendFrames.NotOnline, Array.Empty<byte>(), ct).ConfigureAwait(false);
            return;
        }
        if (target.FriendRequestOption == 0)
        {
            await SendAsync(ctx, FriendFrames.RequestsRefused, Array.Empty<byte>(), ct).ConfigureAwait(false);
            return;
        }
        if (member.FriendRequestPending)
        {
            await SendAsync(ctx, FriendFrames.RequestInFlight, Array.Empty<byte>(), ct).ConfigureAwait(false);
            return;
        }
        if (!target.FriendLoggedIn || IsGameMaster?.Invoke(found.FriendCharacterId) == true)
        {
            await SendAsync(ctx, FriendFrames.NotAvailable, FriendFrames.Unavailable(), ct).ConfigureAwait(false);
            return;
        }
        member.FriendRequestPending = true;
        target.FriendRequestPending = true;
        await NotifyAsync(target, FriendFrames.RequestReceived, FriendFrames.CharacterAndName(owner, member.Character.Name, _text), ct).ConfigureAwait(false);
        await SendAsync(ctx, FriendFrames.RequestSent, Array.Empty<byte>(), ct).ConfigureAwait(false);
        _log($"[friend] 0x{owner:X} '{member.Character.Name}' asks 0x{found.FriendCharacterId:X} '{found.Name}' to be friends");
    }

    private async Task AcceptAsync(S2SessionContext ctx, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (_room.Find(ctx) is not { FriendLoggedIn: true } acceptor) return;
        var requesterId = new PacketReader(payload.Span).ReadUInt64();
        if (_room.Find(requesterId) is not { FriendLoggedIn: true } requester) return;
        var acceptorId = acceptor.Character.CharacterId;
        acceptor.FriendRequestPending = false;
        requester.FriendRequestPending = false;
        var existing = FriendDecisions.ClassifyAccept(
            _friends.GetFriends(acceptorId).Any(f => f.FriendCharacterId == requesterId),
            _friends.GetFriends(requesterId).Any(f => f.FriendCharacterId == acceptorId));
        if (existing != AcceptClass.Insert)
        {
            await SendAsync(ctx, FriendFrames.AlreadyFriends, Array.Empty<byte>(), ct).ConfigureAwait(false);
            if (existing == AcceptClass.PairMismatch)
            {
                await NotifyAsync(requester, FriendFrames.AddPairMismatch, Array.Empty<byte>(), ct).ConfigureAwait(false);
            }
            return;
        }
        var result = _friends.AddFriend(acceptorId, requesterId, NewFriendGroup);
        if (result != FriendAddResult.Added)
        {
            _log($"[friend] 0x{acceptorId:X} accepting 0x{requesterId:X} failed: {result}");
            await SendAsync(ctx, FriendFrames.AddFailed, Array.Empty<byte>(), ct).ConfigureAwait(false);
            await NotifyAsync(requester, FriendFrames.AddFailed, Array.Empty<byte>(), ct).ConfigureAwait(false);
            return;
        }
        var requesterType = _friends.FindCharacter(requester.Character.Name)?.CharacterType ?? 0;
        var acceptorType = _friends.FindCharacter(acceptor.Character.Name)?.CharacterType ?? 0;
        await SendAsync(ctx, FriendFrames.AddedForAcceptor,
            FriendFrames.AddedEntry(requesterId, NewFriendGroup, requester.Character.Name, requesterType, forRequester: false, _text), ct).ConfigureAwait(false);
        await NotifyAsync(requester, FriendFrames.AddedForRequester,
            FriendFrames.AddedEntry(acceptorId, NewFriendGroup, acceptor.Character.Name, acceptorType, forRequester: true, _text), ct).ConfigureAwait(false);
        _log($"[friend] 0x{acceptorId:X} and 0x{requesterId:X} are now friends");
    }

    private async Task RefuseAsync(S2SessionContext ctx, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (_room.Find(ctx) is not { FriendLoggedIn: true } refuser) return;
        var requesterId = new PacketReader(payload.Span).ReadUInt64();
        refuser.FriendRequestPending = false;
        var requester = _room.Find(requesterId);
        if (requester is null)
        {
            await SendAsync(ctx, FriendFrames.CounterpartGone, Array.Empty<byte>(), ct).ConfigureAwait(false);
            return;
        }
        requester.FriendRequestPending = false;
        await NotifyAsync(requester, FriendFrames.RequestRefused, FriendFrames.Name(refuser.Character.Name, _text), ct).ConfigureAwait(false);
        await SendAsync(ctx, FriendFrames.RefusalSent, Array.Empty<byte>(), ct).ConfigureAwait(false);
    }

    private async Task CancelRequestAsync(S2SessionContext ctx, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (_room.Find(ctx) is not { FriendLoggedIn: true } member) return;
        var name = new PacketReader(payload.Span).ReadNulTerminated(_text);
        member.FriendRequestPending = false;
        var target = _room.FindNamed(name);
        if (target is not null) target.FriendRequestPending = false;
        if (target is null)
        {
            await SendAsync(ctx, FriendFrames.CounterpartGone, Array.Empty<byte>(), ct).ConfigureAwait(false);
            return;
        }
        await NotifyAsync(target, FriendFrames.RequestCancelled, FriendFrames.Name(member.Character.Name, _text), ct).ConfigureAwait(false);
    }

    private async Task DeleteAsync(S2SessionContext ctx, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (_room.Find(ctx) is not { FriendLoggedIn: true } member) return;
        var name = new PacketReader(payload.Span).ReadNulTerminated(_text);
        var owner = member.Character.CharacterId;
        var entry = _friends.GetFriends(owner).FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
        if (entry is null) return;
        await SendAsync(ctx, FriendFrames.RemoveStarted, Array.Empty<byte>(), ct).ConfigureAwait(false);
        var friend = _room.Find(entry.FriendCharacterId);
        var result = _friends.RemoveFriend(owner, entry.FriendCharacterId, friend is not null);
        if (result != FriendRemoveResult.Removed)
        {
            _log($"[friend] 0x{owner:X} removing 0x{entry.FriendCharacterId:X} failed: {result}");
            await SendAsync(ctx, FriendFrames.RemoveFailed, Array.Empty<byte>(), ct).ConfigureAwait(false);
            return;
        }
        if (friend is null)
        {
            await SendAsync(ctx, FriendFrames.RemovedWhileOffline, Array.Empty<byte>(), ct).ConfigureAwait(false);
        }
        else
        {
            await NotifyAsync(friend, FriendFrames.RemovedByOwner, FriendFrames.CharacterAndName(owner, member.Character.Name, _text), ct).ConfigureAwait(false);
        }
        await SendAsync(ctx, FriendFrames.RemovedFriend, FriendFrames.CharacterAndName(entry.FriendCharacterId, entry.Name, _text), ct).ConfigureAwait(false);
        _log($"[friend] 0x{owner:X} removed 0x{entry.FriendCharacterId:X} '{entry.Name}' ({(friend is null ? "offline" : "online")})");
    }

    private async Task DeleteOfflineAsync(S2SessionContext ctx, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (_room.Find(ctx) is not { FriendLoggedIn: true } member) return;
        var name = new PacketReader(payload.Span).ReadNulTerminated(_text);
        var owner = member.Character.CharacterId;
        var entry = _friends.GetFriends(owner).FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            await SendAsync(ctx, FriendFrames.DeletedOffline, FriendFrames.CharacterAndName(0, name, _text), ct).ConfigureAwait(false);
            return;
        }
        if (_room.Find(entry.FriendCharacterId) is { FriendLoggedIn: true })
        {
            await SendAsync(ctx, FriendFrames.NotAvailable, FriendFrames.Unavailable(), ct).ConfigureAwait(false);
            return;
        }
        var result = _friends.RemoveFriend(owner, entry.FriendCharacterId, friendOnline: false);
        var (opcode, body) = result switch
        {
            FriendRemoveResult.Removed => (FriendFrames.DeletedOffline, FriendFrames.CharacterAndName(entry.FriendCharacterId, entry.Name, _text)),
            FriendRemoveResult.NotFriends or FriendRemoveResult.ReverseMissing => (FriendFrames.DeletedOffline, FriendFrames.CharacterAndName(0, entry.Name, _text)),
            _ => (FriendFrames.DeleteOfflineFailed, Array.Empty<byte>()),
        };
        await SendAsync(ctx, opcode, body, ct).ConfigureAwait(false);
        _log($"[friend] 0x{owner:X} removed offline friend 0x{entry.FriendCharacterId:X} '{entry.Name}': {result}");
    }

    public async Task TypeChangedAsync(ulong characterId, byte kind, byte job, CancellationToken ct)
    {
        if (_room.Find(characterId) is not { FriendLoggedIn: true }) return;
        await NotifyOnlineFriendsAsync(characterId, FriendFrames.FriendTypeChanged, FriendFrames.TypeChanged(characterId, kind, job), ct).ConfigureAwait(false);
    }

    public async Task RenamedAsync(ulong characterId, string newName, CancellationToken ct)
    {
        if (_room.Find(characterId) is not { FriendLoggedIn: true }) return;
        await NotifyOnlineFriendsAsync(characterId, FriendFrames.FriendRenamed, FriendFrames.CharacterAndName(characterId, newName, _text), ct).ConfigureAwait(false);
    }

    private async Task NotifyOnlineFriendsAsync(ulong owner, ushort opcode, byte[] payload, CancellationToken ct)
    {
        foreach (var friend in _friends.GetFriends(owner))
        {
            if (_room.Find(friend.FriendCharacterId) is { FriendLoggedIn: true } online)
            {
                await NotifyAsync(online, opcode, payload, ct).ConfigureAwait(false);
            }
        }
    }

    private async Task OnlineMessageAsync(S2SessionContext ctx, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (_room.Find(ctx) is not { FriendLoggedIn: true } member) return;
        var (targetId, text) = ReadCharacterAndText(payload.Span);
        var sender = member.Character.CharacterId;
        if (_room.Find(targetId) is not { } target)
        {
            await SendAsync(ctx, FriendFrames.MessageTargetUnknown, Array.Empty<byte>(), ct).ConfigureAwait(false);
            return;
        }
        if (!target.FriendLoggedIn)
        {
            await SendAsync(ctx, FriendFrames.MessageTargetNotReady, Array.Empty<byte>(), ct).ConfigureAwait(false);
            return;
        }
        if (!_friends.GetFriends(sender).Any(f => f.FriendCharacterId == targetId))
        {
            await SendAsync(ctx, FriendFrames.MessageNotMyFriend, Array.Empty<byte>(), ct).ConfigureAwait(false);
            return;
        }
        if (!_friends.GetFriends(targetId).Any(f => f.FriendCharacterId == sender))
        {
            await SendAsync(ctx, FriendFrames.MessageNotTheirFriend, Array.Empty<byte>(), ct).ConfigureAwait(false);
            return;
        }
        await NotifyAsync(target, FriendFrames.MessageDelivered, FriendFrames.MessageFrom(sender, text, _text), ct).ConfigureAwait(false);
        await SendAsync(ctx, FriendFrames.MessageSentTo, FriendFrames.Character(targetId), ct).ConfigureAwait(false);
        MemoSent?.Invoke(sender, targetId);
    }

    private static (ulong FriendId, byte GroupNum, uint Value) ReadGroupChange(ReadOnlySpan<byte> payload)
    {
        var reader = new PacketReader(payload);
        return (reader.ReadUInt64(), reader.ReadByte(), reader.ReadUInt32());
    }

    private (ulong CharacterId, string Text) ReadCharacterAndText(ReadOnlySpan<byte> payload)
    {
        var reader = new PacketReader(payload);
        return (reader.ReadUInt64(), reader.ReadNulTerminated(_text));
    }

    private async Task NotifyAsync(SocialMember recipient, ushort opcode, byte[] payload, CancellationToken ct)
    {
        try
        {
            await SendAsync(recipient.Context, opcode, payload, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ObjectDisposedException or IOException or System.Net.Sockets.SocketException)
        {
            _log($"[friend] 0x{opcode:X4} to 0x{recipient.Character.CharacterId:X} not delivered: {ex.Message}");
        }
    }

    private static Task SendAsync(S2SessionContext ctx, ushort opcode, byte[] payload, CancellationToken ct) =>
        ctx.SendFrameAsync(opcode, payload, (byte)Random.Shared.Next(1, 256), ct);
}
