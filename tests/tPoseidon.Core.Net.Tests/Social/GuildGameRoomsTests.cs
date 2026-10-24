using System.Text;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Core.Net.Tests.Social;

public sealed class GuildGameRoomsTests
{
    private static readonly Dictionary<string, GuildGameEntrant> Online = new()
    {
        ["m"] = new GuildGameEntrant(1, "m", 50, 100, "A", 0xFFFF),
        ["a"] = new GuildGameEntrant(2, "a", 40, 100, "A", 0),
        ["b"] = new GuildGameEntrant(3, "b", 30, 100, "A", 0),
        ["c"] = new GuildGameEntrant(5, "c", 20, 100, "A", 0),
        ["x"] = new GuildGameEntrant(4, "x", 60, 200, "B", 0xFFFF),
        ["y"] = new GuildGameEntrant(6, "y", 70, 200, "B", 0),
    };

    private static readonly GuildRoomCaptions Captions = new("battle", "event");
    private static readonly ulong[] TwoMembers = [1, 2];

    private static byte[] MakePayload(byte mode = GuildGameRooms.LeagueMode, byte max = 3, string password = "")
    {
        var bytes = new List<byte> { mode, max, 2, 179, 1, 10, GuildGameOrders.TeamMatchType };
        bytes.AddRange([.. Encoding.ASCII.GetBytes("T"), 0]);
        bytes.AddRange([.. Encoding.ASCII.GetBytes(password), 0]);
        return bytes.ToArray();
    }

    private static GuildRoomEntry Made(GuildGameRooms rooms, byte mode = GuildGameRooms.LeagueMode, byte max = 3, string password = "")
    {
        var made = rooms.TryMake(Online["m"], GuildGameRooms.ReadMake(MakePayload(mode, max, password), Encoding.ASCII)!, true);
        Assert.Null(made.Error);
        return made.Room!;
    }

    [Fact]
    public void DetailListsBothSidesOrMissing()
    {
        var rooms = new GuildGameRooms(Encoding.ASCII);
        Assert.Equal(GuildGameRooms.DetailMissing, rooms.DetailOf(2, 7).Opcode);
        rooms.Add(new GuildGameRoom(2, 7, new GuildGameSide(11, "G", [new GuildGameMember(40, "a"), new GuildGameMember(55, "b")]), null));
        var (opcode, payload) = rooms.DetailOf(2, 7);
        Assert.Equal(GuildGameRooms.Detail, opcode);
        byte[] expected =
        [
            2, 0, 0, 0, 7, 0, 0, 0,
            2, 40, 0, (byte)'a', 0, 55, 0, (byte)'b', 0, 55, 0, (byte)'G', 0,
            0, 0, 0, 0,
        ];
        Assert.Equal(expected, payload);
        Assert.True(GuildGameRooms.TryReadDetail([2, 7, 0, 0, 0], out var type, out var room));
        Assert.Equal((2, 7u), (type, room));
    }

    [Fact]
    public void MakeReadsTheClientLayoutAndStartsWithTheMasterAlone()
    {
        var request = GuildGameRooms.ReadMake(MakePayload(GuildGameRooms.FriendlyMode, 4, "pw"), Encoding.ASCII)!;
        Assert.Equal(new GuildRoomMake(GuildGameRooms.FriendlyMode, 4, 2, 179, 1, 10, GuildGameOrders.TeamMatchType, "T", "pw"), request);
        Assert.Null(GuildGameRooms.ReadMake(MakePayload().AsSpan(0, 8), Encoding.ASCII));

        var rooms = new GuildGameRooms(Encoding.ASCII);
        Assert.Equal(GuildGameRooms.NoRight, rooms.TryMake(Online["a"], request, true).Error);
        Assert.Equal(GuildGameRooms.OfficeError, rooms.TryMake(Online["m"], request, false).Error);
        Assert.Equal(GuildGameRooms.NoGuild, rooms.TryMake(null, request, true).Error);
        var made = rooms.TryMake(Online["m"], request, true);
        Assert.Null(made.Error);
        Assert.Equal(100u, made.Room!.Key);
        Assert.Equal(179u, made.Room.MapChoice);
        Assert.Equal(GuildGameRooms.FriendlyNoLadder, made.Room.NoLadderFlag);
        Assert.Equal(new[] { Online["m"] }, made.Room.Sides[0].Members);
        Assert.Equal(GuildGameRooms.AlreadyInRoom, rooms.TryMake(Online["m"], request, true).Error);
        Assert.Equal(new byte[] { GuildGameRooms.AlreadyInRoom, 0 }, rooms.Refusal(GuildGameRooms.AlreadyInRoom));
    }

