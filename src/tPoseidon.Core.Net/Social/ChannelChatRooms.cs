using System.Text;

namespace tPoseidon.Core.Net.Social;

public sealed class ChannelChatRooms
{
    public const ushort DefaultMakeLimitLevel = 10;
    public const int MaxRooms = 500;
    public const int PageSize = 10;
    public const int MaxPage = 100;
    public const int MaxBlocked = 10;
    public const int ClientCacheLimit = 200;
    private const int FieldTitle = 0;
    private const int FieldMembers = 1;
    private const int FieldMaxMembers = 2;
    private const int FieldPassword = 3;

    private sealed record PendingCreate(InstantChatMember Member, ChannelChatCreate Create, ushort RoomId, ushort Number);

    private sealed class Seat
    {
        public required InstantChatMember Member { get; init; }
        public bool Master { get; set; }
    }

    private sealed class Room
    {
        public required ushort Id { get; init; }
        public required ushort Number { get; init; }
        public required byte Kind { get; init; }
        public required string Title { get; set; }
        public required string Password { get; set; }
        public required ushort MaxMembers { get; set; }
        public List<Seat> Seats { get; } = new();
        public List<string> Blocked { get; } = new();
        public long[] Versions { get; } = new long[4];
        public Seat? Master => Seats.FirstOrDefault(seat => seat.Master);
    }

    private sealed class ClientCache
    {
        public Dictionary<ushort, long[]> Rooms { get; } = new();
        public List<ushort> Order { get; } = new();
    }

    private readonly Encoding _text;
    private readonly ushort _makeLimitLevel;
    private readonly ChatRoomIdIssuer _ids;
    private readonly object _gate = new();
    private readonly Dictionary<ushort, Room> _rooms = new();
    private readonly Dictionary<ulong, ushort> _roomOf = new();
    private readonly Dictionary<ulong, PendingCreate> _pending = new();
    private readonly Dictionary<ulong, ClientCache> _caches = new();
    private readonly HashSet<ushort> _numbersUsed = new();
    private ushort _lastNumber;
    private long _version;

    public ChannelChatRooms(Encoding text, ushort makeLimitLevel, ChatRoomIdIssuer ids)
    {
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _makeLimitLevel = makeLimitLevel;
        _ids = ids ?? throw new ArgumentNullException(nameof(ids));
    }

    public ushort? RoomOf(ulong characterId)
    {
        lock (_gate) return _roomOf.TryGetValue(characterId, out var roomId) ? roomId : null;
    }

    public bool HasRoom(ushort roomId)
    {
        lock (_gate) return _rooms.ContainsKey(roomId);
    }

    public int RoomCount
    {
        get
        {
            lock (_gate) return _rooms.Count;
        }
    }

    public InstantChatMember? MemberById(ushort roomId, ushort connectionId)
    {
        lock (_gate)
        {
            return _rooms.TryGetValue(roomId, out var room) ? room.Seats.Select(seat => seat.Member).FirstOrDefault(member => member.ConnectionId == connectionId) : null;
        }
    }

    public InstantChatMember? MemberByName(ushort roomId, string name)
    {
        lock (_gate)
        {
            return _rooms.TryGetValue(roomId, out var room)
                ? room.Seats.Select(seat => seat.Member).FirstOrDefault(member => string.Equals(member.Name, name, StringComparison.OrdinalIgnoreCase))
                : null;
        }
    }

