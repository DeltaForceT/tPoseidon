using System.Text;

namespace tPoseidon.Core.Net.Social;

public sealed class InstantChatOutbox
{
    public List<CoupleMessage> Messages { get; } = new();

    public List<PartyOrder> Orders { get; } = new();

    public List<PartyOrder> OnceOrders { get; } = new();

    public List<string> Notes { get; } = new();
}

public sealed class InstantChatRooms
{
    public const ushort LobbyRoomId = 1;
    public const ushort DefaultMakeLimitLevel = 20;
    public const ushort DefaultJoinLimitLevel = 10;

    private sealed record PendingJoin(InstantChatMember Member, InstantChatCreate? Create, ushort RoomId);

    private sealed class Room
    {
        public required ushort Id { get; init; }
        public required string Title { get; set; }
        public required string Password { get; set; }
        public required ushort MaxMembers { get; set; }
        public List<InstantChatMember> Members { get; } = new();
        public ulong MasterId => Members[0].CharacterId;
    }

    private readonly Encoding _text;
    private readonly ushort _makeLimitLevel;
    private readonly ushort _joinLimitLevel;
    private readonly object _gate = new();
    private readonly Dictionary<ushort, Room> _rooms = new();
    private readonly Dictionary<ulong, ushort> _roomOf = new();
    private readonly Dictionary<ulong, PendingJoin> _pending = new();
    private ushort _lastRoomId = LobbyRoomId;

    public InstantChatRooms(Encoding text, ushort makeLimitLevel, ushort joinLimitLevel)
    {
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _makeLimitLevel = makeLimitLevel;
        _joinLimitLevel = joinLimitLevel;
    }

    public ushort? RoomOf(ulong characterId)
    {
        lock (_gate) return _roomOf.TryGetValue(characterId, out var roomId) ? roomId : null;
    }

    public InstantChatMember? MemberById(ushort roomId, ushort connectionId)
    {
        lock (_gate)
        {
            return _rooms.TryGetValue(roomId, out var room) ? room.Members.FirstOrDefault(member => member.ConnectionId == connectionId) : null;
        }
    }

    public InstantChatMember? MemberByName(ushort roomId, string name)
    {
        lock (_gate)
        {
            return _rooms.TryGetValue(roomId, out var room)
                ? room.Members.FirstOrDefault(member => string.Equals(member.Name, name, StringComparison.OrdinalIgnoreCase))
                : null;
        }
    }

    public InstantChatOutbox Create(InstantChatMember maker, InstantChatCreate create)
    {
        ArgumentNullException.ThrowIfNull(maker);
        var outbox = new InstantChatOutbox();
        lock (_gate)
        {
            if (Busy(maker.CharacterId))
            {
                return Refuse(outbox, maker, InstantChatFrames.CreateRefused, InstantChatFrames.AlreadyInRoom, "create refused: already in a room");
            }

            _pending[maker.CharacterId] = new PendingJoin(maker, create, 0);
            outbox.OnceOrders.Add(LevelQuery(maker.CharacterId));
            outbox.Notes.Add($"'{maker.Name}' asks to make room '{create.Title}'; level asked from the game server");
        }

        return outbox;
    }

    public InstantChatOutbox Enter(InstantChatMember joiner, InstantChatEnter enter)
    {
        ArgumentNullException.ThrowIfNull(joiner);
        var outbox = new InstantChatOutbox();
        lock (_gate)
        {
            if (!_rooms.TryGetValue(enter.RoomId, out var room))
            {
                return Refuse(outbox, joiner, InstantChatFrames.EnterRefused, InstantChatFrames.NoRoom, $"enter {enter.RoomId} refused: no room");
            }

            if (Busy(joiner.CharacterId))
            {
                return Refuse(outbox, joiner, InstantChatFrames.EnterRefused, InstantChatFrames.AlreadyInRoom, $"enter {enter.RoomId} refused: already in a room");
            }

            if (room.Members.Count >= room.MaxMembers)
            {
                return Refuse(outbox, joiner, InstantChatFrames.EnterRefused, InstantChatFrames.RoomFull, $"enter {enter.RoomId} refused: full");
            }

            if (!string.Equals(enter.Password, room.Password, StringComparison.Ordinal))
            {
                return Refuse(outbox, joiner, InstantChatFrames.EnterRefused, InstantChatFrames.WrongPassword, $"enter {enter.RoomId} refused: wrong password");
            }

            _pending[joiner.CharacterId] = new PendingJoin(joiner, null, room.Id);
            outbox.OnceOrders.Add(LevelQuery(joiner.CharacterId));
            outbox.Notes.Add($"'{joiner.Name}' asks to enter room {room.Id}; level asked from the game server");
        }

        return outbox;
    }