    [Theory]
    [InlineData(GuildGameRooms.FriendlyMode, GuildGameRooms.FriendlyNoLadder)]
    [InlineData(GuildGameRooms.LeagueMode, GuildGameRooms.LadderCounts)]
    [InlineData(GuildGameRooms.EventMode, GuildGameRooms.EventNoLadder)]
    public void TheGameModeSetsTheLadderFlag(byte mode, byte flag)
    {
        Assert.Equal(flag, GuildGameRooms.NoLadderFlagOf(mode));
    }

    [Fact]
    public void MastersInviteGuildMembersUpToTheRoomSize()
    {
        var rooms = new GuildGameRooms(Encoding.ASCII);
        var room = Made(rooms, max: 3);
        Assert.Equal(GuildGameRooms.NoRight, rooms.Invite(Online["a"], Online["b"]).Error);
        Assert.Equal(GuildGameRooms.MemberCheckFailed, rooms.Invite(Online["m"], Online["x"]).Error);
        Assert.Equal(GuildGameRooms.MemberCheckFailed, rooms.Invite(Online["m"], null).Error);
        Assert.Null(rooms.Invite(Online["m"], Online["a"]).Error);
        Assert.Equal(GuildGameRooms.AlreadyInRoom, rooms.Invite(Online["m"], Online["a"]).Error);
        Assert.Null(rooms.Invite(Online["m"], Online["b"]).Error);
        Assert.Equal(GuildGameRooms.TooManyParticipants, rooms.Invite(Online["m"], Online["c"]).Error);

        var accepted = rooms.Answer(2, accepted: true)!;
        Assert.True(accepted.Joined);
        Assert.Equal(room, accepted.Room);
        var refused = rooms.Answer(3, accepted: false)!;
        Assert.False(refused.Joined);
        Assert.Null(rooms.Answer(3, accepted: true));
        Assert.Equal(TwoMembers, room.Sides[0].Members.Select(member => member.CharacterId));
        Assert.Equal(room, rooms.RoomOf(2));
    }

    [Fact]
    public void JoinFillsTheSecondSideAndChecksThePassword()
    {
        var rooms = new GuildGameRooms(Encoding.ASCII);
        Made(rooms, password: "p");
        Assert.Null(GuildGameRooms.ReadJoin([100, 0, 0, 0], Encoding.ASCII));
        byte[] wrong = [100, 0, 0, 0, (byte)'q', 0];
        Assert.Equal(GuildGameRooms.WrongPassword, rooms.TryJoin(Online["x"], GuildGameRooms.ReadJoin(wrong, Encoding.ASCII)!, true).Error);
        byte[] missing = [101, 0, 0, 0, (byte)'p', 0];
        Assert.Equal(GuildGameRooms.RoomMissing, rooms.TryJoin(Online["x"], GuildGameRooms.ReadJoin(missing, Encoding.ASCII)!, true).Error);
        byte[] right = [100, 0, 0, 0, (byte)'p', 0];
        var joined = rooms.TryJoin(Online["x"], GuildGameRooms.ReadJoin(right, Encoding.ASCII)!, true);
        Assert.Null(joined.Error);
        Assert.Equal(2, joined.Room!.Sides.Count);
        Assert.Equal(GuildGameRooms.Detail, rooms.DetailOf(0, 100).Opcode);
        Assert.NotNull(rooms.Close(100));
        Assert.Equal(GuildGameRooms.DetailMissing, rooms.DetailOf(0, 100).Opcode);
    }

