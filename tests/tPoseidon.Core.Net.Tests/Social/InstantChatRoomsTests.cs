using System.Text;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Core.Net.Tests.Social;

public sealed class InstantChatRoomsTests
{
    private static readonly Encoding Text = Encoding.ASCII;
    private static readonly InstantChatMember Master = new(0x1001, 11, "Master", 30);
    private static readonly InstantChatMember Guest = new(0x1002, 12, "Guest", 25);
    private static readonly InstantChatMember Third = new(0x1003, 13, "Third", 20);

    private static InstantChatRooms Rooms(ushort makeLimit = 1, ushort joinLimit = 1) => new(Text, makeLimit, joinLimit);

    private static InstantChatOutbox Created(InstantChatRooms rooms, InstantChatMember maker, ushort level, InstantChatCreate create)
    {
        var asked = rooms.Create(maker, create);
        Assert.Empty(asked.Messages);
        Assert.Equal(new PartyOrder(maker.CharacterId, InstantChatFrames.OrderReportLevel, 0, Array.Empty<byte>()).OrderType, Assert.Single(asked.OnceOrders).OrderType);
        return rooms.LevelReported(maker.CharacterId, level);
    }

    private static InstantChatOutbox Entered(InstantChatRooms rooms, InstantChatMember joiner, ushort level, InstantChatEnter enter)
    {
        var asked = rooms.Enter(joiner, enter);
        return asked.OnceOrders.Count == 0 ? asked : rooms.LevelReported(joiner.CharacterId, level);
    }

    private static ushort Make(InstantChatRooms rooms, string password = "", ushort max = 3)
    {
        var outbox = Created(rooms, Master, 30, new InstantChatCreate("Hello", password, max));
        var created = Assert.Single(outbox.Messages);
        Assert.Equal(InstantChatFrames.Created, created.Opcode);
        return BitConverter.ToUInt16(created.Payload, 1);
    }

    [Fact]
    public void CreatingARoomAnswersTheKrBodyAndShowsTheSign()
    {
        var rooms = Rooms();

        var outbox = Created(rooms, Master, 30, new InstantChatCreate("Hello", "pw", 5));

        var created = Assert.Single(outbox.Messages);
        Assert.Equal(new byte[] { 1, 2, 0, (byte)'H', (byte)'e', (byte)'l', (byte)'l', (byte)'o', 0, 1, 5, 0 }, created.Payload);
        Assert.Equal(2, outbox.Orders.Count);
        var order = outbox.Orders[0];
        Assert.Equal(new PartyOrder(Master.CharacterId, InstantChatFrames.OrderMyNotice, 0, order.Data), order);
        Assert.Equal(new byte[] { 2, 0 }, order.Data);
        Assert.Equal(InstantChatFrames.OrderNoticeState, outbox.Orders[1].OrderType);
        Assert.Equal(new byte[] { 1 }, outbox.Orders[1].Data);
        Assert.Equal((ushort)2, rooms.RoomOf(Master.CharacterId));
    }

    [Fact]
    public void ACreateIsRefusedInARoomOrBelowTheLevel()
    {
        var rooms = Rooms(makeLimit: 40);
        var low = Created(rooms, Master, 30, new InstantChatCreate("Hello", "", 3));
        Assert.Equal(new byte[] { 1, InstantChatFrames.LevelTooLow }, Assert.Single(low.Messages).Payload);
        Assert.Equal(InstantChatFrames.CreateRefused, low.Messages[0].Opcode);

        var open = Rooms();
        Make(open);
        var again = open.Create(Master, new InstantChatCreate("Again", "", 3));
        Assert.Equal(new byte[] { 1, InstantChatFrames.AlreadyInRoom }, Assert.Single(again.Messages).Payload);
    }