    public InstantChatOutbox LevelReported(ulong characterId, ushort level)
    {
        var outbox = new InstantChatOutbox();
        lock (_gate)
        {
            if (!_pending.Remove(characterId, out var pending)) return outbox;
            if (pending.Create is { } create)
            {
                MakeRoom(outbox, pending.Member, level, create);
            }
            else
            {
                JoinRoom(outbox, pending.Member, level, pending.RoomId);
            }
        }

        return outbox;
    }

    private void MakeRoom(InstantChatOutbox outbox, InstantChatMember maker, ushort level, InstantChatCreate create)
    {
        if (level < _makeLimitLevel)
        {
            Refuse(outbox, maker, InstantChatFrames.CreateRefused, InstantChatFrames.LevelTooLow, $"create refused: level {level} < {_makeLimitLevel}");
            return;
        }

        if (NextRoomId() is not { } roomId)
        {
            Refuse(outbox, maker, InstantChatFrames.CreateRefused, InstantChatFrames.RoomNotMade, "create refused: no free room id");
            return;
        }

        var room = new Room { Id = roomId, Title = create.Title, Password = create.Password, MaxMembers = create.MaxMembers };
        room.Members.Add(maker);
        _rooms[roomId] = room;
        _roomOf[maker.CharacterId] = roomId;
        outbox.Orders.Add(SignOrder(room));
        outbox.Orders.Add(StateOrder(maker.CharacterId, master: true));
        outbox.Messages.Add(new CoupleMessage(maker.CharacterId, InstantChatFrames.Created,
            InstantChatFrames.CreatedBody(roomId, room.Title, room.Password.Length > 0, room.MaxMembers, _text)));
        outbox.Notes.Add($"room {roomId} '{room.Title}' made by '{maker.Name}' (max {room.MaxMembers}{(room.Password.Length > 0 ? ", password" : string.Empty)})");
    }

    private void JoinRoom(InstantChatOutbox outbox, InstantChatMember joiner, ushort level, ushort roomId)
    {
        if (!_rooms.TryGetValue(roomId, out var room))
        {
            Refuse(outbox, joiner, InstantChatFrames.EnterRefused, InstantChatFrames.NoRoom, $"enter {roomId} refused: the room closed");
            return;
        }

        if (room.Members.Count >= room.MaxMembers)
        {
            Refuse(outbox, joiner, InstantChatFrames.EnterRefused, InstantChatFrames.RoomFull, $"enter {roomId} refused: full");
            return;
        }

        if (level < _joinLimitLevel)
        {
            Refuse(outbox, joiner, InstantChatFrames.EnterRefused, InstantChatFrames.LevelTooLow, $"enter {roomId} refused: level {level} < {_joinLimitLevel}");
            return;
        }

        Broadcast(outbox, room, InstantChatFrames.MemberIn, InstantChatFrames.MemberInBody(room.Id, joiner, false, _text));
        room.Members.Add(joiner);
        _roomOf[joiner.CharacterId] = room.Id;
        outbox.Orders.Add(StateOrder(joiner.CharacterId, master: false));
        outbox.Messages.Add(new CoupleMessage(joiner.CharacterId, InstantChatFrames.Entered,
            InstantChatFrames.EnteredBody(room.Id, room.Title, room.Password.Length > 0, room.MaxMembers, MembersOf(room), _text)));
        outbox.Notes.Add($"'{joiner.Name}' entered room {room.Id} ({room.Members.Count}/{room.MaxMembers})");
    }

    public InstantChatOutbox Leave(ulong characterId, ushort roomId)
    {
        var outbox = new InstantChatOutbox();
        lock (_gate)
        {
            if (_pending.Remove(characterId)) return outbox;
            if (RoomHolding(characterId, roomId) is { } room) LeaveRoom(outbox, room, characterId, tellLeaver: true);
        }

        return outbox;
    }

    public InstantChatOutbox Disconnected(ulong characterId)
    {
        var outbox = new InstantChatOutbox();
        lock (_gate)
        {
            _pending.Remove(characterId);
            if (_roomOf.TryGetValue(characterId, out var roomId) && _rooms.TryGetValue(roomId, out var room))
            {
                LeaveRoom(outbox, room, characterId, tellLeaver: false);
            }
        }

        return outbox;
    }

    public InstantChatOutbox Say(ulong characterId, InstantChatLine line)
    {
        var outbox = new InstantChatOutbox();
        lock (_gate)
        {
            if (RoomHolding(characterId, line.RoomId) is not { } room) return outbox;
            var sender = room.Members.First(member => member.CharacterId == characterId);
            Broadcast(outbox, room, InstantChatFrames.ChatLine, InstantChatFrames.ChatLineBody(room.Id, sender.ConnectionId, line.Text, line.Style, _text));
        }

        return outbox;
    }