    public InstantChatOutbox Create(InstantChatMember maker, ChannelChatCreate create)
    {
        ArgumentNullException.ThrowIfNull(maker);
        var outbox = new InstantChatOutbox();
        lock (_gate)
        {
            if (Busy(maker.CharacterId))
            {
                return RefuseCreate(outbox, maker, create.Kind, InstantChatFrames.AlreadyInRoom, "create refused: already in a channel room");
            }

            if (_rooms.Count + _pending.Count > MaxRooms)
            {
                return RefuseCreate(outbox, maker, create.Kind, InstantChatFrames.RoomNotMade, "create refused: too many rooms");
            }

            if (_ids.Issue() is not { } roomId)
            {
                return RefuseCreate(outbox, maker, create.Kind, InstantChatFrames.RoomNotMade, "create refused: no free room id");
            }

            if (NextNumber() is not { } number)
            {
                _ids.Release(roomId);
                return RefuseCreate(outbox, maker, create.Kind, InstantChatFrames.RoomNotMade, "create refused: no free list number");
            }

            _pending[maker.CharacterId] = new PendingCreate(maker, create, roomId, number);
            outbox.OnceOrders.Add(LevelQuery(maker.CharacterId));
            outbox.Notes.Add($"'{maker.Name}' asks to make channel room '{create.Title}' (kind {create.Kind}); level asked from the game server");
        }

        return outbox;
    }

    public InstantChatOutbox LevelReported(ulong characterId, ushort level)
    {
        var outbox = new InstantChatOutbox();
        lock (_gate)
        {
            if (!_pending.Remove(characterId, out var pending)) return outbox;
            if (level < _makeLimitLevel)
            {
                Discard(pending);
                RefuseCreate(outbox, pending.Member, pending.Create.Kind, InstantChatFrames.LevelTooLow, $"create refused: level {level} < {_makeLimitLevel}");
                return outbox;
            }

            var create = pending.Create;
            var room = new Room
            {
                Id = pending.RoomId,
                Number = pending.Number,
                Kind = create.Kind,
                Title = create.Title,
                Password = create.Password,
                MaxMembers = create.MaxMembers,
            };
            room.Seats.Add(new Seat { Member = pending.Member, Master = true });
            Touch(room, FieldMembers);
            _rooms[room.Id] = room;
            _roomOf[pending.Member.CharacterId] = room.Id;
            outbox.Messages.Add(new CoupleMessage(pending.Member.CharacterId, InstantChatFrames.Created,
                ChannelChatFrames.CreatedBody(room.Kind, room.Id, room.Title, room.Password.Length > 0, room.MaxMembers, _text)));
            outbox.Notes.Add($"channel room {room.Id} '{room.Title}' (kind {room.Kind}, no. {room.Number}) made by '{pending.Member.Name}' (max {room.MaxMembers}{(room.Password.Length > 0 ? ", password" : string.Empty)})");
        }

        return outbox;
    }

    public InstantChatOutbox Enter(InstantChatMember joiner, ChannelChatEnter enter)
    {
        ArgumentNullException.ThrowIfNull(joiner);
        var outbox = new InstantChatOutbox();
        lock (_gate)
        {
            if (!ChannelChatFrames.IsRoomKind(enter.Kind) || !_rooms.TryGetValue(enter.RoomId, out var room) || room.Kind != enter.Kind)
            {
                return RefuseEnter(outbox, joiner, enter.Kind, InstantChatFrames.NoRoom, $"enter {enter.RoomId} refused: no such room");
            }

            if (Busy(joiner.CharacterId))
            {
                return RefuseEnter(outbox, joiner, enter.Kind, InstantChatFrames.AlreadyInRoom, $"enter {enter.RoomId} refused: already in a channel room");
            }

            if (room.Seats.Count >= room.MaxMembers)
            {
                return RefuseEnter(outbox, joiner, enter.Kind, InstantChatFrames.RoomFull, $"enter {enter.RoomId} refused: full");
            }

            if (room.Master is null)
            {
                return RefuseEnter(outbox, joiner, enter.Kind, InstantChatFrames.RoomNotMade, $"enter {enter.RoomId} refused: no master");
            }

            if (!string.Equals(enter.Password, room.Password, StringComparison.Ordinal))
            {
                return RefuseEnter(outbox, joiner, enter.Kind, InstantChatFrames.WrongPassword, $"enter {enter.RoomId} refused: wrong password");
            }

            if (room.Blocked.Contains(joiner.Name, StringComparer.Ordinal))
            {
                return RefuseEnter(outbox, joiner, enter.Kind, ChannelChatFrames.Blocked, $"enter {enter.RoomId} refused: on the block list");
            }

            Broadcast(outbox, room, InstantChatFrames.MemberIn, ChannelChatFrames.MemberInBody(room.Id, joiner, false, _text));
            room.Seats.Add(new Seat { Member = joiner });
            _roomOf[joiner.CharacterId] = room.Id;
            Touch(room, FieldMembers);
            outbox.Messages.Add(new CoupleMessage(joiner.CharacterId, InstantChatFrames.Entered,
                ChannelChatFrames.EnteredBody(room.Kind, room.Id, room.Title, room.Password.Length > 0, room.MaxMembers, MembersOf(room), _text)));
            outbox.Notes.Add($"'{joiner.Name}' entered channel room {room.Id} ({room.Seats.Count}/{room.MaxMembers})");
        }

        return outbox;
    }