    [Fact]
    public void OnlyTheHostStartsAndBothSidesNeedThreeParticipants()
    {
        var rooms = new GuildGameRooms(Encoding.ASCII);
        var room = Made(rooms, max: 3);
        Assert.Equal(GuildGameRooms.RoomMissing, rooms.TryStart(1).Error);
        rooms.TryJoin(Online["x"], new GuildRoomJoin(100, string.Empty), true);
        Assert.Equal(GuildGameRooms.NoRight, rooms.TryStart(4).Error);
        Assert.Equal(GuildGameRooms.TooFewParticipants, rooms.TryStart(1).Error);

        var small = new GuildGameRooms(Encoding.ASCII, allowSmallTeams: true);
        Made(small);
        small.TryJoin(Online["x"], new GuildRoomJoin(100, string.Empty), true);
        var started = small.TryStart(1);
        Assert.Null(started.Error);
        Assert.True(started.Room!.Started);
        Assert.Equal(GuildGameRooms.NoRight, small.TryStart(1).Error);
        Assert.Equal(GuildGameRooms.NoRight, small.Invite(Online["m"], Online["a"]).Error);
        Assert.Null(small.Leave(1));
        Assert.Equal(2, room.Sides.Count);
    }

    [Fact]
    public void LeavingRemovesAMemberTheGuestSideOrTheWholeRoom()
    {
        var rooms = new GuildGameRooms(Encoding.ASCII);
        var room = Made(rooms);
        rooms.Invite(Online["m"], Online["a"]);
        rooms.Answer(2, accepted: true);
        rooms.TryJoin(Online["x"], new GuildRoomJoin(100, string.Empty), true);
        rooms.Invite(Online["x"], Online["y"]);
        rooms.Answer(6, accepted: true);

        Assert.Equal(GuildRoomLeaveKind.MemberLeft, rooms.Leave(2)!.Kind);
        var removal = rooms.Remove(4, "y")!;
        Assert.Equal(("y", "x"), (removal.Removed.Name, removal.By.Name));
        Assert.Null(rooms.Remove(4, "x"));
        var side = rooms.Leave(4)!;
        Assert.Equal(GuildRoomLeaveKind.SideLeft, side.Kind);
        Assert.Single(room.Sides);
        var closed = rooms.Leave(1)!;
        Assert.Equal(GuildRoomLeaveKind.RoomClosed, closed.Kind);
        Assert.Null(rooms.Find(100));
        Assert.Null(rooms.Leave(1));
    }

    [Fact]
    public void ListWritesTheCaptionAndTheClientRoomRecord()
    {
        var rooms = new GuildGameRooms(Encoding.ASCII);
        Made(rooms, max: 5);
        var expected = new List<byte>();
        expected.AddRange([.. Encoding.ASCII.GetBytes("battle"), 0]);
        expected.AddRange([1, 0, 0, 0, 100, 0, 0, 0, 0, (byte)'T', 0]);
        expected.AddRange([5, 0, 0, 0]);
        expected.AddRange([0, 0, 0, 0, GuildGameRooms.LeagueMode, GuildGameOrders.TeamMatchType, GuildGameRooms.WaitingState, 0]);
        expected.AddRange([100, 0, 0, 0, (byte)'A', 0, (byte)'m', 0]);
        expected.AddRange([7, 0, 0, 0, 50, 0, 0, 0, 1, 0, 0, 0]);
        expected.AddRange(new byte[2 + 4]);
        expected.AddRange([0, 0, 0, 0, 0, 0]);
        expected.AddRange(new byte[12 + 2 + 4]);
        Assert.Equal(expected.ToArray(), rooms.RoomList(Captions, guild => guild == 100 ? (byte)7 : (byte)0));

        var events = new GuildGameRooms(Encoding.ASCII);
        Made(events, mode: GuildGameRooms.EventMode);
        Assert.Equal(Encoding.ASCII.GetBytes("event\0"), events.RoomList(Captions, _ => 0).AsSpan(0, 6).ToArray());
    }