    [Fact]
    public void EnteringFollowsTheKrChecks()
    {
        var rooms = Rooms(joinLimit: 22);
        var roomId = Make(rooms, password: "pw", max: 2);

        Assert.Equal(InstantChatFrames.NoRoom, Code(Entered(rooms, Guest, 25, new InstantChatEnter(99, "pw"))));
        Assert.Equal(InstantChatFrames.WrongPassword, Code(Entered(rooms, Guest, 25, new InstantChatEnter(roomId, "no"))));
        Assert.Equal(InstantChatFrames.LevelTooLow, Code(Entered(rooms, Third, 20, new InstantChatEnter(roomId, "pw"))));
        Assert.Equal(InstantChatFrames.AlreadyInRoom, Code(Entered(rooms, Master, 30, new InstantChatEnter(roomId, "pw"))));

        var entered = Entered(rooms, Guest, 25, new InstantChatEnter(roomId, "pw"));

        var memberIn = entered.Messages.Single(message => message.Opcode == InstantChatFrames.MemberIn);
        Assert.Equal(Master.CharacterId, memberIn.To);
        Assert.Equal(new byte[] { 2, 0, 12, 0, (byte)'G', (byte)'u', (byte)'e', (byte)'s', (byte)'t', 0, 25, 0, 0, 0 }, memberIn.Payload);
        var joined = entered.Messages.Single(message => message.Opcode == InstantChatFrames.Entered);
        Assert.Equal(Guest.CharacterId, joined.To);
        Assert.Equal(1, joined.Payload[0]);
        Assert.Equal(new byte[] { 1, 2, 0, 2, 0 }, joined.Payload[9..14]);
        Assert.Equal(InstantChatFrames.RoomFull, Code(Entered(rooms, new InstantChatMember(0x1004, 14, "Late", 50), 50, new InstantChatEnter(roomId, "pw"))));
    }

    [Fact]
    public void ALineGoesToEveryMemberWithTheSender()
    {
        var rooms = Rooms();
        var roomId = Make(rooms);
        Entered(rooms, Guest, 25, new InstantChatEnter(roomId, string.Empty));

        var said = rooms.Say(Guest.CharacterId, new InstantChatLine(roomId, "hi", 7));

        Assert.Equal(new[] { Master.CharacterId, Guest.CharacterId }, said.Messages.Select(message => message.To));
        Assert.All(said.Messages, message => Assert.Equal(new byte[] { 2, 0, 12, 0, (byte)'h', (byte)'i', 0, 7, 0 }, message.Payload));
        Assert.Empty(rooms.Say(Third.CharacterId, new InstantChatLine(roomId, "hi", 7)).Messages);
    }

    [Fact]
    public void AMemberLeavingTellsEveryoneAndTheMasterLeavingClosesTheRoom()
    {
        var rooms = Rooms();
        var roomId = Make(rooms);
        Entered(rooms, Guest, 25, new InstantChatEnter(roomId, string.Empty));
        Entered(rooms, Third, 20, new InstantChatEnter(roomId, string.Empty));

        var left = rooms.Leave(Guest.CharacterId, roomId);
        Assert.Equal(new[] { Guest.CharacterId, Master.CharacterId, Third.CharacterId }, left.Messages.Select(message => message.To));
        Assert.All(left.Messages, message => Assert.Equal(InstantChatFrames.MemberOut, message.Opcode));

        var closed = rooms.Leave(Master.CharacterId, roomId);
        Assert.Equal(new[] { Master.CharacterId, Third.CharacterId }, closed.Messages.Select(message => message.To));
        Assert.All(closed.Messages, message => Assert.Equal(InstantChatFrames.Closed, message.Opcode));
        Assert.Equal(new[] { InstantChatFrames.OrderMyNotice, InstantChatFrames.OrderNoticeState, InstantChatFrames.OrderNoticeState }, closed.Orders.Select(order => order.OrderType));
        Assert.All(closed.Orders, order => Assert.Null(order.Data));
        Assert.Null(rooms.RoomOf(Third.CharacterId));
        Assert.Equal(InstantChatFrames.RoomMissing, Assert.Single(rooms.Info(Third.CharacterId, roomId).Messages).Opcode);
    }