    public InstantChatOutbox Leave(ulong characterId, ushort roomId)
    {
        var outbox = new InstantChatOutbox();
        lock (_gate)
        {
            if (DropPending(characterId)) return outbox;
            if (RoomHolding(characterId, roomId) is { } room) LeaveRoom(outbox, room, characterId, tellLeaver: true);
        }

        return outbox;
    }

    public InstantChatOutbox Released(ulong characterId, bool clientAlive)
    {
        var outbox = new InstantChatOutbox();
        lock (_gate)
        {
            DropPending(characterId);
            if (_roomOf.TryGetValue(characterId, out var roomId) && _rooms.TryGetValue(roomId, out var room))
            {
                LeaveRoom(outbox, room, characterId, tellLeaver: clientAlive);
            }

            _caches.Remove(characterId);
        }

        return outbox;
    }

    public InstantChatOutbox Say(ulong characterId, InstantChatLine line)
    {
        var outbox = new InstantChatOutbox();
        lock (_gate)
        {
            if (RoomHolding(characterId, line.RoomId) is not { } room) return outbox;
            var sender = SeatOf(room, characterId);
            Broadcast(outbox, room, InstantChatFrames.ChatLine, InstantChatFrames.ChatLineBody(room.Id, sender.Member.ConnectionId, line.Text, line.Style, _text));
        }

        return outbox;
    }

    public InstantChatOutbox Close(ulong characterId, ushort roomId)
    {
        var outbox = new InstantChatOutbox();
        lock (_gate)
        {
            if (RoomHolding(characterId, roomId) is not { } room) return outbox;
            if (!SeatOf(room, characterId).Master)
            {
                outbox.Messages.Add(new CoupleMessage(characterId, InstantChatFrames.CloseRefused, InstantChatFrames.RoomAndByte(room.Id, InstantChatFrames.NotMaster)));
                return outbox;
            }

            Broadcast(outbox, room, InstantChatFrames.Closed, InstantChatFrames.Room(room.Id));
            foreach (var seat in room.Seats) _roomOf.Remove(seat.Member.CharacterId);
            room.Seats.Clear();
            Remove(room);
            outbox.Notes.Add($"channel room {room.Id} '{room.Title}' closed");
        }

        return outbox;
    }

    public InstantChatOutbox SetTitle(ulong characterId, ushort roomId, string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        return MasterChange(characterId, roomId, InstantChatFrames.SetTitleRefused, InstantChatFrames.TitleSet, room =>
        {
            room.Title = InstantChatFrames.Limit(title, InstantChatFrames.TitleBytes, _text);
            Touch(room, FieldTitle);
            return (InstantChatFrames.TitleChanged, InstantChatFrames.RoomAndText(room.Id, room.Title, _text));
        });
    }

    public InstantChatOutbox SetMaxMembers(ulong characterId, ushort roomId, ushort maxMembers) =>
        MasterChange(characterId, roomId, InstantChatFrames.SetMaxMembersRefused, InstantChatFrames.MaxMembersSet, room =>
        {
            room.MaxMembers = maxMembers;
            Touch(room, FieldMaxMembers);
            return (InstantChatFrames.MaxMembersChanged, InstantChatFrames.RoomAndByte(room.Id, (byte)maxMembers));
        });