    public InstantChatOutbox Close(ulong characterId, ushort roomId)
    {
        var outbox = new InstantChatOutbox();
        lock (_gate)
        {
            if (RoomHolding(characterId, roomId) is not { } room) return outbox;
            if (room.MasterId != characterId)
            {
                outbox.Messages.Add(new CoupleMessage(characterId, InstantChatFrames.CloseRefused, InstantChatFrames.RoomAndByte(room.Id, InstantChatFrames.NotMaster)));
                return outbox;
            }

            CloseRoom(outbox, room);
        }

        return outbox;
    }

    public InstantChatOutbox SetTitle(ulong characterId, ushort roomId, string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        return MasterChange(characterId, roomId, InstantChatFrames.SetTitleRefused, InstantChatFrames.TitleSet, room =>
        {
            room.Title = InstantChatFrames.Limit(title, InstantChatFrames.TitleBytes, _text);
            return (InstantChatFrames.TitleChanged, InstantChatFrames.RoomAndText(room.Id, room.Title, _text), true);
        });
    }

    public InstantChatOutbox SetMaxMembers(ulong characterId, ushort roomId, ushort maxMembers) =>
        MasterChange(characterId, roomId, InstantChatFrames.SetMaxMembersRefused, InstantChatFrames.MaxMembersSet, room =>
        {
            room.MaxMembers = maxMembers;
            return (InstantChatFrames.MaxMembersChanged, InstantChatFrames.RoomAndByte(room.Id, (byte)maxMembers), false);
        });

    public InstantChatOutbox SetPassword(ulong characterId, ushort roomId, string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        return MasterChange(characterId, roomId, InstantChatFrames.SetPasswordRefused, InstantChatFrames.PasswordSet, room =>
        {
            room.Password = InstantChatFrames.Limit(password, InstantChatFrames.PasswordBytes, _text);
            return (InstantChatFrames.PasswordChanged, InstantChatFrames.RoomAndByte(room.Id, room.Password.Length > 0 ? (byte)1 : (byte)0), true);
        });
    }

    public InstantChatOutbox Kick(ulong characterId, ushort roomId, ushort target)
    {
        var outbox = new InstantChatOutbox();
        lock (_gate)
        {
            if (RoomHolding(characterId, roomId) is not { } room) return outbox;
            var kicker = room.Members.First(member => member.CharacterId == characterId);
            var kicked = room.Members.FirstOrDefault(member => member.ConnectionId == target);
            var code = room.MasterId != characterId ? InstantChatFrames.NotMaster
                : target == kicker.ConnectionId ? InstantChatFrames.SameMember
                : kicked is null ? InstantChatFrames.NotInRoom
                : (byte)0;
            if (code != 0)
            {
                outbox.Messages.Add(new CoupleMessage(characterId, InstantChatFrames.KickRefused, InstantChatFrames.KickRefusedBody(room.Id, target, code)));
                return outbox;
            }

            outbox.Messages.Add(new CoupleMessage(characterId, InstantChatFrames.KickDone, InstantChatFrames.RoomAndMember(room.Id, target)));
            Broadcast(outbox, room, InstantChatFrames.Kicked, InstantChatFrames.RoomAndMember(room.Id, target));
            room.Members.Remove(kicked!);
            _roomOf.Remove(kicked!.CharacterId);
            outbox.Orders.Add(StateOrder(kicked.CharacterId, master: null));
            outbox.Notes.Add($"'{kicked.Name}' kicked from room {room.Id}");
        }

        return outbox;
    }

    public InstantChatOutbox Info(ulong requester, ushort roomId)
    {
        var outbox = new InstantChatOutbox();
        lock (_gate)
        {
            outbox.Messages.Add(_rooms.TryGetValue(roomId, out var room)
                ? new CoupleMessage(requester, InstantChatFrames.RoomInfo, InstantChatFrames.RoomInfoBody(room.Id, room.Title, room.Password.Length > 0, _text))
                : new CoupleMessage(requester, InstantChatFrames.RoomMissing, InstantChatFrames.Room(roomId)));
        }

        return outbox;
    }

    public InstantChatOutbox? RemoveByGameMaster(ulong characterId)
    {
        var outbox = new InstantChatOutbox();
        lock (_gate)
        {
            if (!_roomOf.TryGetValue(characterId, out var roomId) || !_rooms.TryGetValue(roomId, out var room)) return null;
            if (room.MasterId == characterId)
            {
                CloseRoom(outbox, room);
            }
            else
            {
                outbox.Messages.Add(new CoupleMessage(characterId, InstantChatFrames.CloseRefused, InstantChatFrames.RoomAndByte(room.Id, InstantChatFrames.NotMaster)));
            }
        }

        return outbox;
    }

