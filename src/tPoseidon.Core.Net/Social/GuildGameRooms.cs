using System.Text;

namespace tPoseidon.Core.Net.Social;

public sealed record GuildGameMember(ushort Level, string Name);

public sealed record GuildGameSide(uint GuildCode, string GuildName, IReadOnlyList<GuildGameMember> Members);

public sealed record GuildGameRoom(byte GameType, uint RoomId, GuildGameSide? SideA, GuildGameSide? SideB);

public sealed record GuildGameEntrant(ulong CharacterId, string Name, ushort Level, uint GuildCode, string GuildName, ushort Rights);

public sealed record GuildRoomMake(
    byte Mode, byte MaxParticipants, byte LevelLimit, byte MapChoice, byte TimeLimit, byte OutCount, byte GameType, string Title, string Password);

public sealed record GuildRoomJoin(uint RoomKey, string Password);

public sealed record GuildRoomCaptions(string Battle, string EventBattle)
{
    public const string TextTable = "GuildUIParams";
    public const string BattleText = "WaitRoom_Title1";
    public const string EventBattleText = "WaitRoom_Title2";

    public static GuildRoomCaptions None { get; } = new(string.Empty, string.Empty);

    public static GuildRoomCaptions From(IReadOnlyDictionary<string, string> texts)
    {
        ArgumentNullException.ThrowIfNull(texts);
        return new GuildRoomCaptions(texts.GetValueOrDefault(BattleText, string.Empty), texts.GetValueOrDefault(EventBattleText, string.Empty));
    }
}

public sealed record GuildLadderListRow(string GuildName, uint GuildCode, int Matches, int Wins, int Losses, int Points);

public sealed class GuildRoomSide
{
    public GuildRoomSide(uint guildCode, string guildName, GuildGameEntrant master)
    {
        GuildCode = guildCode;
        GuildName = guildName;
        Master = master;
        Members.Add(master);
    }

    public uint GuildCode { get; }
    public string GuildName { get; }
    public GuildGameEntrant Master { get; }
    public List<GuildGameEntrant> Members { get; } = new();
    public Dictionary<ulong, GuildGameEntrant> Invited { get; } = new();

    public ushort AverageLevel => Members.Count == 0 ? (ushort)0 : (ushort)(Members.Sum(member => member.Level) / Members.Count);
}

public sealed class GuildRoomEntry
{
    public GuildRoomEntry(uint key, GuildRoomMake settings, GuildRoomSide host)
    {
        Key = key;
        Settings = settings;
        Sides.Add(host);
    }

    public uint Key { get; }
    public GuildRoomMake Settings { get; }
    public List<GuildRoomSide> Sides { get; } = new();
    public bool Started { get; set; }

    public byte GameType => Settings.GameType;
    public byte MaxParticipants => Settings.MaxParticipants;
    public uint MapChoice => Settings.MapChoice;
    public bool HasPassword => Settings.Password.Length > 0;
    public byte NoLadderFlag => GuildGameRooms.NoLadderFlagOf(Settings.Mode);

    public byte GameMasterFlag { get; set; } = GuildGameRooms.NewRoomGameMasterFlag;

    public IEnumerable<GuildGameEntrant> Everyone => Sides.SelectMany(side => side.Members);

    public GuildRoomSide? SideOf(ulong characterId) => Sides.FirstOrDefault(side => side.Members.Any(member => member.CharacterId == characterId));
}

public readonly record struct GuildRoomResult(byte? Error, ushort ErrorOpcode, GuildRoomEntry? Room)
{
    public static GuildRoomResult Refused(ushort opcode, byte code) => new(code, opcode, null);
}

public readonly record struct GuildInviteResult(byte? Error, GuildRoomEntry? Room)
{
    public static GuildInviteResult Refused(byte code) => new(code, null);
}

public sealed record GuildInviteAnswer(GuildRoomEntry Room, GuildRoomSide Side, GuildGameEntrant Invitee, bool Joined);

public sealed record GuildRoomRemoval(GuildRoomEntry Room, GuildGameEntrant Removed, GuildGameEntrant By);