    public InstantChatOutbox SetPassword(ulong characterId, ushort roomId, string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        return MasterChange(characterId, roomId, InstantChatFrames.SetPasswordRefused, InstantChatFrames.PasswordSet, room =>
        {
            room.Password = InstantChatFrames.Limit(password, InstantChatFrames.PasswordBytes, _text);
            Touch(room, FieldPassword);
            return (InstantChatFrames.PasswordChanged, InstantChatFrames.RoomAndByte(room.Id, password.Length > 0 ? (byte)1 : (byte)0));
        });
    }

    public InstantChatOutbox Kick(ulong characterId, ushort roomId, ushort target)
    {
        var outbox = new InstantChatOutbox();
        lock (_gate)
        {
            if (RoomHolding(characterId, roomId) is not { } room) return outbox;
            var kicker = SeatOf(room, characterId);
            var kicked = room.Seats.FirstOrDefault(seat => seat.Member.ConnectionId == target);
            var code = !kicker.Master ? InstantChatFrames.NotMaster
                : target == kicker.Member.ConnectionId ? InstantChatFrames.SameMember
                : kicked is null ? InstantChatFrames.NotInRoom
                : (byte)0;
            if (code != 0)
            {
                outbox.Messages.Add(new CoupleMessage(characterId, InstantChatFrames.KickRefused, ChannelChatFrames.RoomAndMemberAndCode(room.Id, target, code)));
                return outbox;
            }

            outbox.Messages.Add(new CoupleMessage(characterId, InstantChatFrames.KickDone, InstantChatFrames.RoomAndMember(room.Id, target)));
            Broadcast(outbox, room, InstantChatFrames.Kicked, InstantChatFrames.RoomAndMember(room.Id, target));
            room.Seats.Remove(kicked!);
            _roomOf.Remove(kicked!.Member.CharacterId);
            Touch(room, FieldMembers);
            outbox.Notes.Add($"'{kicked.Member.Name}' kicked from channel room {room.Id}");
        }

        return outbox;
    }

    public InstantChatOutbox Handover(ulong characterId, ushort roomId, ushort target)
    {
        var outbox = new InstantChatOutbox();
        lock (_gate)
        {
            if (RoomHolding(characterId, roomId) is not { } room) return outbox;
            var requester = SeatOf(room, characterId);
            var successor = room.Seats.FirstOrDefault(seat => seat.Member.ConnectionId == target);
            var code = !requester.Master ? InstantChatFrames.NotMaster : successor is null ? InstantChatFrames.NotInRoom : (byte)0;
            if (code != 0)
            {
                outbox.Messages.Add(new CoupleMessage(characterId, ChannelChatFrames.DelegateRefused, ChannelChatFrames.RoomAndMemberAndCode(room.Id, target, code)));
                return outbox;
            }

            PassMaster(outbox, room, successor!);
            outbox.Messages.Add(new CoupleMessage(characterId, ChannelChatFrames.DelegateDone, InstantChatFrames.RoomAndMember(room.Id, target)));
        }

        return outbox;
    }