    [Fact]
    public void OnlyTheMasterChangesTheRoomAndKicks()
    {
        var rooms = Rooms();
        var roomId = Make(rooms);
        Entered(rooms, Guest, 25, new InstantChatEnter(roomId, string.Empty));

        var refused = rooms.SetTitle(Guest.CharacterId, roomId, "Mine");
        Assert.Equal(new CoupleMessage(Guest.CharacterId, InstantChatFrames.SetTitleRefused, new byte[] { 2, 0, InstantChatFrames.NotMaster }).Payload, Assert.Single(refused.Messages).Payload);

        var titled = rooms.SetTitle(Master.CharacterId, roomId, "News");
        Assert.Equal(2, titled.Messages.Count(message => message.Opcode == InstantChatFrames.TitleChanged));
        Assert.Single(titled.Messages, message => message.Opcode == InstantChatFrames.TitleSet);
        Assert.Empty(titled.Orders);
        Assert.Equal(InstantChatFrames.OrderMyNotice, Assert.Single(titled.OnceOrders).OrderType);

        var locked = rooms.SetPassword(Master.CharacterId, roomId, "pw");
        Assert.All(locked.Messages.Where(message => message.Opcode == InstantChatFrames.PasswordChanged), message => Assert.Equal(new byte[] { 2, 0, 1 }, message.Payload));
        var bigger = rooms.SetMaxMembers(Master.CharacterId, roomId, 9);
        Assert.Empty(bigger.Orders);
        Assert.All(bigger.Messages.Where(message => message.Opcode == InstantChatFrames.MaxMembersChanged), message => Assert.Equal(new byte[] { 2, 0, 9 }, message.Payload));

        Assert.Equal(new byte[] { 2, 0, 11, 0, InstantChatFrames.SameMember }, Assert.Single(rooms.Kick(Master.CharacterId, roomId, 11).Messages).Payload);
        Assert.Equal(new byte[] { 2, 0, 99, 0, InstantChatFrames.NotInRoom }, Assert.Single(rooms.Kick(Master.CharacterId, roomId, 99).Messages).Payload);
        Assert.Equal(new byte[] { 2, 0, 11, 0, InstantChatFrames.NotMaster }, Assert.Single(rooms.Kick(Guest.CharacterId, roomId, 11).Messages).Payload);

        var kicked = rooms.Kick(Master.CharacterId, roomId, 12);
        Assert.Equal(InstantChatFrames.KickDone, kicked.Messages[0].Opcode);
        Assert.Equal(new[] { Master.CharacterId, Guest.CharacterId }, kicked.Messages.Skip(1).Select(message => message.To));
        Assert.Null(rooms.RoomOf(Guest.CharacterId));
    }

    [Fact]
    public void TheRoomInfoAndTheGameMasterRemoval()
    {
        var rooms = Rooms();
        var roomId = Make(rooms, password: "pw");
        Entered(rooms, Guest, 25, new InstantChatEnter(roomId, "pw"));

        Assert.Equal(new byte[] { 2, 0, 1, (byte)'H', (byte)'e', (byte)'l', (byte)'l', (byte)'o', 0, 1 }, Assert.Single(rooms.Info(Third.CharacterId, roomId).Messages).Payload);
        Assert.Null(rooms.RemoveByGameMaster(Third.CharacterId));
        var member = rooms.RemoveByGameMaster(Guest.CharacterId)!;
        Assert.Equal(new byte[] { 2, 0, InstantChatFrames.NotMaster }, Assert.Single(member.Messages).Payload);
        var master = rooms.RemoveByGameMaster(Master.CharacterId)!;
        Assert.Equal(2, master.Messages.Count(message => message.Opcode == InstantChatFrames.Closed));
    }