public enum GuildRoomLeaveKind
{
    MemberLeft,
    SideLeft,
    RoomClosed,
}

public sealed record GuildRoomLeave(GuildRoomLeaveKind Kind, GuildRoomEntry Room, GuildRoomSide Side, GuildGameEntrant Leaver, IReadOnlyList<GuildGameEntrant> Notified);

public sealed class GuildGameRooms
{
    public const ushort Make = 0x2C89;
    public const ushort MakeOk = 0x2C8A;
    public const ushort MakeError = 0x2C8B;
    public const ushort Join = 0x2C8C;
    public const ushort JoinOk = 0x2C8D;
    public const ushort JoinError = 0x2C8E;
    public const ushort RoomClosedNotice = 0x2C90;
    public const ushort LeaveRequest = 0x2C92;
    public const ushort SideLeftNotice = 0x2C93;
    public const ushort WatchRequest = 0x2C95;
    public const ushort WatchOk = 0x2C96;
    public const ushort WatchError = 0x2C97;
    public const ushort StartRequest = 0x2C98;
    public const ushort ListRequest = 0x2C9B;
    public const ushort List = 0x2C9C;
    public const ushort DetailRequest = 0x2CA1;
    public const ushort Detail = 0x2CA2;
    public const ushort DetailMissing = 0x2CA3;
    public const ushort InviteRequest = 0x2CA6;
    public const ushort InvitePending = 0x2CA7;
    public const ushort InviteRefusedNotice = 0x2CA8;
    public const ushort RemoveRequest = 0x2CA9;
    public const ushort RemovedNotice = 0x2CAA;
    public const ushort Invitation = 0x2CAC;
    public const ushort InviteAnswer = 0x2CAD;
    public const ushort RoomState = 0x2CAE;
    public const ushort PlayoffRequest = 0x2CB2;
    public const ushort PlayoffList = 0x2CB3;
    public const ushort LadderListRequest = 0x2CB6;
    public const ushort LadderList = 0x2CB7;
    public const ushort ParticipantAdded = 0x2CB8;

    public const int MakeFixedBytes = 7;
    public const int MinParticipants = 3;
    public const int SidesPerRoom = 2;
    public const int LadderListLength = 20;
    public const ushort RightGuildGame = 0x20;
    public const uint NoPlayoff = 1;
    public const uint RoomClosedCode = 0;

    public const byte FriendlyMode = 0;
    public const byte LeagueMode = 1;
    public const byte EventMode = 2;
    public const byte LadderCounts = 0;
    public const byte FriendlyNoLadder = 1;
    public const byte EventNoLadder = 2;

    public const byte NoGuild = 2;
    public const byte NoRight = 3;
    public const byte AlreadyInRoom = 5;
    public const byte OfficeError = 6;
    public const byte TooFewParticipants = 7;
    public const byte MemberCheckFailed = 8;
    public const byte RoomMissing = 9;
    public const byte WrongPassword = 10;
    public const byte JoinOfficeError = 0x0B;
    public const byte TooManyParticipants = 0x0D;
    public const byte RoomListClosed = 0x0C;
    public const byte WatchersRefused = 0x12;
    public const byte NewRoomGameMasterFlag = 1;
    public const byte RoomListOpen = 1;
    public const byte WaitingState = 0;
    public const byte StartedState = 3;

    private readonly object _gate = new();
    private readonly Dictionary<uint, GuildRoomEntry> _rooms = new();
    private readonly Dictionary<(byte GameType, uint RoomId), GuildGameRoom> _detailOnly = new();
    private readonly Encoding _text;
    private readonly bool _allowSmallTeams;
    private readonly byte[] _listOpen = { RoomListOpen, RoomListOpen, RoomListOpen };

    public byte GameOpenFlag
    {
        get { lock (_gate) return _listOpen[ListOf(GuildGameOrders.TeamMatchType)]; }
    }

    public void SetGameOpenFlag(byte value)
    {
        lock (_gate) Array.Fill(_listOpen, value);
    }

    public byte? GameMasterFlagOf(uint roomKey)
    {
        lock (_gate) return _rooms.TryGetValue(roomKey, out var room) ? room.GameMasterFlag : null;
    }