    public InstantChatOutbox Block(ulong characterId, ChannelChatBlockRequest request)
    {
        var outbox = new InstantChatOutbox();
        lock (_gate)
        {
            if (RoomHolding(characterId, request.RoomId) is not { } room) return outbox;
            var requester = SeatOf(room, characterId);
            var code = !requester.Master ? InstantChatFrames.NotMaster
                : string.Equals(request.Name, requester.Member.Name, StringComparison.Ordinal) ? InstantChatFrames.SameMember
                : request.Add && room.Blocked.Contains(request.Name, StringComparer.Ordinal) ? ChannelChatFrames.BlockExists
                : request.Add && room.Blocked.Count > MaxBlocked - 1 ? ChannelChatFrames.BlockFull
                : !request.Add && !room.Blocked.Contains(request.Name, StringComparer.Ordinal) ? InstantChatFrames.NotInRoom
                : (byte)0;
            if (code != 0)
            {
                outbox.Messages.Add(new CoupleMessage(characterId, ChannelChatFrames.BlockRefused,
                    ChannelChatFrames.BlockRefusedBody(room.Id, request.Name, request.Add, code, _text)));
                return outbox;
            }

            Broadcast(outbox, room, ChannelChatFrames.BlockChanged, ChannelChatFrames.BlockBody(room.Id, request.Name, request.Add, _text));
            var stored = InstantChatFrames.Limit(request.Name, 0x40, _text);
            if (request.Add) room.Blocked.Add(stored);
            else room.Blocked.Remove(stored);
            outbox.Messages.Add(new CoupleMessage(characterId, ChannelChatFrames.BlockDone, ChannelChatFrames.BlockBody(room.Id, request.Name, request.Add, _text)));
        }

        return outbox;
    }

    public InstantChatOutbox BlockList(ulong requester, ushort roomId)
    {
        var outbox = new InstantChatOutbox();
        lock (_gate)
        {
            outbox.Messages.Add(_rooms.TryGetValue(roomId, out var room)
                ? new CoupleMessage(requester, ChannelChatFrames.BlockListed, ChannelChatFrames.BlockListBody(room.Id, room.Blocked, _text))
                : new CoupleMessage(requester, ChannelChatFrames.BlockListMissing, InstantChatFrames.RoomAndByte(roomId, InstantChatFrames.NoRoom)));
        }

        return outbox;
    }

    public InstantChatOutbox Info(ulong requester, ushort roomId)
    {
        var outbox = new InstantChatOutbox();
        lock (_gate)
        {
            if (!_rooms.TryGetValue(roomId, out var room) || room.Master is null)
            {
                outbox.Messages.Add(new CoupleMessage(requester, InstantChatFrames.RoomMissing, InstantChatFrames.Room(roomId)));
            }
        }

        return outbox;
    }

    public InstantChatOutbox Message(ulong characterId, ChannelChatMessage message)
    {
        var outbox = new InstantChatOutbox();
        lock (_gate)
        {
            if (RoomHolding(characterId, message.RoomId) is not { } room) return outbox;
            var sender = SeatOf(room, characterId);
            var target = room.Seats.FirstOrDefault(seat => string.Equals(seat.Member.Name, message.Name, StringComparison.OrdinalIgnoreCase));
            if (target is null) return outbox;
            outbox.Messages.Add(new CoupleMessage(target.Member.CharacterId, ChannelChatFrames.Messaged,
                ChannelChatFrames.MessagedBody(room.Id, message.Text, sender.Member.Name, _text)));
        }

        return outbox;
    }

    public InstantChatOutbox List(ulong requester, ChannelChatListRequest request)
    {
        var outbox = new InstantChatOutbox();
        lock (_gate)
        {
            var kind = request.Kind;
            var page = request.Page;
            if (!ChannelChatFrames.IsListKind(kind))
            {
                outbox.Messages.Add(new CoupleMessage(requester, ChannelChatFrames.ListRefused,
                    ChannelChatFrames.KindPageAndCode(kind, page, ChannelChatFrames.ListKindUnknown)));
                return outbox;
            }

            var rooms = _rooms.Values.Where(room => kind == ChannelChatFrames.AllKind || room.Kind == kind).OrderBy(room => room.Number).ToList();
            var pages = rooms.Count / PageSize + (rooms.Count % PageSize != 0 ? 1 : 0);
            if (page > pages || page == 0 || page > MaxPage)
            {
                outbox.Messages.Add(new CoupleMessage(requester, ChannelChatFrames.ListRefused,
                    ChannelChatFrames.KindPageAndCode(kind, page, rooms.Count == 0 ? ChannelChatFrames.ListEmpty : ChannelChatFrames.ListPastEnd)));
                return outbox;
            }

            var onPage = pages == page && rooms.Count % PageSize != 0 ? rooms.Count % PageSize : PageSize;
            outbox.Messages.Add(new CoupleMessage(requester, ChannelChatFrames.ListHeader,
                ChannelChatFrames.ListHeaderBody(kind, page, (byte)pages, (byte)onPage)));
            var cache = CacheOf(requester);
            foreach (var room in rooms.Skip((page - 1) * PageSize).Take(onPage))
            {
                outbox.Messages.Add(new CoupleMessage(requester, cache.Rooms.TryGetValue(room.Id, out var seen) ? ChannelChatFrames.ListEntryChanges : ChannelChatFrames.ListEntry,
                    EntryBody(room, seen)));
                Remember(cache, room);
            }

            outbox.Messages.Add(new CoupleMessage(requester, ChannelChatFrames.ListEnd, ChannelChatFrames.KindAndPage(kind, page)));
        }

        return outbox;
    }