    [Fact]
    public void RoomFramesCarryTheParticipants()
    {
        var rooms = new GuildGameRooms(Encoding.ASCII);
        var room = Made(rooms);
        var made = rooms.MadeFor(room, [Online["a"]], _ => 0);
        var participants = new byte[] { 1, 100, 0, 0, 0, 50, 0, (byte)'m', 0, 0, 0, 0, 0 };
        var invitable = new byte[] { 1, (byte)'a', 0, 40, 0, 0, 0, 0, 0, 0, 0 };
        Assert.Equal(invitable.Concat(participants).ToArray(), made[^(invitable.Length + participants.Length)..]);
        Assert.Equal(participants, rooms.StateFor(room, _ => 0)[^participants.Length..]);
        Assert.Equal(participants[1..], rooms.Participant(Online["m"]));
        Assert.Equal(new byte[] { (byte)'a', 0, 8, 0, 0, 0 }, rooms.NameAndCode("a", 8));
        Assert.Equal(new byte[] { (byte)'a', 0, (byte)'m', 0 }, rooms.Removed("a", "m"));
        Assert.True(GuildGameRooms.TryReadAnswer([1, 0x2F, 0, 0, 0], out var accepted, out var code));
        Assert.Equal((true, 0x2Fu), (accepted, code));
        Assert.Equal("a", GuildGameRooms.ReadName([(byte)'a', 0], Encoding.ASCII));
    }

    [Fact]
    public void LadderListKeepsTheTopTwentyGuilds()
    {
        var rooms = new GuildGameRooms(Encoding.ASCII);
        var rows = Enumerable.Range(1, 25).Select(index => new GuildLadderListRow("G", (uint)index, 10, 6, 3, 1200 - index)).ToList();
        var payload = rooms.LadderListFor(rows);
        Assert.Equal(GuildGameRooms.LadderListLength, BitConverter.ToUInt16(payload, 0));
        Assert.Equal(2 + GuildGameRooms.LadderListLength * (2 + 5 * 4), payload.Length);
        Assert.Equal(new byte[] { (byte)'G', 0, 1, 0, 0, 0, 10, 0, 0, 0, 6, 0, 0, 0, 3, 0, 0, 0, 0xAF, 0x04, 0, 0 }, payload[2..24]);
    }

    [Fact]
    public void ClosedRoomListsRefuseMakeAndJoinWithCodeTwelve()
    {
        var rooms = new GuildGameRooms(Encoding.ASCII);
        Assert.Equal(GuildGameRooms.RoomListOpen, rooms.GameOpenFlag);
        rooms.SetGameOpenFlag(0);
        Assert.Equal(0, rooms.GameOpenFlag);
        var make = GuildGameRooms.ReadMake(MakePayload(), Encoding.ASCII)!;
        var refused = rooms.TryMake(Online["m"], make, true);
        Assert.Equal((GuildGameRooms.MakeError, GuildGameRooms.RoomListClosed), (refused.ErrorOpcode, refused.Error!.Value));

        rooms.SetGameOpenFlag(2);
        Assert.Null(rooms.TryMake(Online["m"], make, true).Error);
        rooms.SetGameOpenFlag(0);
        var joinRefused = rooms.TryJoin(Online["x"], new GuildRoomJoin(100, string.Empty), true);
        Assert.Equal((GuildGameRooms.JoinError, GuildGameRooms.RoomListClosed), (joinRefused.ErrorOpcode, joinRefused.Error!.Value));
    }

    [Fact]
    public void RoomGameMasterFlagStartsAtOneAndCanBeSet()
    {
        var rooms = new GuildGameRooms(Encoding.ASCII);
        Assert.Null(rooms.GameMasterFlagOf(100));
        Assert.False(rooms.SetGameMasterFlag(100, 0));
        Made(rooms);

        Assert.Equal(GuildGameRooms.NewRoomGameMasterFlag, rooms.GameMasterFlagOf(100));
        Assert.True(rooms.SetGameMasterFlag(100, 7));
        Assert.Equal((byte)7, rooms.GameMasterFlagOf(100));
    }
}