    public bool SetGameMasterFlag(uint roomKey, byte value)
    {
        lock (_gate)
        {
            if (!_rooms.TryGetValue(roomKey, out var room)) return false;
            room.GameMasterFlag = value;
            return true;
        }
    }

    public static int ListOf(byte gameType) => gameType switch
    {
        GuildGameOrders.DeathMatchType => 1,
        GuildGameOrders.MonsterMatchType => 2,
        _ => 0,
    };

    public static byte NoLadderFlagOf(byte mode) => mode switch
    {
        LeagueMode => LadderCounts,
        EventMode => EventNoLadder,
        _ => FriendlyNoLadder,
    };

    private bool ListOpenLocked(byte gameType) => _listOpen[ListOf(gameType)] != 0;

    public GuildGameRooms(Encoding text, bool allowSmallTeams = false)
    {
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _allowSmallTeams = allowSmallTeams;
    }

    public void Add(GuildGameRoom room)
    {
        ArgumentNullException.ThrowIfNull(room);
        lock (_gate) _detailOnly[(room.GameType, room.RoomId)] = room;
    }

    public GuildRoomEntry? Close(uint roomKey)
    {
        lock (_gate) return _rooms.Remove(roomKey, out var room) ? room : null;
    }

    public GuildRoomEntry? Find(uint roomKey)
    {
        lock (_gate) return _rooms.GetValueOrDefault(roomKey);
    }

    public GuildRoomEntry? RoomOf(ulong characterId)
    {
        lock (_gate) return _rooms.Values.FirstOrDefault(room => room.SideOf(characterId) is not null);
    }

    public static GuildRoomMake? ReadMake(ReadOnlySpan<byte> payload, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (payload.Length < MakeFixedBytes + 2) return null;
        var r = new PacketReader(payload);
        var mode = r.ReadByte();
        var maxParticipants = r.ReadByte();
        var levelLimit = r.ReadByte();
        var mapChoice = r.ReadByte();
        var timeLimit = r.ReadByte();
        var outCount = r.ReadByte();
        var gameType = r.ReadByte();
        if (payload[r.Position..].IndexOf((byte)0) < 0) return null;
        var title = r.ReadNulTerminated(text);
        if (r.Remaining < 1 || payload[r.Position..].IndexOf((byte)0) < 0) return null;
        var password = r.ReadNulTerminated(text);
        return new GuildRoomMake(mode, maxParticipants, levelLimit, mapChoice, timeLimit, outCount, gameType, title, password);
    }

    public static GuildRoomJoin? ReadJoin(ReadOnlySpan<byte> payload, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (payload.Length < sizeof(uint) + 1) return null;
        var r = new PacketReader(payload);
        var key = r.ReadUInt32();
        if (payload[r.Position..].IndexOf((byte)0) < 0) return null;
        return new GuildRoomJoin(key, r.ReadNulTerminated(text));
    }

    public static string? ReadName(ReadOnlySpan<byte> payload, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var end = payload.IndexOf((byte)0);
        return end < 0 ? null : text.GetString(payload[..end]);
    }

    public static bool TryReadAnswer(ReadOnlySpan<byte> payload, out bool accepted, out uint code)
    {
        accepted = false;
        code = 0;
        if (payload.Length < 1 + sizeof(uint)) return false;
        var r = new PacketReader(payload);
        accepted = r.ReadByte() != 0;
        code = r.ReadUInt32();
        return true;
    }