    private byte[] EntryBody(Room room, long[]? seen)
    {
        var members = (byte)room.Seats.Count;
        var max = (byte)room.MaxMembers;
        var hasPassword = room.Password.Length > 0;
        if (seen is null)
        {
            return ChannelChatFrames.ListEntryBody(room.Kind, room.Number, room.Id, room.Title, members, max, hasPassword, _text);
        }

        var changed = Enumerable.Range(0, 4).Select(field => seen[field] != room.Versions[field]).ToArray();
        var mask = (byte)((changed[FieldTitle] ? 1 : 0) | (changed[FieldMembers] ? 2 : 0) | (changed[FieldMaxMembers] ? 4 : 0) | (changed[FieldPassword] ? 8 : 0));
        return ChannelChatFrames.ListEntryChangesBody(room.Kind, room.Number, room.Id, mask,
            changed[FieldTitle] ? room.Title : null,
            changed[FieldMembers] ? members : null,
            changed[FieldMaxMembers] ? max : null,
            changed[FieldPassword] ? hasPassword : null,
            _text);
    }

    private ClientCache CacheOf(ulong characterId)
    {
        if (!_caches.TryGetValue(characterId, out var cache))
        {
            cache = new ClientCache();
            _caches[characterId] = cache;
        }

        return cache;
    }

    private static void Remember(ClientCache cache, Room room)
    {
        if (cache.Rooms.Remove(room.Id)) cache.Order.Remove(room.Id);
        if (cache.Rooms.Count > ClientCacheLimit - 1)
        {
            cache.Rooms.Remove(cache.Order[0]);
            cache.Order.RemoveAt(0);
        }

        cache.Rooms[room.Id] = (long[])room.Versions.Clone();
        cache.Order.Add(room.Id);
    }

    private InstantChatOutbox MasterChange(ulong characterId, ushort roomId, ushort refused, ushort done,
        Func<Room, (ushort Opcode, byte[] Body)> change)
    {
        var outbox = new InstantChatOutbox();
        lock (_gate)
        {
            if (RoomHolding(characterId, roomId) is not { } room) return outbox;
            if (!SeatOf(room, characterId).Master)
            {
                outbox.Messages.Add(new CoupleMessage(characterId, refused, InstantChatFrames.RoomAndByte(room.Id, InstantChatFrames.NotMaster)));
                return outbox;
            }

            var (opcode, body) = change(room);
            Broadcast(outbox, room, opcode, body);
            outbox.Messages.Add(new CoupleMessage(characterId, done, InstantChatFrames.Room(room.Id)));
        }

        return outbox;
    }