    [Fact]
    public void ARoomActionWaitsForTheLevelAndLeavingDropsIt()
    {
        var rooms = Rooms();
        var roomId = Make(rooms);

        rooms.Enter(Guest, new InstantChatEnter(roomId, string.Empty));
        Assert.Equal(InstantChatFrames.AlreadyInRoom, Assert.Single(rooms.Create(Guest, new InstantChatCreate("Mine", "", 3)).Messages).Payload[1]);
        Assert.Empty(rooms.Leave(Guest.CharacterId, roomId).Messages);
        Assert.Empty(rooms.LevelReported(Guest.CharacterId, 25).Messages);
        Assert.Null(rooms.RoomOf(Guest.CharacterId));
        Assert.Empty(rooms.LevelReported(Third.CharacterId, 25).Messages);
    }

    [Fact]
    public void LeavingCancelsTheRoomState()
    {
        var rooms = Rooms();
        var roomId = Make(rooms);
        Entered(rooms, Guest, 25, new InstantChatEnter(roomId, string.Empty));

        var left = rooms.Leave(Guest.CharacterId, roomId);

        var cancel = Assert.Single(left.Orders);
        Assert.Equal((Guest.CharacterId, InstantChatFrames.OrderNoticeState), (cancel.CharacterId, cancel.OrderType));
        Assert.Null(cancel.Data);
    }

    [Fact]
    public void TheKrLimitDefaultsAre20And10()
    {
        Assert.Equal(20, InstantChatRooms.DefaultMakeLimitLevel);
        Assert.Equal(10, InstantChatRooms.DefaultJoinLimitLevel);
    }

    [Fact]
    public void TheClientRequestsDecode()
    {
        Assert.True(InstantChatFrames.TryReadCreate(new byte[] { 1, (byte)'T', 0, (byte)'p', 0, 4, 0 }, Text, out var create));
        Assert.Equal(new InstantChatCreate("T", "p", 4), create);
        Assert.False(InstantChatFrames.TryReadCreate(new byte[] { 2, (byte)'T', 0, 0, 4, 0 }, Text, out _));
        Assert.True(InstantChatFrames.TryReadEnter(new byte[] { 1, 5, 0, 0 }, Text, out var enter));
        Assert.Equal(new InstantChatEnter(5, string.Empty), enter);
        Assert.True(InstantChatFrames.TryReadLine(new byte[] { 5, 0, (byte)'x', 0, 3, 0 }, Text, out var line));
        Assert.Equal(new InstantChatLine(5, "x", 3), line);
        Assert.Equal(new byte[] { 0x34, 0x12, 5, 0 }, InstantChatFrames.SignShownBody(0x1234, 5));
    }

    [Fact]
    public void TheGameMasterRemovalAnswersTheKrReplies()
    {
        var removed = GameMasterService.RemoveInstantChat(0x9001, "Friend", name => name == "Friend" ? 0x1002 : null, _ => true)!;
        Assert.Equal(GameMasterFrames.InstantChatRemoved, Assert.Single(removed.Messages).Opcode);
        Assert.Equal(GameMasterChat.RemoveInstantChatText, Assert.Single(removed.Texts).Name);

        var empty = GameMasterService.RemoveInstantChat(0x9001, "Friend", _ => 0x1002, _ => false)!;
        Assert.Equal(GameMasterFrames.InstantChatRemoveFail, Assert.Single(empty.Messages).Opcode);
        var offline = GameMasterService.RemoveInstantChat(0x9001, "Friend", _ => null, _ => true)!;
        Assert.Equal(GameMasterFrames.InstantChatRemoveFail, Assert.Single(offline.Messages).Opcode);
        Assert.Equal(0x14A3, GameMasterFrames.RemoveInstantChat);
    }

    private static byte Code(InstantChatOutbox outbox)
    {
        var refused = Assert.Single(outbox.Messages);
        Assert.Equal(InstantChatFrames.EnterRefused, refused.Opcode);
        return refused.Payload[1];
    }
}