    private InstantChatOutbox MasterChange(ulong characterId, ushort roomId, ushort refused, ushort done,
        Func<Room, (ushort Opcode, byte[] Body, bool RefreshSign)> change)
    {
        var outbox = new InstantChatOutbox();
        lock (_gate)
        {
            if (RoomHolding(characterId, roomId) is not { } room) return outbox;
            if (room.MasterId != characterId)
            {
                outbox.Messages.Add(new CoupleMessage(characterId, refused, InstantChatFrames.RoomAndByte(room.Id, InstantChatFrames.NotMaster)));
                return outbox;
            }

            var (opcode, body, refreshSign) = change(room);
            Broadcast(outbox, room, opcode, body);
            outbox.Messages.Add(new CoupleMessage(characterId, done, InstantChatFrames.Room(room.Id)));
            if (refreshSign) outbox.OnceOrders.Add(SignOrder(room));
        }

        return outbox;
    }

    private bool Busy(ulong characterId) => _roomOf.ContainsKey(characterId) || _pending.ContainsKey(characterId);

    private static PartyOrder LevelQuery(ulong characterId) =>
        new(characterId, InstantChatFrames.OrderReportLevel, 0, Array.Empty<byte>());

    private static PartyOrder StateOrder(ulong characterId, bool? master) =>
        new(characterId, InstantChatFrames.OrderNoticeState, 0, master is { } isMaster ? InstantChatFrames.StateOrderData(isMaster) : null);

    private Room? RoomHolding(ulong characterId, ushort roomId) =>
        _roomOf.TryGetValue(characterId, out var held) && held == roomId && _rooms.TryGetValue(roomId, out var room) ? room : null;

    private void LeaveRoom(InstantChatOutbox outbox, Room room, ulong characterId, bool tellLeaver)
    {
        if (room.MasterId == characterId)
        {
            CloseRoom(outbox, room);
            return;
        }

        var leaver = room.Members.First(member => member.CharacterId == characterId);
        var body = InstantChatFrames.RoomAndMember(room.Id, leaver.ConnectionId);
        if (tellLeaver) outbox.Messages.Add(new CoupleMessage(characterId, InstantChatFrames.MemberOut, body));
        room.Members.Remove(leaver);
        _roomOf.Remove(characterId);
        outbox.Orders.Add(StateOrder(characterId, master: null));
        Broadcast(outbox, room, InstantChatFrames.MemberOut, body);
        outbox.Notes.Add($"'{leaver.Name}' left room {room.Id}");
    }

    private void CloseRoom(InstantChatOutbox outbox, Room room)
    {
        Broadcast(outbox, room, InstantChatFrames.Closed, InstantChatFrames.Room(room.Id));
        outbox.Orders.Add(new PartyOrder(room.MasterId, InstantChatFrames.OrderMyNotice, 0, null));
        foreach (var member in room.Members)
        {
            _roomOf.Remove(member.CharacterId);
            outbox.Orders.Add(StateOrder(member.CharacterId, master: null));
        }
        _rooms.Remove(room.Id);
        outbox.Notes.Add($"room {room.Id} '{room.Title}' closed");
    }

    private static PartyOrder SignOrder(Room room) =>
        new(room.MasterId, InstantChatFrames.OrderMyNotice, 0, InstantChatFrames.SignOrderData(room.Id));

    private static List<(InstantChatMember Member, bool Master)> MembersOf(Room room) =>
        room.Members.Select((member, index) => (member, index == 0)).ToList();

    private static void Broadcast(InstantChatOutbox outbox, Room room, ushort opcode, byte[] body)
    {
        foreach (var member in room.Members) outbox.Messages.Add(new CoupleMessage(member.CharacterId, opcode, body));
    }

    private static InstantChatOutbox Refuse(InstantChatOutbox outbox, InstantChatMember who, ushort opcode, byte code, string note)
    {
        outbox.Messages.Add(new CoupleMessage(who.CharacterId, opcode, InstantChatFrames.KindAndCode(code)));
        outbox.Notes.Add($"'{who.Name}' {note}");
        return outbox;
    }

    private ushort? NextRoomId()
    {
        var start = _lastRoomId;
        var candidate = start;
        do
        {
            candidate++;
            if (candidate is 0 or LobbyRoomId) continue;
            if (!_rooms.ContainsKey(candidate))
            {
                _lastRoomId = candidate;
                return candidate;
            }
        }
        while (candidate != start);
        return null;
    }
}