    private void LeaveRoom(InstantChatOutbox outbox, Room room, ulong characterId, bool tellLeaver)
    {
        var leaver = SeatOf(room, characterId);
        if (leaver.Master)
        {
            var successor = room.Seats.FirstOrDefault(seat => !ReferenceEquals(seat, leaver));
            if (successor is not null) PassMaster(outbox, room, successor);
        }

        var body = InstantChatFrames.RoomAndMember(room.Id, leaver.Member.ConnectionId);
        if (tellLeaver) outbox.Messages.Add(new CoupleMessage(characterId, InstantChatFrames.MemberOut, body));
        room.Seats.Remove(leaver);
        _roomOf.Remove(characterId);
        Touch(room, FieldMembers);
        Broadcast(outbox, room, InstantChatFrames.MemberOut, body);
        outbox.Notes.Add($"'{leaver.Member.Name}' left channel room {room.Id}");
        if (room.Seats.Count == 0)
        {
            Remove(room);
            outbox.Notes.Add($"channel room {room.Id} '{room.Title}' removed (empty)");
        }
    }

    private static void PassMaster(InstantChatOutbox outbox, Room room, Seat successor)
    {
        (room.Master ?? room.Seats[0]).Master = false;
        successor.Master = true;
        room.Seats.Remove(successor);
        room.Seats.Insert(0, successor);
        Broadcast(outbox, room, ChannelChatFrames.Delegated, InstantChatFrames.RoomAndMember(room.Id, successor.Member.ConnectionId));
    }

    private bool Busy(ulong characterId) => _roomOf.ContainsKey(characterId) || _pending.ContainsKey(characterId);

    private bool DropPending(ulong characterId)
    {
        if (!_pending.Remove(characterId, out var pending)) return false;
        Discard(pending);
        return true;
    }

    private void Discard(PendingCreate pending)
    {
        _ids.Release(pending.RoomId);
        _numbersUsed.Remove(pending.Number);
    }

    private void Remove(Room room)
    {
        _rooms.Remove(room.Id);
        _ids.Release(room.Id);
        _numbersUsed.Remove(room.Number);
    }

    private void Touch(Room room, int field) => room.Versions[field] = ++_version;

    private static PartyOrder LevelQuery(ulong characterId) =>
        new(characterId, InstantChatFrames.OrderReportLevel, 0, Array.Empty<byte>());

    private Room? RoomHolding(ulong characterId, ushort roomId) =>
        _roomOf.TryGetValue(characterId, out var held) && held == roomId && _rooms.TryGetValue(roomId, out var room) ? room : null;

    private static Seat SeatOf(Room room, ulong characterId) => room.Seats.First(seat => seat.Member.CharacterId == characterId);

    private static List<(InstantChatMember Member, bool Master)> MembersOf(Room room) =>
        room.Seats.Select(seat => (seat.Member, seat.Master)).ToList();

    private static void Broadcast(InstantChatOutbox outbox, Room room, ushort opcode, byte[] body)
    {
        foreach (var seat in room.Seats) outbox.Messages.Add(new CoupleMessage(seat.Member.CharacterId, opcode, body));
    }

    private static InstantChatOutbox RefuseCreate(InstantChatOutbox outbox, InstantChatMember who, byte kind, byte code, string note)
    {
        outbox.Messages.Add(new CoupleMessage(who.CharacterId, InstantChatFrames.CreateRefused, ChannelChatFrames.KindAndCode(kind, code)));
        outbox.Notes.Add($"'{who.Name}' {note}");
        return outbox;
    }

    private static InstantChatOutbox RefuseEnter(InstantChatOutbox outbox, InstantChatMember who, byte kind, byte code, string note)
    {
        outbox.Messages.Add(new CoupleMessage(who.CharacterId, InstantChatFrames.EnterRefused, ChannelChatFrames.KindAndCode(kind, code)));
        outbox.Notes.Add($"'{who.Name}' {note}");
        return outbox;
    }

    private ushort? NextNumber()
    {
        var start = _lastNumber;
        var candidate = start;
        do
        {
            candidate++;
            if (candidate == 0 || _numbersUsed.Contains(candidate)) continue;
            _numbersUsed.Add(candidate);
            _lastNumber = candidate;
            return candidate;
        }
        while (candidate != start);
        return null;
    }
}