    public GuildRoomResult TryMake(GuildGameEntrant? requester, GuildRoomMake request, bool requesterInLobby)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (requester is null || requester.GuildCode == 0) return GuildRoomResult.Refused(MakeError, NoGuild);
        if (!HasGameRight(requester)) return GuildRoomResult.Refused(MakeError, NoRight);
        if (!requesterInLobby) return GuildRoomResult.Refused(MakeError, OfficeError);
        lock (_gate)
        {
            if (InRoomLocked(requester.GuildCode) || InvitedLocked(requester.CharacterId)) return GuildRoomResult.Refused(MakeError, AlreadyInRoom);
            if (request.GameType != GuildGameOrders.MonsterMatchType && !ListOpenLocked(request.GameType)) return GuildRoomResult.Refused(MakeError, RoomListClosed);
            if (request.MaxParticipants < 1) return GuildRoomResult.Refused(MakeError, TooManyParticipants);
            var room = new GuildRoomEntry(requester.GuildCode, request, new GuildRoomSide(requester.GuildCode, requester.GuildName, requester));
            _rooms[room.Key] = room;
            return new GuildRoomResult(null, MakeOk, room);
        }
    }

    public GuildRoomResult TryJoin(GuildGameEntrant? requester, GuildRoomJoin request, bool requesterInLobby)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (requester is null || requester.GuildCode == 0) return GuildRoomResult.Refused(JoinError, NoGuild);
        if (!HasGameRight(requester)) return GuildRoomResult.Refused(JoinError, NoRight);
        if (!requesterInLobby) return GuildRoomResult.Refused(JoinError, JoinOfficeError);
        lock (_gate)
        {
            if (InRoomLocked(requester.GuildCode) || InvitedLocked(requester.CharacterId)) return GuildRoomResult.Refused(JoinError, AlreadyInRoom);
            if (!_rooms.TryGetValue(request.RoomKey, out var room) || room.Started) return GuildRoomResult.Refused(JoinError, RoomMissing);
            if (!ListOpenLocked(room.GameType)) return GuildRoomResult.Refused(JoinError, RoomListClosed);
            if (room.HasPassword && !string.Equals(room.Settings.Password, request.Password, StringComparison.Ordinal)) return GuildRoomResult.Refused(JoinError, WrongPassword);
            if (room.Sides.Count >= SidesPerRoom) return GuildRoomResult.Refused(JoinError, JoinOfficeError);
            room.Sides.Add(new GuildRoomSide(requester.GuildCode, requester.GuildName, requester));
            return new GuildRoomResult(null, JoinOk, room);
        }
    }

    public GuildRoomResult TryWatch(GuildRoomJoin request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (!_rooms.TryGetValue(request.RoomKey, out var room)) return GuildRoomResult.Refused(WatchError, RoomMissing);
            if (!ListOpenLocked(room.GameType)) return GuildRoomResult.Refused(WatchError, RoomListClosed);
            if (room.GameMasterFlag == 0) return GuildRoomResult.Refused(WatchError, WatchersRefused);
            if (room.HasPassword && !string.Equals(room.Settings.Password, request.Password, StringComparison.Ordinal)) return GuildRoomResult.Refused(WatchError, WrongPassword);
            return new GuildRoomResult(null, WatchOk, room);
        }
    }

    public GuildInviteResult Invite(GuildGameEntrant inviter, GuildGameEntrant? invitee)
    {
        ArgumentNullException.ThrowIfNull(inviter);
        lock (_gate)
        {
            var room = _rooms.Values.FirstOrDefault(candidate => candidate.Sides.Any(side => side.Master.CharacterId == inviter.CharacterId));
            if (room is null || room.Started) return GuildInviteResult.Refused(NoRight);
            var side = room.Sides.First(candidate => candidate.Master.CharacterId == inviter.CharacterId);
            if (invitee is null || invitee.GuildCode != side.GuildCode || invitee.CharacterId == inviter.CharacterId) return GuildInviteResult.Refused(MemberCheckFailed);
            if (_rooms.Values.Any(candidate => candidate.SideOf(invitee.CharacterId) is not null) || InvitedLocked(invitee.CharacterId)) return GuildInviteResult.Refused(AlreadyInRoom);
            if (side.Members.Count + side.Invited.Count >= room.MaxParticipants) return GuildInviteResult.Refused(TooManyParticipants);
            side.Invited[invitee.CharacterId] = invitee;
            return new GuildInviteResult(null, room);
        }
    }

    public GuildInviteAnswer? Answer(ulong inviteeId, bool accepted)
    {
        lock (_gate)
        {
            foreach (var room in _rooms.Values)
            {
                foreach (var side in room.Sides)
                {
                    if (!side.Invited.Remove(inviteeId, out var invitee)) continue;
                    var joined = accepted && !room.Started && side.Members.Count < room.MaxParticipants;
                    if (joined) side.Members.Add(invitee);
                    return new GuildInviteAnswer(room, side, invitee, joined);
                }
            }
            return null;
        }
    }

    public GuildRoomRemoval? Remove(ulong masterId, string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        lock (_gate)
        {
            foreach (var room in _rooms.Values)
            {
                if (room.Started || room.Sides.FirstOrDefault(side => side.Master.CharacterId == masterId) is not { } side) continue;
                var removed = side.Members.FirstOrDefault(member => member.CharacterId != masterId && string.Equals(member.Name, name, StringComparison.Ordinal));
                if (removed is null) return null;
                side.Members.Remove(removed);
                return new GuildRoomRemoval(room, removed, side.Master);
            }
            return null;
        }
    }

    public GuildRoomLeave? Leave(ulong characterId)
    {
        lock (_gate)
        {
            foreach (var room in _rooms.Values)
            {
                if (room.Started || room.SideOf(characterId) is not { } side) continue;
                var leaver = side.Members.First(member => member.CharacterId == characterId);
                var notified = room.Everyone.ToList();
                if (side.Master.CharacterId != characterId)
                {
                    side.Members.Remove(leaver);
                    return new GuildRoomLeave(GuildRoomLeaveKind.MemberLeft, room, side, leaver, notified);
                }
                if (room.Sides[0] == side)
                {
                    _rooms.Remove(room.Key);
                    return new GuildRoomLeave(GuildRoomLeaveKind.RoomClosed, room, side, leaver, notified);
                }
                room.Sides.Remove(side);
                return new GuildRoomLeave(GuildRoomLeaveKind.SideLeft, room, side, leaver, notified);
            }
            return null;
        }
    }

    public GuildRoomResult TryStart(ulong requesterId)
    {
        lock (_gate)
        {
            var room = _rooms.Values.FirstOrDefault(candidate => candidate.Sides[0].Master.CharacterId == requesterId);
            if (room is null || room.Started) return GuildRoomResult.Refused(MakeError, NoRight);
            if (room.Sides.Count < SidesPerRoom) return GuildRoomResult.Refused(MakeError, RoomMissing);
            if (!_allowSmallTeams && room.Sides.Any(side => side.Members.Count < MinParticipants)) return GuildRoomResult.Refused(MakeError, TooFewParticipants);
            room.Started = true;
            foreach (var side in room.Sides) side.Invited.Clear();
            return new GuildRoomResult(null, MakeOk, room);
        }
    }

    private static bool HasGameRight(GuildGameEntrant entrant) => (entrant.Rights & RightGuildGame) != 0;

    private bool InRoomLocked(uint guildCode) => _rooms.Values.Any(room => room.Sides.Any(side => side.GuildCode == guildCode));

    private bool InvitedLocked(ulong characterId) => _rooms.Values.Any(room => room.Sides.Any(side => side.Invited.ContainsKey(characterId)));

    public byte[] Refusal(byte code)
    {
        var w = new PacketWriter(2);
        w.WriteByte(code);
        w.WriteNulTerminated(string.Empty, _text);
        return w.ToArray();
    }

    public byte[] RoomList(GuildRoomCaptions captions, Func<uint, byte> rankOf)
    {
        ArgumentNullException.ThrowIfNull(captions);
        ArgumentNullException.ThrowIfNull(rankOf);
        GuildRoomEntry[] rooms;
        lock (_gate) rooms = _rooms.Values.ToArray();
        var w = new PacketWriter(16 + rooms.Length * 128);
        w.WriteNulTerminated(rooms.Any(room => room.Settings.Mode == EventMode) ? captions.EventBattle : captions.Battle, _text);
        w.WriteUInt16((ushort)Math.Min(rooms.Length, ushort.MaxValue));
        foreach (var room in rooms.Take(ushort.MaxValue)) WriteRecord(w, room, rankOf);
        return w.ToArray();
    }

    public byte[] MadeFor(GuildRoomEntry room, IReadOnlyList<GuildGameEntrant> invitable, Func<uint, byte> rankOf)
    {
        ArgumentNullException.ThrowIfNull(room);
        ArgumentNullException.ThrowIfNull(invitable);
        ArgumentNullException.ThrowIfNull(rankOf);
        var own = room.Sides[0];
        var w = new PacketWriter(256);
        WriteRecord(w, room, rankOf);
        w.WriteByte((byte)Math.Min(invitable.Count, byte.MaxValue));
        foreach (var member in invitable.Take(byte.MaxValue))
        {
            w.WriteNulTerminated(member.Name, _text);
            w.WriteUInt32(member.Level);
            w.WriteUInt32(0);
        }
        WriteParticipants(w, own.Members);
        return w.ToArray();
    }

    public byte[] StateFor(GuildRoomEntry room, Func<uint, byte> rankOf)
    {
        ArgumentNullException.ThrowIfNull(room);
        ArgumentNullException.ThrowIfNull(rankOf);
        var w = new PacketWriter(256);
        WriteRecord(w, room, rankOf);
        WriteParticipants(w, room.Everyone.ToList());
        return w.ToArray();
    }

    public byte[] JoinedFor(GuildRoomEntry room, int side)
    {
        ArgumentNullException.ThrowIfNull(room);
        var joined = room.Sides[side];
        var w = new PacketWriter(96);
        w.WriteUInt32(room.Key);
        w.WriteUInt32(joined.GuildCode);
        w.WriteUInt16(joined.Master.Level);
        w.WriteByte((byte)side);
        w.WriteNulTerminated(joined.Master.Name, _text);
        w.WriteUInt32(0);
        w.WriteUInt32(0);
        w.WriteNulTerminated(joined.GuildName, _text);
        w.WriteByte(0);
        w.WriteByte(0);
        w.WriteUInt32(0);
        return w.ToArray();
    }

    public byte[] Participant(GuildGameEntrant member)
    {
        ArgumentNullException.ThrowIfNull(member);
        var w = new PacketWriter(32);
        WriteParticipant(w, member);
        return w.ToArray();
    }

    public byte[] Name(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var w = new PacketWriter(name.Length + 1);
        w.WriteNulTerminated(name, _text);
        return w.ToArray();
    }

    public byte[] NameAndCode(string name, uint code)
    {
        ArgumentNullException.ThrowIfNull(name);
        var w = new PacketWriter(name.Length + 5);
        w.WriteNulTerminated(name, _text);
        w.WriteUInt32(code);
        return w.ToArray();
    }

    public byte[] Removed(string removed, string by)
    {
        ArgumentNullException.ThrowIfNull(removed);
        ArgumentNullException.ThrowIfNull(by);
        var w = new PacketWriter(removed.Length + by.Length + 2);
        w.WriteNulTerminated(removed, _text);
        w.WriteNulTerminated(by, _text);
        return w.ToArray();
    }

    public static byte[] Code(uint code)
    {
        var w = new PacketWriter(sizeof(uint));
        w.WriteUInt32(code);
        return w.ToArray();
    }

    public byte[] LadderListFor(IReadOnlyList<GuildLadderListRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var shown = rows.Take(LadderListLength).ToList();
        var w = new PacketWriter(sizeof(ushort) + shown.Count * 48);
        w.WriteUInt16((ushort)shown.Count);
        foreach (var row in shown)
        {
            w.WriteNulTerminated(row.GuildName, _text);
            w.WriteUInt32(row.GuildCode);
            w.WriteUInt32((uint)Math.Max(0, row.Matches));
            w.WriteUInt32((uint)Math.Max(0, row.Wins));
            w.WriteUInt32((uint)Math.Max(0, row.Losses));
            w.WriteUInt32((uint)Math.Max(0, row.Points));
        }
        return w.ToArray();
    }

    private void WriteParticipants(PacketWriter w, IReadOnlyList<GuildGameEntrant> members)
    {
        w.WriteByte((byte)Math.Min(members.Count, byte.MaxValue));
        foreach (var member in members.Take(byte.MaxValue)) WriteParticipant(w, member);
    }

    private void WriteParticipant(PacketWriter w, GuildGameEntrant member)
    {
        w.WriteUInt32(member.GuildCode);
        w.WriteUInt16(member.Level);
        w.WriteNulTerminated(member.Name, _text);
        w.WriteUInt32(0);
    }

    private void WriteRecord(PacketWriter w, GuildRoomEntry room, Func<uint, byte> rankOf)
    {
        w.WriteUInt16(0);
        w.WriteUInt32(room.Key);
        w.WriteByte(room.HasPassword ? (byte)1 : (byte)0);
        w.WriteNulTerminated(room.Settings.Title, _text);
        w.WriteUInt32(room.MaxParticipants);
        w.WriteUInt32(0);
        w.WriteByte(room.Settings.Mode);
        w.WriteByte(room.GameType);
        w.WriteByte(room.Started ? StartedState : WaitingState);
        w.WriteByte(0);
        for (var side = 0; side < SidesPerRoom; side++)
        {
            var entry = side < room.Sides.Count ? room.Sides[side] : null;
            w.WriteUInt32(entry?.GuildCode ?? 0);
            w.WriteNulTerminated(entry?.GuildName ?? string.Empty, _text);
            w.WriteNulTerminated(entry?.Master.Name ?? string.Empty, _text);
            w.WriteUInt32(entry is null ? 0u : rankOf(entry.GuildCode));
            w.WriteUInt32((uint)(entry?.AverageLevel ?? 0));
            w.WriteUInt32((uint)(entry?.Members.Count ?? 0));
            w.WriteByte(0);
            w.WriteByte(0);
            w.WriteUInt32(0);
        }
        if (room.GameType is GuildGameOrders.DeathMatchType or GuildGameOrders.MonsterMatchType)
        {
            w.WriteUInt64(0);
            w.WriteUInt64(0);
        }
    }

    public static bool TryReadDetail(ReadOnlySpan<byte> payload, out byte gameType, out uint roomId)
    {
        gameType = 0;
        roomId = 0;
        if (payload.Length < 5) return false;
        var r = new PacketReader(payload);
        gameType = r.ReadByte();
        roomId = r.ReadUInt32();
        return true;
    }

    public (ushort Opcode, byte[] Payload) DetailOf(byte gameType, uint roomId)
    {
        GuildGameRoom? room;
        lock (_gate)
        {
            if (!_detailOnly.TryGetValue((gameType, roomId), out room) && _rooms.TryGetValue(roomId, out var entry) && entry.GameType == gameType)
            {
                room = new GuildGameRoom(gameType, roomId, SideOf(entry, 0), SideOf(entry, 1));
            }
        }
        if (room is null) return (DetailMissing, Array.Empty<byte>());
        var w = new PacketWriter(64);
        w.WriteUInt32(room.GameType);
        w.WriteUInt32(room.RoomId);
        WriteSide(w, room.SideA);
        WriteSide(w, room.SideB);
        return (Detail, w.ToArray());
    }

    private static GuildGameSide? SideOf(GuildRoomEntry room, int side) =>
        side < room.Sides.Count
            ? new GuildGameSide(room.Sides[side].GuildCode, room.Sides[side].GuildName,
                room.Sides[side].Members.Select(member => new GuildGameMember(member.Level, member.Name)).ToArray())
            : null;

    private void WriteSide(PacketWriter w, GuildGameSide? side)
    {
        if (side is null)
        {
            w.WriteByte(0);
            w.WriteUInt16(0);
            w.WriteNulTerminated(string.Empty, _text);
            return;
        }
        w.WriteByte((byte)Math.Min(side.Members.Count, byte.MaxValue));
        foreach (var member in side.Members.Take(byte.MaxValue))
        {
            w.WriteUInt16(member.Level);
            w.WriteNulTerminated(member.Name, _text);
        }
        w.WriteUInt16(side.Members.Count == 0 ? (ushort)0 : side.Members.Max(member => member.Level));
        w.WriteNulTerminated(side.GuildName, _text);
    }
}
