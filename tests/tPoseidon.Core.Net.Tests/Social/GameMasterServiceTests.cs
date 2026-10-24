using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Core.Net.Tests.Social;

public sealed class GameMasterServiceTests
{
    private const ulong Gm = 0x4001;
    private const string GmName = "Keeper";
    private const string Password = "secret";
    private const string Loopback = "127.0.0.1";

    private static readonly Encoding Text = Encoding.Latin1;
    private static readonly ulong[] OnlineGuildMembers = { 0x5001, 0x5002 };
    private static readonly string[] GuildMemoTexts = { "Again", "Hello" };
    private static readonly string[] MemoSentSubjects = { "Hello", "Other" };
    private static readonly string[] SummonArguments = { "2001", "3" };
    private static readonly string[] IssueArguments = { "4100", "1001", "10" };
    private static readonly string[] OxGiftArguments = { "1001", "2" };
    private static readonly string[] BlockArguments = { "2", "30", "Other" };
    private static readonly string[] UnblockArguments = { "2", "Other" };
    private static readonly string[] MusicArguments = { "town" };
    private static readonly string[] ZeroObjectArguments = { "0" };
    private static readonly string[] ObjectArguments = { "4097" };
    private static readonly string[] OtherArguments = { "Other" };
    private static readonly string[] NoticeArguments = { "7", "Hello" };
    private static readonly string[] BadNoticeArguments = { "x", "Hello" };
    private static readonly string[] KickReasonSubjects = { "Other", "spamming" };

    private sealed class FakeAuthority : IGmAuthorityStore
    {
        public List<(string Name, string Password, string Ip)> Checks { get; } = new();

        public bool IsGmClientLoginPermitted(string characterName, string password, string clientIp)
        {
            Checks.Add((characterName, password, clientIp));
            return characterName == GmName && password == Password && clientIp == Loopback;
        }

        public GmCharacterRegistration? RegisterGmCharacter(string characterName, string permittedIp) =>
            throw new NotSupportedException();

        public ulong? GmCharacterId(string characterName) => characterName == GmName ? Gm : null;
    }

    private sealed class FakeBlockStore : IGmBlockStore
    {
        public HashSet<ulong> Characters { get; } = new() { 0x4002, 0x4003 };

        public Dictionary<(ulong CharacterId, byte Type), (int Count, ushort Minutes, ulong Gm)> Rows { get; } = new();

        public int BlockCharacter(ulong characterId, byte blockType, ushort minutes, ulong gameMasterId)
        {
            if (!Characters.Contains(characterId)) return GmBlockRules.NoCharacter;
            Rows[(characterId, blockType)] = (Rows.TryGetValue((characterId, blockType), out var row) ? row.Count + 1 : 1, minutes, gameMasterId);
            return GmBlockRules.Ok;
        }

        public int UnblockCharacter(ulong characterId, byte blockType, ulong gameMasterId)
        {
            if (!Rows.TryGetValue((characterId, blockType), out var row) || row.Count == 0) return GmBlockRules.NoBlockRow;
            Rows[(characterId, blockType)] = (row.Count - 1, row.Minutes, gameMasterId);
            return GmBlockRules.Ok;
        }

        public Dictionary<string, byte> UserAuthority { get; } = new();

        public GmRunningBlock? LatestRunningBlock(ulong characterId) => null;

        public int SetUserAuthority(string characterName, byte authority)
        {
            if (characterName is not ("Other" or "Third")) return 0;
            UserAuthority[characterName] = authority;
            return 1;
        }
    }

    private static Character Named(ulong id, string name) => new() { CharacterId = id, OwnerAccountId = 1, Slot = 0, Name = name, Species = Species.Buffalo, FType = FType.Power };

    private static (GameMasterService Service, FakeBlockStore Records) WithBlockRecords()
    {
        var records = new FakeBlockStore();
        var friends = new InMemoryFriendStore(_ => null, name => name switch
        {
            "Other" => Named(0x4002, "Other"),
            "Away" => Named(0x4003, "Away"),
            "Lost" => Named(0x4009, "Lost"),
            _ => null,
        });
        return (new GameMasterService(new FakeAuthority(), Text) { Friends = friends, BlockRecords = records }, records);
    }

    private static byte[] WithPassword(string password, params byte[] rest)
    {
        var bytes = Text.GetBytes(password);
        return new[] { (byte)bytes.Length }.Concat(bytes).Concat(rest).ToArray();
    }

    [Fact]
    public void LoginWithAccountPasswordFromPermittedIpRepliesAndSetsTheGmAttribute()
    {
        var authority = new FakeAuthority();
        var service = new GameMasterService(authority, Text);

        var outbox = service.Login(Gm, GmName, WithPassword(Password), Loopback);

        Assert.NotNull(outbox);
        Assert.Equal((GmName, Password, Loopback), Assert.Single(authority.Checks));
        var reply = Assert.Single(outbox!.Messages);
        Assert.Equal(GameMasterFrames.LoginOk, reply.Opcode);
        Assert.Empty(reply.Payload);
        var order = Assert.Single(outbox.Orders);
        Assert.Equal(GameMasterFrames.OrderSetGmAttribute, order.OrderType);
        Assert.Equal(new byte[] { 1 }, order.Data);
        Assert.False(outbox.Disconnect);
        Assert.True(service.IsLoggedIn(Gm));
    }

    [Fact]
    public void RefusedLoginDisconnectsWithoutReply()
    {
        var service = new GameMasterService(new FakeAuthority(), Text);

        var outbox = service.Login(Gm, GmName, WithPassword("wrong"), Loopback);

        Assert.NotNull(outbox);
        Assert.True(outbox!.Disconnect);
        Assert.Equal(Gm, Assert.Single(outbox.Kicks));
        Assert.Empty(outbox.Messages);
        Assert.Empty(outbox.Orders);
        Assert.False(service.IsLoggedIn(Gm));
    }

    [Fact]
    public void LoginIsIgnoredWhenTheSessionWasAlreadyOpened()
    {
        var authority = new FakeAuthority();
        var service = new GameMasterService(authority, Text);
        service.OpenSession(Gm);

        var outbox = service.Login(Gm, GmName, WithPassword(Password), Loopback);

        Assert.NotNull(outbox);
        Assert.Empty(authority.Checks);
        Assert.Empty(outbox!.Messages);
        Assert.False(service.IsLoggedIn(Gm));
    }

    [Fact]
    public void PasswordLongerThanTheServerBufferIsNotARequest()
    {
        var service = new GameMasterService(new FakeAuthority(), Text);

        Assert.Null(service.Login(Gm, GmName, WithPassword(new string('x', GameMasterFrames.PasswordBufferSize)), Loopback));
        Assert.Null(service.Login(Gm, GmName, new byte[] { 5, 1, 2 }, Loopback));
    }

    [Fact]
    public void CommandsNeedTheLoginPasswordPrefix()
    {
        var service = new GameMasterService(new FakeAuthority(), Text);
        Assert.False(service.TryAuthorize(Gm, WithPassword(Password, 4), out _));

        service.Login(Gm, GmName, WithPassword(Password), Loopback);

        Assert.True(service.TryAuthorize(Gm, WithPassword(Password, 4), out var consumed));
        Assert.Equal(1 + Password.Length, consumed);
        Assert.False(service.TryAuthorize(Gm, WithPassword("other", 4), out _));
    }

    [Fact]
    public void EnteringAZoneResendsTheGmAttributeOnlyWhileLoggedIn()
    {
        var service = new GameMasterService(new FakeAuthority(), Text);
        Assert.Empty(service.ZoneEntered(Gm).Orders);

        service.Login(Gm, GmName, WithPassword(Password), Loopback);

        var order = Assert.Single(service.ZoneEntered(Gm).Orders);
        Assert.Equal(GameMasterFrames.OrderSetGmAttribute, order.OrderType);
        Assert.Equal(new byte[] { 1 }, order.Data);
    }

    [Fact]
    public void TeleportToZoneOrdersTheTargetOrRepliesWithTheFailure()
    {
        var service = new GameMasterService(new FakeAuthority(), Text);
        ulong? Online(string name) => name == "Other" ? 0x4002 : null;

        var order = Assert.Single(service.TeleportToZone(Gm, string.Empty, 23, Online).Orders);
        Assert.Equal((Gm, GameMasterFrames.OrderTeleportToZone), (order.CharacterId, order.OrderType));
        Assert.Equal(new byte[] { 23, 0 }, order.Data);

        Assert.Equal(0x4002ul, Assert.Single(service.TeleportToZone(Gm, "Other", 5, Online).Orders).CharacterId);
        Assert.Equal(GameMasterFrames.TeleportToZoneFail, Assert.Single(service.TeleportToZone(Gm, "Gone", 5, Online).Messages).Opcode);
        Assert.Equal(GameMasterFrames.TeleportToZoneFail, Assert.Single(service.TeleportToZone(Gm, string.Empty, GameMasterFrames.ZoneLimit, Online).Messages).Opcode);
    }

    [Fact]
    public void TeleportToChrIsOrderedOnTheAnchorsServer()
    {
        var service = new GameMasterService(new FakeAuthority(), Text);
        ulong? Online(string name) => name == "Other" ? 0x4002 : null;

        var order = Assert.Single(service.TeleportToChr(Gm, string.Empty, "Other", Online).Orders);
        Assert.Equal(0x4002ul, order.CharacterId);
        Assert.True(GameMasterFrames.TryReadChrToChr(order.Data, out var who, out var to));
        Assert.Equal((Gm, 0x4002ul), (who, to));
        Assert.Equal(22, Assert.IsType<byte[]>(order.Data).Length);
        Assert.Equal(GameMasterFrames.TeleportToChrFail, Assert.Single(service.TeleportToChr(Gm, string.Empty, "Gone", Online).Messages).Opcode);
    }

    [Fact]
    public void TeleportToChrOrderCarriesTheRelayedPosition()
    {
        var plain = GameMasterFrames.ChrToChr(Gm, 0x4002);
        Assert.True(GameMasterFrames.TryReadChrToChr(plain, out _, out _, out var plainZone, out _, out _));
        Assert.Equal(0, plainZone);

        var relayed = GameMasterFrames.ChrToChr(Gm, 0x4002, 23, 120, 340);
        Assert.Equal(GameMasterFrames.ChrToChrLength, relayed.Length);
        Assert.True(GameMasterFrames.TryReadChrToChr(relayed, out var who, out var to, out var zone, out var x, out var y));
        Assert.Equal((Gm, 0x4002ul, (ushort)23, (ushort)120, (ushort)340), (who, to, zone, x, y));
    }

    [Fact]
    public void KickRepliesOkAndKicksOnlyOnlineCharacters()
    {
        ulong? Online(string name) => name == "Other" ? 0x4002 : null;

        var done = GameMasterService.Kick(Gm, "Other", Online);
        Assert.Equal(0x4002ul, Assert.Single(done.Kicks));
        Assert.Equal(GameMasterFrames.KickOk, Assert.Single(done.Messages).Opcode);

        var failed = GameMasterService.Kick(Gm, "Gone", Online);
        Assert.Empty(failed.Kicks);
        Assert.Equal(GameMasterFrames.KickFail, Assert.Single(failed.Messages).Opcode);
    }

    [Fact]
    public void ToolLoginsDoNotSetTheGmAttributeAndDoNotKick()
    {
        var service = new GameMasterService(new FakeAuthority(), Text);

        var ok = service.Login(Gm, GmName, WithPassword(Password), Loopback, tool: true);
        Assert.Empty(ok!.Orders);
        Assert.True(service.IsLoggedIn(Gm, tool: true));
        Assert.False(service.IsLoggedIn(Gm));

        var refused = new GameMasterService(new FakeAuthority(), Text).Login(Gm, GmName, WithPassword("wrong"), Loopback, tool: true);
        Assert.Empty(refused!.Kicks);
        Assert.True(refused.Disconnect);
    }

    private static GameMasterService LoggedIn(bool tool = false)
    {
        var service = new GameMasterService(new FakeAuthority(), Text);
        service.Login(Gm, GmName, WithPassword(Password), Loopback, tool);
        return service;
    }

    [Fact]
    public void SuperRunOrdersTheSpeedAndRepliesEmpty()
    {
        var outbox = LoggedIn().SuperRun(Gm, false, 3);

        Assert.NotNull(outbox);
        var order = Assert.Single(outbox!.Orders);
        Assert.Equal((Gm, GameMasterFrames.OrderSuperRun), (order.CharacterId, order.OrderType));
        Assert.True(GameMasterFrames.TryReadSuperRun(order.Data, out var speed));
        Assert.Equal(3u, speed);
        var reply = Assert.Single(outbox.Messages);
        Assert.Equal(GameMasterFrames.SpeedOk, reply.Opcode);
        Assert.Empty(reply.Payload);
        var text = Assert.Single(outbox.Texts);
        Assert.Equal((GameMasterChat.SpeedSetText, "3"), (text.Name, Assert.Single(text.Subjects)));
    }

    [Fact]
    public void SuperRunOutsideOneToFourIsNotARequest()
    {
        var service = LoggedIn();

        Assert.Null(service.SuperRun(Gm, false, 0));
        Assert.Null(service.SuperRun(Gm, false, 5));
    }

    [Fact]
    public void ImmortalAndInvisibleOrderTheSwitchAndReply()
    {
        var service = LoggedIn();

        var immortal = service.Immortal(Gm, false, true);
        Assert.Equal(GameMasterFrames.OrderImmortal, Assert.Single(immortal.Orders).OrderType);
        Assert.Equal(new byte[] { 1 }, immortal.Orders[0].Data);
        Assert.Equal(GameMasterFrames.ImmortalOk, Assert.Single(immortal.Messages).Opcode);
        Assert.Equal(GameMasterChat.ImmortalOnText, Assert.Single(immortal.Texts).Name);

        var visible = service.Invisible(Gm, false, false);
        Assert.Equal(GameMasterFrames.OrderInvisible, Assert.Single(visible.Orders).OrderType);
        Assert.Equal(new byte[] { 0 }, visible.Orders[0].Data);
        Assert.Equal(GameMasterFrames.InvisibleOk, Assert.Single(visible.Messages).Opcode);
        Assert.Equal(GameMasterChat.InvisibleOffText, Assert.Single(visible.Texts).Name);
    }

    [Fact]
    public void LogoutClearsTheEagleAndEveryActiveSwitch()
    {
        var service = LoggedIn();
        service.SuperRun(Gm, false, 2);
        service.Immortal(Gm, false, true);
        service.Invisible(Gm, false, true);

        var orders = service.Logout(Gm).Orders;

        Assert.Equal(
            new[] { GameMasterFrames.OrderSuperRun, GameMasterFrames.OrderImmortal, GameMasterFrames.OrderInvisible, GameMasterFrames.OrderSetGmAttribute },
            orders.Select(order => order.OrderType));
        Assert.True(GameMasterFrames.TryReadSuperRun(orders[0].Data, out var speed));
        Assert.Equal(1u, speed);
        Assert.Equal(new byte[] { 0 }, orders[1].Data);
        Assert.Equal(new byte[] { 0 }, orders[2].Data);
        Assert.Equal(new byte[] { 0 }, orders[3].Data);
    }

    [Fact]
    public void LogoutOfAnIdleGmOnlyClearsTheEagle()
    {
        var order = Assert.Single(LoggedIn().Logout(Gm).Orders);

        Assert.Equal(GameMasterFrames.OrderSetGmAttribute, order.OrderType);
        Assert.Equal(new byte[] { 0 }, order.Data);
    }

    [Fact]
    public void ToolLogoutClearsTheSwitchesButNotTheEagle()
    {
        var service = LoggedIn(tool: true);
        service.Immortal(Gm, true, true);

        var order = Assert.Single(service.Logout(Gm, tool: true).Orders);

        Assert.Equal(GameMasterFrames.OrderImmortal, order.OrderType);
    }

    [Fact]
    public void EnteringAZoneRestoresTheActiveSwitches()
    {
        var service = LoggedIn();
        service.SuperRun(Gm, false, 4);
        service.Invisible(Gm, false, true);

        var types = service.ZoneEntered(Gm).Orders.Select(order => order.OrderType).ToArray();

        Assert.Equal(new[] { GameMasterFrames.OrderSetGmAttribute, GameMasterFrames.OrderSuperRun, GameMasterFrames.OrderInvisible }, types);
    }

    private static (GameMasterService Service, InMemoryFriendStore Friends) WithFriends()
    {
        var target = new Character { CharacterId = 0x4002, OwnerAccountId = 9, Slot = 0, Name = "Other", Species = Species.Buffalo, FType = FType.Power };
        var friends = new InMemoryFriendStore(id => id == target.CharacterId ? target : null, name => name == target.Name ? target : null);
        return (new GameMasterService(new FakeAuthority(), Text) { Friends = friends }, friends);
    }

    [Fact]
    public void MemoIsStoredAsAGmMessageAndConfirmedInChat()
    {
        var (service, friends) = WithFriends();

        var outbox = service.Memo("Other", "Hello");

        var text = Assert.Single(outbox.Texts);
        Assert.Equal(GameMasterChat.MemoSentText, text.Name);
        Assert.Equal(MemoSentSubjects, text.Subjects);
        var stored = Assert.Single(friends.TakeMessages(0x4002));
        Assert.Equal((FriendRules.GmSenderCharacterId, FriendRules.GmMessageKind, "Hello", FriendRules.GmSenderName), (stored.SenderCharacterId, stored.Kind, stored.Content, stored.SenderName));
    }

    [Fact]
    public void MemoRefusesEmptyOversizedAndUnknownTargets()
    {
        var (service, friends) = WithFriends();

        Assert.Equal(GameMasterChat.MemoEmptyText, Assert.Single(service.Memo("Other", string.Empty).Texts).Name);
        Assert.Equal(GameMasterChat.MemoTooLongText, Assert.Single(service.Memo("Other", new string('x', GameMasterFrames.MemoLength + 1)).Texts).Name);
        Assert.Empty(service.Memo("Gone", "Hello").Texts);
        Assert.Empty(friends.TakeMessages(0x4002));
    }

    [Fact]
    public void KickWithCauseSendsTheCauseSanctionsTheAccountAndWaitsForTheClient()
    {
        var (service, records) = WithBlockRecords();
        service.OpenSession(Gm);
        ulong? Online(string name) => name == "Other" ? 0x4002 : null;

        var outbox = service.KickWithCause(Gm, false, "Other", "spamming", Online);

        Assert.Empty(outbox.Kicks);
        var cause = Assert.Single(outbox.DirectMessages);
        Assert.Equal((0x4002ul, GameMasterFrames.ClientKickCause), (cause.To, cause.Opcode));
        var r = new PacketReader(cause.Payload);
        Assert.Equal("spamming", r.ReadNulTerminated(Text));
        var index = r.ReadUInt16();
        Assert.NotEqual(0, index);
        Assert.Equal(new[] { GameMasterFrames.KickOk, GameMasterFrames.AccountBlockOk }, outbox.Messages.Select(message => message.Opcode));
        Assert.Equal(GmBlockRules.SanctionedUser, records.UserAuthority["Other"]);
        var text = Assert.Single(outbox.Texts);
        Assert.Equal(GameMasterChat.KickReasonText, text.Name);
        Assert.Equal(KickReasonSubjects, text.Subjects);

        Assert.Null(service.KickCauseRead(0x4002, (ushort)(index + 1)));
        var read = service.KickCauseRead(0x4002, index)!.Value;
        Assert.Equal((Gm, false), (read.GameMaster, read.Tool));
        Assert.Equal(0x4002ul, Assert.Single(read.Outbox.Kicks));
        Assert.Equal(GameMasterFrames.KickOk, Assert.Single(read.Outbox.Messages).Opcode);
        var noCharacter = service.KickCauseRead(0, index)!.Value;
        Assert.Empty(noCharacter.Outbox.Kicks);
        Assert.Equal(GameMasterFrames.KickFail, Assert.Single(noCharacter.Outbox.Messages).Opcode);
    }

    [Fact]
    public void KickWithCauseRefusesALongReasonAndStillSanctionsAnOfflineTarget()
    {
        var (service, records) = WithBlockRecords();
        ulong? Online(string name) => name == "Other" ? 0x4002 : null;

        var tooLong = service.KickWithCause(Gm, false, "Other", new string('x', GameMasterFrames.MemoLength + 1), Online);
        Assert.Empty(tooLong.DirectMessages);
        Assert.Empty(tooLong.Messages);
        Assert.Equal(GameMasterChat.KickReasonTooLongText, Assert.Single(tooLong.Texts).Name);
        Assert.Empty(records.UserAuthority);

        var offline = service.KickWithCause(Gm, false, "Third", "spamming", Online);
        Assert.Empty(offline.DirectMessages);
        Assert.Equal(new[] { GameMasterFrames.KickFail, GameMasterFrames.AccountBlockOk }, offline.Messages.Select(message => message.Opcode));
        Assert.Equal(GmBlockRules.SanctionedUser, records.UserAuthority["Third"]);

        var unknown = service.KickWithCause(Gm, false, "Gone", "spamming", Online);
        Assert.Equal(new[] { GameMasterFrames.KickFail, GameMasterFrames.AccountBlockFail }, unknown.Messages.Select(message => message.Opcode));
    }

    private static readonly string[] AccountBlockOnArguments = { "on", "Other" };

    [Fact]
    public void TheAccountBlockToolCommandCarriesTheFlagAndTheName()
    {
        var request = GameMasterToolCommands.Build("account-block", AccountBlockOnArguments, Text)!;

        Assert.Equal(GameMasterFrames.AccountBlock, request.Opcode);
        Assert.Equal(GameMasterFrames.AccountBlockOn, request.Body[0]);
        Assert.Equal("Other", new PacketReader(request.Body.AsSpan(1)).ReadNulTerminated(Text));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(0xFFFC, 0xFFFD)]
    [InlineData(0xFFFD, 1)]
    public void GmIndexesCountUpAndWrapAfter0xFFFD(int current, int next)
    {
        Assert.Equal((ushort)next, GameMasterFrames.NextIndex((ushort)current));
    }

    [Fact]
    public void EachGmSessionGetsTheNextFreeIndex()
    {
        var (service, _) = WithBlockRecords();
        ulong? Online(string name) => name == "Other" ? 0x4002 : null;
        service.OpenSession(Gm);
        service.OpenSession(Gm, tool: true);

        static ushort IndexIn(GameMasterOutbox outbox) => new PacketReader(outbox.DirectMessages[0].Payload.AsSpan(outbox.DirectMessages[0].Payload.Length - sizeof(ushort))).ReadUInt16();

        Assert.Equal((ushort)1, IndexIn(service.KickWithCause(Gm, false, "Other", "a", Online)));
        Assert.Equal((ushort)2, IndexIn(service.KickWithCause(Gm, true, "Other", "a", Online)));
    }

    private static readonly string[] ChatBlockSubjects = { "Other", "30" };
    private static readonly string[] OtherSubject = { "Other" };
    private static readonly string[] NoReasonSubjects = { "Other", "2" };
    private static readonly string[] ReasonSubjects = { "Other", "2", "spam" };
    private static readonly string[] HelpNames = { "Help_Login", "Help_Logout" };
    private static readonly ushort[] ZoneCountZones = { 23, 23, 5, 1500 };
    private static readonly string[] NpcLines = { "Welcome", "Bye" };
    private static readonly string[] SummonNpcArguments = { "1001", "Welcome", "Bye" };
    private static readonly string[] CampIdArguments = { "0x4002" };
    private static readonly string[] ChatListNames = { "GM_GetAllUserChatBlock", GameMasterChatBlocks.ListLineText, GameMasterChatBlocks.ListCountText };
    private static readonly string[] ChatListLine = { "1", "Other", "16386", "30", "10:00:00" };

    [Fact]
    public void ChatBlockMinutesBlockReleaseAndPermanentlyBlock()
    {
        var (service, records) = WithBlockRecords();
        ulong? Online(string name) => name == "Other" ? 0x4002 : null;

        var timed = service.TimedBlockFromChat(Gm, GameMasterBlocks.Chat, "Other", 30, Online, "10:00:00");
        var timedText = Assert.Single(timed.Texts);
        Assert.Equal("GM_ChatBlock", timedText.Name);
        Assert.Equal(ChatBlockSubjects, timedText.Subjects);
        Assert.Equal((ushort)30, records.Rows[(0x4002, GameMasterBlocks.Chat)].Minutes);

        var released = service.TimedBlockFromChat(Gm, GameMasterBlocks.Chat, "Other", 0, Online, "10:00:00");
        Assert.Equal("GM_ReleaseChatBlock", Assert.Single(released.Texts).Name);

        var permanent = service.TimedBlockFromChat(Gm, GameMasterBlocks.Move, "Other", -1, Online, "10:00:00");
        var permanentText = Assert.Single(permanent.Texts);
        Assert.Equal("GM_PermBlockMove", permanentText.Name);
        Assert.Equal(OtherSubject, permanentText.Subjects);
        Assert.Equal((ushort)GameMasterChatBlocks.PermanentMinutes, records.Rows[(0x4002, GameMasterBlocks.Move)].Minutes);
    }

    [Fact]
    public void ChatNormalBlockTakesDaysAndAnOptionalReason()
    {
        var (service, records) = WithBlockRecords();
        service.OpenSession(Gm);
        ulong? Online(string name) => name == "Other" ? 0x4002 : null;

        var noReason = service.NormalBlockFromChat(Gm, "Other", 2, null, Online, "10:00:00");
        var noReasonText = Assert.Single(noReason.Texts);
        Assert.Equal("GM_BlockNoReason", noReasonText.Name);
        Assert.Equal(NoReasonSubjects, noReasonText.Subjects);
        Assert.Equal((ushort)(2 * GameMasterChatBlocks.MinutesPerDay), records.Rows[(0x4002, GameMasterBlocks.Normal)].Minutes);

        var withReason = service.NormalBlockFromChat(Gm, "Other", 2, "spam", Online, "10:00:00");
        Assert.Contains(withReason.DirectMessages, message => message.Opcode == GameMasterFrames.ClientCause);
        var reasonText = Assert.Single(withReason.Texts);
        Assert.Equal("GM_BlockReason", reasonText.Name);
        Assert.Equal(ReasonSubjects, reasonText.Subjects);

        Assert.Equal("GM_PermBlockNoReason", Assert.Single(service.NormalBlockFromChat(Gm, "Other", -1, null, Online, "10:00:00").Texts).Name);
        Assert.Equal("GM_ReleaseBlock", Assert.Single(service.NormalBlockFromChat(Gm, "Other", 0, null, Online, "10:00:00").Texts).Name);
    }

    [Fact]
    public void AChatBlockListHasAHeaderOneLinePerBlockAndTheCount()
    {
        var (service, _) = WithBlockRecords();
        ulong? Online(string name) => name == "Other" ? 0x4002 : null;
        service.TimedBlockFromChat(Gm, GameMasterBlocks.Chat, "Other", 30, Online, "10:00:00");

        var list = service.BlockListFromChat(GameMasterBlocks.Chat, "GM_GetAllUserChatBlock", id => id == 0x4002 ? "Other" : null);

        Assert.Equal(ChatListNames, list.Texts.Select(text => text.Name));
        Assert.Equal(ChatListLine, list.Texts[1].Subjects);
        Assert.Equal("1", Assert.Single(list.Texts[2].Subjects));
    }

    [Fact]
    public void CommandHelpRepliesWithEveryHelpLine()
    {
        var chat = new GameMasterChat(
            new Dictionary<string, string> { [GameMasterChat.CommandHelp] = "commandhelp", [GameMasterChat.ChatBlock] = "chatblock" },
            new Dictionary<string, string>(),
            new Dictionary<string, string> { ["Help_Login"] = "/Login:Password", ["Help_Logout"] = "/Logout" });

        Assert.Equal(GameMasterChat.CommandHelp, chat.Parse("/commandhelp")!.Name);
        Assert.Equal(GameMasterChat.ChatBlock, chat.Parse("/chatblock Other 30")!.Name);
        Assert.Equal(HelpNames, GameMasterService.CommandHelp(chat.HelpNames).Texts.Select(text => text.Name));
        Assert.Equal("/Login:Password", chat.Result("Help_Login"));
    }

    [Fact]
    public void ZoneCountsListEveryZoneWithCharacters()
    {
        var outbox = GameMasterService.ZoneCounts(Gm, ZoneCountZones);

        var reply = Assert.Single(outbox.Messages);
        Assert.Equal(GameMasterFrames.ZoneCountsReply, reply.Opcode);
        Assert.True(GameMasterFrames.TryReadZoneCounts(reply.Payload, out var zones));
        Assert.Equal(new (ushort, ushort)[] { (0, 1), (5, 1), (23, 2) }, zones);
    }

    [Fact]
    public void RemoveCampByIdWarnsWhenTheZoneIsUnknownAndStillOrders()
    {
        var unknown = GameMasterService.RemoveMyCampById(Gm, 0x4002, ownerZoneKnown: false);

        var warning = Assert.Single(unknown.Messages);
        Assert.Equal(GameMasterFrames.RemoveMyCampUnknownZone, warning.Opcode);
        Assert.Equal(new byte[] { 0, 0 }, warning.Payload);
        var order = Assert.Single(unknown.Orders);
        Assert.Equal((0x4002ul, GameMasterFrames.OrderRemoveMyCamp), (order.CharacterId, order.OrderType));
        Assert.Empty(GameMasterService.RemoveMyCampById(Gm, 0x4002, ownerZoneKnown: true).Messages);
    }

    [Fact]
    public void SummonNpcPassesTheRequestToTheGmsGameServer()
    {
        var request = GameMasterFrames.SummonNpcRequest(1001, NpcLines, Text);

        var order = Assert.Single(GameMasterService.SummonNpc(Gm, request, Text).Orders);

        Assert.Equal((Gm, GameMasterFrames.OrderSummonNpc), (order.CharacterId, order.OrderType));
        Assert.Equal(request, order.Data);
        Assert.True(GameMasterFrames.TryReadSummonNpc(order.Data, Text, out var chrType, out var lines));
        Assert.Equal(1001u, chrType);
        Assert.Equal(NpcLines, lines);
        Assert.Equal(GameMasterFrames.ReportSummonNpc, GameMasterFrames.ReportOf(GameMasterFrames.OrderSummonNpc));
        Assert.Equal(GameMasterFrames.SummonNpcOk, GameMasterService.Acknowledged(Gm, GameMasterFrames.ReportSummonNpc, new byte[4])!.Messages[0].Opcode);
        var refused = GameMasterService.Acknowledged(Gm, GameMasterFrames.ReportSummonNpc, new byte[] { 1, 0, 0, 0 })!.Messages[0];
        Assert.Equal(GameMasterFrames.SummonNpcFail, refused.Opcode);
        Assert.Empty(refused.Payload);
    }

    [Fact]
    public void TheNewToolCommandsBuildTheirRequests()
    {
        Assert.Equal(GameMasterFrames.ZoneCounts, GameMasterToolCommands.Build("zone-counts", Array.Empty<string>(), Text)!.Opcode);
        var camp = GameMasterToolCommands.Build("remove-camp-id", CampIdArguments, Text)!;
        Assert.True(GameMasterFrames.TryReadCharacterId(camp.Body, out var owner));
        Assert.Equal((GameMasterFrames.RemoveMyCampById, 0x4002ul), (camp.Opcode, owner));
        var npc = GameMasterToolCommands.Build("summon-npc", SummonNpcArguments, Text)!;
        Assert.Equal(GameMasterFrames.SummonNpc, npc.Opcode);
        Assert.Equal(GameMasterFrames.SummonNpcRequest(1001, NpcLines, Text), npc.Body);
    }

    [Fact]
    public void AccountBlockSetsOrClearsTheUserAuthority()
    {
        var (service, records) = WithBlockRecords();

        Assert.Equal(GameMasterFrames.AccountBlockOk, Assert.Single(service.AccountBlock(Gm, GameMasterFrames.AccountBlockOn, "Other").Messages).Opcode);
        Assert.Equal(GmBlockRules.SanctionedUser, records.UserAuthority["Other"]);
        Assert.Equal(GameMasterFrames.AccountBlockOk, Assert.Single(service.AccountBlock(Gm, 0, "Other").Messages).Opcode);
        Assert.Equal(GmBlockRules.NormalUser, records.UserAuthority["Other"]);
        Assert.Equal(GameMasterFrames.AccountBlockFail, Assert.Single(service.AccountBlock(Gm, GameMasterFrames.AccountBlockOn, "Gone").Messages).Opcode);
    }

    [Fact]
    public void PresentItemOrdersTheReceiversServer()
    {
        ulong? Online(string name) => name == "Other" ? 0x4002 : null;

        var outbox = GameMasterService.PresentItem("Other", 1001, 5, Online);

        Assert.NotNull(outbox);
        var order = Assert.Single(outbox!.Orders);
        Assert.Equal((0x4002ul, GameMasterFrames.OrderPresentItem), (order.CharacterId, order.OrderType));
        Assert.Equal(14, order.Data!.Length);
        Assert.True(GameMasterFrames.TryReadPresent(order.Data, out var chr, out var item, out var count));
        Assert.Equal((0x4002ul, 1001u, (ushort)5), (chr, item, count));
    }

    [Fact]
    public void PresentItemToAnOfflineCharacterOrdersNothingAndNonsenseIsNotARequest()
    {
        ulong? Online(string name) => name == "Other" ? 0x4002 : null;

        Assert.Empty(GameMasterService.PresentItem("Gone", 1001, 1, Online)!.Orders);
        Assert.Null(GameMasterService.PresentItem("Other", 0, 1, Online));
        Assert.Null(GameMasterService.PresentItem("Other", 1001, 0, Online));
    }

    [Fact]
    public void OxQuestionAndAnswerAreOrdersOnTheGmsServer()
    {
        var service = new GameMasterService(new FakeAuthority(), Text);

        var question = Assert.Single(service.OxQuestion(Gm, "Is the sky blue?").Orders);
        Assert.Equal((Gm, GameMasterFrames.OrderOxQuestion), (question.CharacterId, question.OrderType));
        Assert.Equal("Is the sky blue?", GameMasterFrames.ReadQuestion(question.Data, Text));

        var answer = Assert.Single(GameMasterService.OxAnswer(Gm, 1).Orders);
        Assert.Equal((Gm, GameMasterFrames.OrderOxAnswer), (answer.CharacterId, answer.OrderType));
        Assert.Equal(new byte[] { 1 }, answer.Data);
    }

    [Fact]
    public void SummonMonsterOrdersTheGmsServerWithinTheMonsterRanges()
    {
        var order = Assert.Single(GameMasterService.SummonMonster(Gm, 2001, 3)!.Orders);
        Assert.Equal((Gm, GameMasterFrames.OrderSummonMonster), (order.CharacterId, order.OrderType));
        Assert.True(GameMasterFrames.TryReadSummon(order.Data, out var type, out var zone, out var count));
        Assert.Equal((2001u, 0u, (byte)3), (type, zone, count));

        Assert.Empty(GameMasterService.SummonMonster(Gm, 1999, 1)!.Orders);
        Assert.Empty(GameMasterService.SummonMonster(Gm, 3435, 1)!.Orders);
        Assert.Null(GameMasterService.SummonMonster(Gm, 2001, 0));
        Assert.Empty(GameMasterService.SummonMonster(Gm, 16998, 1)!.Orders);
    }

    [Fact]
    public void SetMusicSendsASixtyFourByteNameAndRemoveObjectCarriesTheId()
    {
        var service = new GameMasterService(new FakeAuthority(), Text);

        var music = Assert.Single(service.SetMusic(Gm, "town")!.Orders);
        Assert.Equal((Gm, GameMasterFrames.OrderSetMusic), (music.CharacterId, music.OrderType));
        Assert.Equal(GameMasterFrames.MusicNameLength, music.Data!.Length);
        Assert.Equal("town", GameMasterFrames.ReadMusicName(music.Data, Text));
        Assert.Null(service.SetMusic(Gm, string.Empty));

        var removal = Assert.Single(GameMasterService.RemoveObject(Gm, 0x1001).Orders);
        Assert.Equal((Gm, GameMasterFrames.OrderRemoveObject), (removal.CharacterId, removal.OrderType));
        Assert.True(GameMasterFrames.TryReadObjectId(removal.Data, out var id));
        Assert.Equal((ushort)0x1001, id);
    }

    [Fact]
    public void NpcOnOffKeepsTheHiddenListAndAnswersWithTheNpcType()
    {
        var service = new GameMasterService(new FakeAuthority(), Text);

        var hide = Assert.Single(GameMasterService.NpcDeactivate(Gm, 0x1100).Orders);
        Assert.Equal((Gm, GameMasterFrames.OrderNpcOnOff), (hide.CharacterId, hide.OrderType));
        Assert.True(GameMasterFrames.TryReadNpcOnOff(hide.Data, out var on, out var hiddenId));
        Assert.Equal((false, (ushort)0x1100), (on, hiddenId));
        Assert.Equal(GameMasterFrames.ReportNpcOnOff, GameMasterFrames.ReportOf(GameMasterFrames.OrderNpcOnOff));

        var hidden = service.NpcOnOffReported(Gm, GameMasterFrames.NpcOnOffAckBody(new(false, 0, 0x1100, 23, 3050)))!;
        var hiddenReply = Assert.Single(hidden.Messages);
        Assert.Equal(GameMasterFrames.NpcDeactivated, hiddenReply.Opcode);
        Assert.True(GameMasterFrames.TryReadAckResult(hiddenReply.Payload, out var hiddenType));
        Assert.Equal(3050u, hiddenType);
        service.NpcOnOffReported(Gm, GameMasterFrames.NpcOnOffAckBody(new(false, 0, 0x1000, 23, 3040)));
        service.NpcOnOffReported(Gm, GameMasterFrames.NpcOnOffAckBody(new(false, 0, 0x1200, 5, 3060)));

        var list = Assert.Single(service.NpcList(Gm).Messages);
        Assert.Equal(GameMasterFrames.NpcListReply, list.Opcode);
        Assert.True(GameMasterFrames.TryReadNpcList(list.Payload, out var npcs));
        Assert.Equal(new (ushort, uint, uint)[] { (0, 5, 3060), (1, 23, 3040), (2, 23, 3050) }, npcs.ToArray());

        var show = Assert.Single(service.NpcActivate(Gm, 1).ZoneOrders);
        Assert.Equal((GameMasterFrames.OrderNpcOnOff, (ushort)23), (show.OrderType, show.Zone));
        Assert.True(GameMasterFrames.TryReadNpcOnOff(show.Data, out var shownOn, out var shownId));
        Assert.Equal((true, (ushort)0x1000), (shownOn, shownId));

        var shown = Assert.Single(service.NpcOnOffReported(Gm, GameMasterFrames.NpcOnOffAckBody(new(true, 0, 0x1000, 23, 3040)))!.Messages);
        Assert.Equal(GameMasterFrames.NpcActivated, shown.Opcode);
        Assert.True(GameMasterFrames.TryReadNpcList(Assert.Single(service.NpcList(Gm).Messages).Payload, out var remaining));
        Assert.Equal(2, remaining.Count);

        var badIndex = Assert.Single(service.NpcActivate(Gm, 9).Messages);
        Assert.Equal(GameMasterFrames.NpcActivateFail, badIndex.Opcode);
        Assert.Equal(new byte[] { 1, 9, 0 }, badIndex.Payload);

        var missing = Assert.Single(service.NpcOnOffReported(Gm, GameMasterFrames.NpcOnOffAckBody(new(true, 1, 0x1300, 23, 0)))!.Messages);
        Assert.Equal(GameMasterFrames.NpcActivateFail, missing.Opcode);
        Assert.Equal(new byte[] { 0, 0x00, 0x13 }, missing.Payload);
        var missingHide = Assert.Single(service.NpcOnOffReported(Gm, GameMasterFrames.NpcOnOffAckBody(new(false, 1, 0x1300, 23, 0)))!.Messages);
        Assert.Equal(GameMasterFrames.NpcDeactivateFail, missingHide.Opcode);
        Assert.Equal(new byte[] { 0x00, 0x13 }, missingHide.Payload);
    }

    [Fact]
    public void NpcRefreshOrdersTheZoneAndRepliesWithTheZone()
    {
        var refresh = Assert.Single(GameMasterService.NpcRefresh(Gm, 23).ZoneOrders);
        Assert.Equal((GameMasterFrames.OrderNpcRefresh, (ushort)23), (refresh.OrderType, refresh.Zone));
        Assert.Empty(refresh.Data);
        Assert.Equal(GameMasterFrames.ReportNpcRefresh, GameMasterFrames.ReportOf(GameMasterFrames.OrderNpcRefresh));

        var done = Assert.Single(GameMasterService.NpcRefreshReported(Gm, 23, GameMasterFrames.AckResult(0))!.Messages);
        Assert.Equal(GameMasterFrames.NpcRefreshed, done.Opcode);
        Assert.Equal(new byte[] { 23, 0, 0, 0 }, done.Payload);
        var failed = Assert.Single(GameMasterService.NpcRefreshReported(Gm, 23, GameMasterFrames.AckResult(1))!.Messages);
        Assert.Equal(GameMasterFrames.NpcRefreshFail, failed.Opcode);
        Assert.Equal(GameMasterFrames.NpcRefreshFail, Assert.Single(GameMasterService.NpcRefresh(Gm, 70000).Messages).Opcode);
    }

    [Fact]
    public void NpcAcknowledgementIsFifteenBytes()
    {
        var body = GameMasterFrames.NpcOnOffAckBody(new(true, 0, 0x1000, 23, 3040));
        Assert.Equal(GameMasterFrames.NpcOnOffAckLength, body.Length);
        Assert.Equal(new byte[] { 1, 0, 0, 0, 0, 0x00, 0x10, 23, 0, 0, 0, 0xE0, 0x0B, 0, 0 }, body);
    }

    [Fact]
    public void RemovingAShopOrCampOrdersTheOwnersServerAndNamesTheOwner()
    {
        ulong? Online(string name) => name == "Other" ? 0x4002 : null;

        var shop = GameMasterService.RemovePersonalShop("Other", Online)!;
        var shopOrder = Assert.Single(shop.Orders);
        Assert.Equal((0x4002ul, GameMasterFrames.OrderRemovePersonalShop), (shopOrder.CharacterId, shopOrder.OrderType));
        Assert.True(GameMasterFrames.TryReadCharacterId(shopOrder.Data, out var shopOwner));
        Assert.Equal(0x4002ul, shopOwner);
        Assert.Equal(GameMasterChat.RemovePersonalShopText, Assert.Single(shop.Texts).Name);

        var camp = GameMasterService.RemoveMyCamp("Other", Online)!;
        Assert.Equal(GameMasterFrames.OrderRemoveMyCamp, Assert.Single(camp.Orders).OrderType);

        var gone = GameMasterService.RemoveMyCamp("Gone", Online)!;
        Assert.Empty(gone.Orders);
        Assert.Equal(GameMasterChat.NotOnlineText, Assert.Single(gone.Texts).Name);
        Assert.Null(GameMasterService.RemoveMyCamp(string.Empty, Online));
    }

    [Fact]
    public void ToolCommandsBuildTheWireBodiesOfTheWorldCommands()
    {
        var summon = GameMasterToolCommands.Build("summon-monster", SummonArguments, Text)!;
        Assert.Equal(GameMasterFrames.SummonMonster, summon.Opcode);
        Assert.True(GameMasterFrames.TryReadSummon(summon.Body, out var type, out var zone, out var count));
        Assert.Equal((2001u, 0u, (byte)3), (type, zone, count));

        var music = GameMasterToolCommands.Build("set-music", MusicArguments, Text)!;
        Assert.Equal(GameMasterFrames.SetMusic, music.Opcode);
        Assert.Equal("town", GameMasterFrames.ReadMusicName(music.Body, Text));

        var gift = GameMasterToolCommands.Build("ox-give-item", OxGiftArguments, Text)!;
        Assert.Equal(GameMasterFrames.OxGiveItem, gift.Opcode);
        Assert.True(GameMasterFrames.TryReadOxGift(gift.Body, out var giftItem, out var giftCount));
        Assert.Equal((1001u, (byte)2), (giftItem, giftCount));
        Assert.Equal(GameMasterFrames.OrderOxGiveItem, Assert.Single(GameMasterService.OxGiveItem(Gm, giftItem, giftCount).Orders).OrderType);

        var issue = GameMasterToolCommands.Build("issue-item", IssueArguments, Text)!;
        Assert.Equal(GameMasterFrames.SetIssueItem, issue.Opcode);
        Assert.True(GameMasterFrames.TryReadIssueItem(issue.Body, out var issuer, out var issuedItem, out var issuedCount));
        Assert.Equal(((ushort)4100, 1001u, (ushort)10), (issuer, issuedItem, issuedCount));
        Assert.Equal(GameMasterFrames.OrderSetIssueItem, Assert.Single(GameMasterService.SetIssueItem(Gm, issuer, issuedItem, issuedCount).Orders).OrderType);

        Assert.Equal(GameMasterFrames.RemoveMonsters, GameMasterToolCommands.Build("remove-monsters", Array.Empty<string>(), Text)!.Opcode);
        Assert.Null(GameMasterToolCommands.Build("remove-object", ZeroObjectArguments, Text));
        Assert.Equal(GameMasterFrames.RemoveObject, GameMasterToolCommands.Build("remove-object", ObjectArguments, Text)!.Opcode);
        Assert.Equal(GameMasterFrames.RemovePersonalShop, GameMasterToolCommands.Build("remove-shop", OtherArguments, Text)!.Opcode);
        Assert.Equal(GameMasterFrames.RemoveMyCampByName, GameMasterToolCommands.Build("remove-camp", OtherArguments, Text)!.Opcode);
    }

    [Fact]
    public void ToolCommandsBuildTheGuildGameFlags()
    {
        Assert.Equal(GameMasterFrames.GuildGameFlag, GameMasterToolCommands.Build("guild-games", Array.Empty<string>(), Text)!.Opcode);
        var closed = GameMasterToolCommands.Build("guild-games", GuildGamesOffArguments, Text)!;
        Assert.Equal((GameMasterFrames.SetGuildGameFlag, (byte)0), (closed.Opcode, Assert.Single(closed.Body)));
        var read = GameMasterToolCommands.Build("guild-room-flag", RoomFlagReadArguments, Text)!;
        Assert.Equal(GameMasterFrames.GuildRoomFlag, read.Opcode);
        Assert.Equal(new byte[] { 120, 0, 0, 0 }, read.Body);
        var set = GameMasterToolCommands.Build("guild-room-flag", RoomFlagSetArguments, Text)!;
        Assert.Equal(GameMasterFrames.SetGuildRoomFlag, set.Opcode);
        Assert.Equal(new byte[] { 120, 0, 0, 0, 3 }, set.Body);
        Assert.Equal("guild game rooms closed (flag 0)", GameMasterToolConnection.Describe(new ToolReply(GameMasterFrames.GuildGameFlagReply, new byte[] { 0 })));
        Assert.Equal("room flag 3", GameMasterToolConnection.Describe(new ToolReply(GameMasterFrames.GuildRoomFlagReply, new byte[] { 3 })));
    }

    private static readonly string[] GuildGamesOffArguments = { "off" };
    private static readonly string[] RoomFlagReadArguments = { "120" };
    private static readonly string[] RoomFlagSetArguments = { "120", "3" };

    [Fact]
    public void BlocksAreSavedListedAndTheTargetIsTold()
    {
        var (service, records) = WithBlockRecords();
        ulong? Online(string name) => name == "Other" ? 0x4002 : null;
        string? OnlineName(ulong id) => id == 0x4002 ? "Other" : null;

        var blocked = service.Block(Gm, GameMasterBlocks.Chat, 30, "Other", Online, "10:00:00");
        Assert.Equal(GameMasterFrames.BlockOk, Assert.Single(blocked.Messages).Opcode);
        var told = Assert.Single(blocked.DirectMessages);
        Assert.Equal((0x4002ul, GameMasterFrames.ClientBlocked), (told.To, told.Opcode));
        Assert.Equal(new byte[] { 2, 0, 30, 0, 0, 0 }, told.Payload);
        Assert.Equal((1, (ushort)30, Gm), records.Rows[(0x4002, GameMasterBlocks.Chat)]);

        Assert.Equal(GameMasterFrames.BlockFail, Assert.Single(service.Block(Gm, 4, 30, "Other", Online, "10:00:00").Messages).Opcode);

        var list = Assert.Single(service.BlockList(Gm, GameMasterBlocks.Chat, OnlineName)!.Messages);
        Assert.True(GameMasterFrames.TryReadBlockList(list.Payload, Text, false, out var entries));
        Assert.Equal(new GameMasterBlockEntry(0, 0x4002, "Other", 30, "10:00:00"), Assert.Single(entries));
        var all = Assert.Single(service.BlockListAll(Gm, OnlineName).Messages);
        Assert.True(GameMasterFrames.TryReadBlockList(all.Payload, Text, true, out var allEntries));
        Assert.Equal(GameMasterBlocks.Chat, Assert.Single(allEntries).Type);

        var lifted = service.Unblock(Gm, GameMasterBlocks.Chat, "Other", Online);
        Assert.Equal(GameMasterFrames.UnblockOk, Assert.Single(lifted.Messages).Opcode);
        Assert.Equal(GameMasterFrames.ClientUnblocked, Assert.Single(lifted.DirectMessages).Opcode);
        Assert.Empty(service.Blocks.OfType(GameMasterBlocks.Chat));
        Assert.Equal(GameMasterFrames.UnblockFail, Assert.Single(service.Unblock(Gm, GameMasterBlocks.Chat, "Other", Online).Messages).Opcode);

        service.Block(Gm, GameMasterBlocks.Trade, 5, "Other", Online, "10:00:00");
        Assert.True(service.BlockReleased(0x4002, GameMasterBlocks.Trade));
        Assert.False(service.BlockReleased(0x4002, GameMasterBlocks.Trade));
    }

    [Fact]
    public void OfflineBlocksGoToTheDatabaseExceptTheNormalType()
    {
        var (service, records) = WithBlockRecords();
        ulong? Nobody(string name) => null;

        var away = service.Block(Gm, GameMasterBlocks.Trade, 60, "Away", Nobody, "10:00:00");
        Assert.Equal(GameMasterFrames.BlockOk, Assert.Single(away.Messages).Opcode);
        Assert.True(records.Rows.ContainsKey((0x4003, GameMasterBlocks.Trade)));
        Assert.Equal("Away", Assert.Single(service.Blocks.OfType(GameMasterBlocks.Trade)).Name);

        var all = Assert.Single(service.BlockListAll(Gm, _ => null).Messages);
        Assert.True(GameMasterFrames.TryReadBlockList(all.Payload, Text, true, out var entries));
        Assert.Equal("Away", Assert.Single(entries).Name);

        Assert.Empty(service.Block(Gm, GameMasterBlocks.Normal, 60, "Away", Nobody, "10:00:00").Messages);
        Assert.Empty(service.Block(Gm, GameMasterBlocks.Chat, 60, "Unknown", Nobody, "10:00:00").Messages);

        var lost = service.Block(Gm, GameMasterBlocks.Chat, 60, "Lost", Nobody, "10:00:00");
        Assert.Equal(GameMasterFrames.BlockFail, Assert.Single(lost.Messages).Opcode);
        Assert.Empty(service.Blocks.OfType(GameMasterBlocks.Chat));

        Assert.Empty(service.Unblock(Gm, GameMasterBlocks.Normal, "Away", Nobody).Messages);
        Assert.Equal(GameMasterFrames.UnblockOk, Assert.Single(service.Unblock(Gm, GameMasterBlocks.Trade, "Away", Nobody).Messages).Opcode);
        Assert.Empty(service.Blocks.OfType(GameMasterBlocks.Trade));
    }

    [Fact]
    public void BlockWithCauseTellsTheCauseAndKicksTheReader()
    {
        var (service, records) = WithBlockRecords();
        service.OpenSession(Gm, tool: true);
        ulong? Online(string name) => name == "Other" ? 0x4002 : null;

        var refused = Assert.Single(service.BlockWithCause(Gm, true, "Gone", 60, "spam", Online, "10:00:00").Messages);
        Assert.Equal(GameMasterFrames.BlockWithCauseFail, refused.Opcode);
        Assert.Equal(new byte[] { 1, 0 }, refused.Payload);

        var blocked = service.BlockWithCause(Gm, true, "Other", 60, "spam", Online, "10:00:00");
        var cause = blocked.DirectMessages[0];
        Assert.Equal((0x4002ul, GameMasterFrames.ClientCause), (cause.To, cause.Opcode));
        var r = new PacketReader(cause.Payload);
        Assert.Equal("spam", r.ReadNulTerminated(Text));
        var index = r.ReadUInt16();
        Assert.NotEqual(0, index);
        Assert.Equal(GameMasterFrames.BlockOk, Assert.Single(blocked.Messages).Opcode);
        Assert.Equal(GameMasterFrames.ClientBlocked, blocked.DirectMessages[1].Opcode);
        Assert.True(records.Rows.ContainsKey((0x4002, GameMasterBlocks.Normal)));

        Assert.Null(service.CauseRead(0x4002, (ushort)(index + 1)));
        var read = service.CauseRead(0x4002, index)!.Value;
        Assert.Equal((Gm, true), (read.GameMaster, read.Tool));
        Assert.Equal(0x4002ul, Assert.Single(read.Outbox.Kicks));
        Assert.Equal(GameMasterFrames.BlockWithCauseOk, Assert.Single(read.Outbox.Messages).Opcode);
        var noCharacter = service.CauseRead(0, index)!.Value;
        Assert.Empty(noCharacter.Outbox.Kicks);
        Assert.Equal(GameMasterFrames.BlockWithCauseFail, Assert.Single(noCharacter.Outbox.Messages).Opcode);
    }

    [Fact]
    public void ClientReportedBlocksAreListedWithTheCurrentName()
    {
        var (service, _) = WithBlockRecords();
        Assert.True(GameMasterFrames.TryReadClientBlockList(new byte[] { 2, 0, 2, 0, 4, 0 }, out var types));
        Assert.Equal(1, service.ClientBlocks(0x4002, types, "10:00:00"));
        var all = Assert.Single(service.BlockListAll(Gm, id => id == 0x4002 ? "Other" : null).Messages);
        Assert.True(GameMasterFrames.TryReadBlockList(all.Payload, Text, true, out var entries));
        Assert.Equal(new GameMasterBlockEntry(GameMasterBlocks.Chat, 0x4002, "Other", GameMasterBlocks.UnknownMinutes, "10:00:00"), Assert.Single(entries));
        Assert.False(GameMasterFrames.TryReadClientBlockList(new byte[] { 2, 0, 2, 0 }, out _));
    }

    [Fact]
    public void BlockToolCommandsBuildTheirBodies()
    {
        var block = GameMasterToolCommands.Build("block", BlockArguments, Text)!;
        Assert.Equal(GameMasterFrames.Block, block.Opcode);
        Assert.True(GameMasterFrames.TryReadBlock(block.Body, Text, out var type, out var minutes, out var name));
        Assert.Equal(((ushort)2, 30u, "Other"), (type, minutes, name));
        var unblock = GameMasterToolCommands.Build("unblock", UnblockArguments, Text)!;
        Assert.True(GameMasterFrames.TryReadUnblock(unblock.Body, Text, out var unblockType, out var unblockName));
        Assert.Equal(((ushort)2, "Other"), (unblockType, unblockName));
        Assert.Equal(GameMasterFrames.BlockListAll, GameMasterToolCommands.Build("block-list", Array.Empty<string>(), Text)!.Opcode);
    }

    [Fact]
    public void GuildToolsAnswerForKnownAndUnknownGuilds()
    {
        var (service, friends) = WithGuild();
        string? OnlineName(ulong id) => id switch { 0x5001 => "a", 0x5002 => "b", _ => null };

        Assert.Equal(GameMasterFrames.GuildInfoMissing, Assert.Single(service.GuildInfo(Gm, "Nobody", OnlineName).Messages).Opcode);
        var info = Assert.Single(service.GuildInfo(Gm, "Wolves", OnlineName).Messages);
        Assert.Equal(GameMasterFrames.GuildInfoReply, info.Opcode);
        var r = new tPoseidon.Core.Net.PacketReader(info.Payload);
        r.ReadUInt32();
        Assert.Equal("Wolves", r.ReadNulTerminated(Text));
        Assert.Equal(3, r.ReadByte());
        Assert.Equal("a", r.ReadNulTerminated(Text));
        r.ReadNulTerminated(Text);
        Assert.Equal(1, r.ReadByte());
        Assert.Equal(1, r.ReadByte());
        Assert.Equal(2, r.ReadByte());

        Assert.Equal(GameMasterFrames.GuildMemoFail, Assert.Single(service.GuildGradeMemo(Gm, 0, "Nobody", "Hi").Messages).Opcode);
        service.GuildGradeMemo(Gm, 0, "Wolves", "Hi");
        Assert.Single(friends.TakeMessages(0x5001));

        var blocked = service.GuildBlock(Gm, GameMasterBlocks.Chat, 10, "Wolves", string.Empty, OnlineName, "10:00:00");
        Assert.Equal(GameMasterFrames.GuildBlockOk, blocked.Messages[0].Opcode);
        Assert.Equal(2, blocked.Messages.Count(message => message.Opcode == GameMasterFrames.BlockOk));
        Assert.Equal(2, blocked.DirectMessages.Count);
        Assert.Equal(2, service.Blocks.OfType(GameMasterBlocks.Chat).Count);
        var unknown = Assert.Single(service.GuildBlock(Gm, GameMasterBlocks.Chat, 10, "Nobody", string.Empty, OnlineName, "10:00:00").Messages);
        Assert.Equal((GameMasterFrames.GuildBlockFail, GameMasterFrames.GuildBlockUnknownGuild), (unknown.Opcode, unknown.Payload[0]));
        var nobodyOnline = Assert.Single(service.GuildBlock(Gm, GameMasterBlocks.Chat, 10, "Wolves", string.Empty, _ => null, "10:00:00").Messages);
        Assert.Equal(GameMasterFrames.GuildBlockNotLoaded, nobodyOnline.Payload[0]);
    }

    private static (GameMasterService Service, InMemoryFriendStore Friends) WithGuild()
    {
        var friends = new InMemoryFriendStore(_ => null, _ => null);
        var guilds = new InMemoryGuildStore();
        var guild = guilds.CreateGuild("Wolves", 9, 0x5001);
        guilds.AddMember(guild.GuildCode, 0x5002, "b");
        guilds.AddMember(guild.GuildCode, 0x5003, "c");
        var records = new FakeBlockStore();
        records.Characters.UnionWith(OnlineGuildMembers);
        return (new GameMasterService(new FakeAuthority(), Text) { Friends = friends, Guilds = guilds, BlockRecords = records }, friends);
    }

    [Fact]
    public void GuildToZoneOrdersEveryOnlineMember()
    {
        var (service, _) = WithGuild();

        var outbox = service.GuildToZone("Wolves", 23, id => id != 0x5003);

        Assert.Equal(OnlineGuildMembers, outbox.Orders.Select(order => order.CharacterId).Order());
        Assert.All(outbox.Orders, order => Assert.Equal(GameMasterFrames.OrderTeleportToZone, order.OrderType));
        Assert.Empty(service.GuildToZone("Nobody", 23, _ => true).Orders);
        Assert.Empty(service.GuildToZone("Wolves", GameMasterFrames.ZoneLimit, _ => true).Orders);
    }

    [Fact]
    public void GuildToChrOrdersTheOtherMembersOnTheAnchorsServer()
    {
        var (service, _) = WithGuild();
        ulong? Online(string name) => name == "Boss" ? 0x5001 : null;

        var outbox = service.GuildToChr("Wolves", "Boss", Online, _ => true);

        Assert.Equal(2, outbox.Orders.Count);
        Assert.All(outbox.Orders, order => Assert.Equal((0x5001ul, GameMasterFrames.OrderTeleportToChr), (order.CharacterId, order.OrderType)));
        Assert.True(GameMasterFrames.TryReadChrToChr(outbox.Orders[0].Data, out var who, out var to));
        Assert.NotEqual(0x5001ul, who);
        Assert.Equal(0x5001ul, to);
        Assert.Empty(service.GuildToChr("Wolves", "Gone", Online, _ => true).Orders);
    }

    [Fact]
    public void GuildMemosReachTheMasterOrEveryMaster()
    {
        var (service, friends) = WithGuild();

        service.GuildMemoToMaster("Wolves", "Hello");
        service.GuildMemoToAllMasters("Again");

        Assert.Equal(GuildMemoTexts, friends.TakeMessages(0x5001).Select(message => message.Content).Order(StringComparer.Ordinal));
        Assert.Empty(friends.TakeMessages(0x5002));
    }

    [Fact]
    public void LogoutAndSessionOpenEndTheGmLogin()
    {
        var service = new GameMasterService(new FakeAuthority(), Text);
        service.Login(Gm, GmName, WithPassword(Password), Loopback);

        service.OpenSession(Gm);
        Assert.False(service.IsLoggedIn(Gm));

        service.Logout(Gm);
        var again = service.Login(Gm, GmName, WithPassword(Password), Loopback);
        Assert.True(service.IsLoggedIn(Gm));
        Assert.NotNull(again);
        Assert.Single(again.Orders);
    }

    [Fact]
    public void CharCountRepliesWithTheZoneAndTheNumberOfCharacters()
    {
        var outbox = GameMasterService.CharCount(Gm, 23, zone => zone == 23 ? 5 : 0);

        var reply = Assert.Single(outbox.Messages);
        Assert.Equal(Gm, reply.To);
        Assert.Equal(GameMasterFrames.CharCountReply, reply.Opcode);
        Assert.Equal(new byte[] { 23, 0, 5, 0, 0, 0 }, reply.Payload);
    }

    [Fact]
    public void CharInfoOrdersTheCharacterServerAndAnswersMissingForOfflineNames()
    {
        var found = GameMasterService.CharInfo(Gm, "Other", name => name == "Other" ? 0x5001UL : null);
        var order = Assert.Single(found.Orders);
        Assert.Equal(0x5001UL, order.CharacterId);
        Assert.Equal(GameMasterFrames.OrderCharInfo, order.OrderType);
        Assert.True(GameMasterFrames.TryReadCharacterId(order.Data!, out var target));
        Assert.Equal(0x5001UL, target);

        var missing = GameMasterService.CharInfo(Gm, "Nobody", _ => null);
        Assert.Empty(missing.Orders);
        var reply = Assert.Single(missing.Messages);
        Assert.Equal(GameMasterFrames.CharInfoMissing, reply.Opcode);
        Assert.Empty(reply.Payload);
    }

    [Fact]
    public void CharInfoReportBecomesTheReplyWithTheAccountAndTheFields()
    {
        var service = new GameMasterService(new FakeAuthority(), Text);
        var report = GameMasterFrames.CharInfoReport(new GameMasterFrames.CharacterInfo("Other", 0x5001, 23, 40, 1234, false, false), Text);
        Assert.Equal(GameMasterFrames.CharInfoReportLength, report.Length);

        var outbox = service.CharInfoReported(Gm, report, "acct");

        var reply = Assert.Single(outbox!.Messages);
        Assert.Equal(GameMasterFrames.CharInfoReply, reply.Opcode);
        var expected = new List<byte>();
        expected.AddRange(Text.GetBytes("acct"));
        expected.Add(0);
        expected.AddRange(Text.GetBytes("Other"));
        expected.Add(0);
        expected.AddRange(BitConverter.GetBytes(0x5001UL));
        expected.AddRange(BitConverter.GetBytes((ushort)23));
        expected.AddRange(BitConverter.GetBytes(40U));
        expected.AddRange(BitConverter.GetBytes(1234U));
        expected.Add(0);
        Assert.Equal(expected.ToArray(), reply.Payload);
    }

    [Fact]
    public void CharInfoReportOfAMissingCharacterBecomesTheMissingReply()
    {
        var service = new GameMasterService(new FakeAuthority(), Text);
        var report = GameMasterFrames.CharInfoReport(new GameMasterFrames.CharacterInfo(string.Empty, 0x5001, 0, 0, 0, true, false), Text);

        var outbox = service.CharInfoReported(Gm, report, string.Empty);

        Assert.Equal(GameMasterFrames.CharInfoMissing, Assert.Single(outbox!.Messages).Opcode);
        Assert.Null(service.CharInfoReported(Gm, new byte[3], string.Empty));
    }

    [Fact]
    public void SuperNoticeGoesToTheChatLobbyWithZeroedIdsAndTheColourLast()
    {
        var service = new GameMasterService(new FakeAuthority(), Text);

        var outbox = service.SuperNotice(5, "Hi");

        var broadcast = Assert.Single(outbox!.LobbyBroadcasts);
        Assert.Equal(GameMasterFrames.SuperNoticeShown, broadcast.Opcode);
        Assert.Equal(new byte[] { 0, 0, 0, 0, (byte)'H', (byte)'i', 0, 5, 0 }, broadcast.Payload);
        Assert.Null(service.SuperNotice(5, string.Empty));
    }

    [Fact]
    public void SuperNoticeRequestRoundTripsThroughTheToolCommand()
    {
        var request = GameMasterToolCommands.Build("super-notice", NoticeArguments, Text)!;

        Assert.Equal(GameMasterFrames.SuperNotice, request.Opcode);
        Assert.True(GameMasterFrames.TryReadSuperNotice(request.Body, Text, out var colour, out var notice));
        Assert.Equal((ushort)7, colour);
        Assert.Equal("Hello", notice);
        Assert.Null(GameMasterToolCommands.Build("super-notice", BadNoticeArguments, Text));
    }

    [Fact]
    public void EveryGameMasterOrderTheServicesProduceIsDeliveredToTheGameServer()
    {
        var service = new GameMasterService(new FakeAuthority(), Text);
        service.Login(Gm, GmName, WithPassword(Password), Loopback);
        var produced = new List<ushort>();
        produced.AddRange(service.SuperRun(Gm, false, 3)!.Orders.Select(order => order.OrderType));
        produced.AddRange(service.Immortal(Gm, false, true)!.Orders.Select(order => order.OrderType));
        produced.AddRange(service.Invisible(Gm, false, true)!.Orders.Select(order => order.OrderType));
        produced.AddRange(GameMasterService.PresentItem("Other", 1001, 1, _ => 0x5001UL)!.Orders.Select(order => order.OrderType));
        produced.AddRange(GameMasterService.SummonMonster(Gm, 2001, 1)!.Orders.Select(order => order.OrderType));
        produced.AddRange(service.SetMusic(Gm, "town")!.Orders.Select(order => order.OrderType));
        produced.AddRange(GameMasterService.RemoveObject(Gm, 0).Orders.Select(order => order.OrderType));
        produced.AddRange(GameMasterService.RemovePersonalShop("Other", _ => 0x5001UL)!.Orders.Select(order => order.OrderType));
        produced.AddRange(GameMasterService.RemoveMyCamp("Other", _ => 0x5001UL)!.Orders.Select(order => order.OrderType));
        produced.AddRange(service.OxQuestion(Gm, "Q").Orders.Select(order => order.OrderType));
        produced.AddRange(GameMasterService.OxAnswer(Gm, 1)!.Orders.Select(order => order.OrderType));
        produced.AddRange(GameMasterService.CharInfo(Gm, "Other", _ => 0x5001UL).Orders.Select(order => order.OrderType));

        Assert.Equal(12, produced.Count);
        Assert.All(produced, orderType => Assert.True(GameMasterFrames.IsGameServerOrder(orderType), $"order 0x{orderType:X2}"));
        Assert.False(GameMasterFrames.IsGameServerOrder(0x0D));
    }

    [Fact]
    public void ARefusedMonsterTypeAnswersWithReasonOne()
    {
        var reply = Assert.Single(GameMasterService.SummonMonster(Gm, 1999, 1)!.Messages);

        Assert.Equal(GameMasterFrames.SummonMonsterFail, reply.Opcode);
        Assert.Equal(new byte[] { 1 }, reply.Payload);
    }

    [Fact]
    public void AcksBecomeTheSuccessOrFailureRepliesOfTheirCommand()
    {
        var ok = GameMasterService.Acknowledged(Gm, GameMasterFrames.ReportSummonMonster, GameMasterFrames.AckResult(GameMasterFrames.AckOk))!;
        Assert.Equal((GameMasterFrames.SummonMonsterOk, 0), (Assert.Single(ok.Messages).Opcode, ok.Messages[0].Payload.Length));

        var failed = GameMasterService.Acknowledged(Gm, GameMasterFrames.ReportSummonMonster, GameMasterFrames.AckResult(GameMasterFrames.AckFailed))!;
        Assert.Equal(GameMasterFrames.SummonMonsterFail, Assert.Single(failed.Messages).Opcode);
        Assert.Equal(new byte[] { 4 }, failed.Messages[0].Payload);

        var music = GameMasterService.Acknowledged(Gm, GameMasterFrames.ReportSetMusic, GameMasterFrames.AckResult(0))!;
        Assert.Equal(GameMasterFrames.SetMusicOk, Assert.Single(music.Messages).Opcode);

        var removed = GameMasterService.Acknowledged(Gm, GameMasterFrames.ReportRemoveObject, GameMasterFrames.AckResult(GameMasterFrames.AckRemoveObjectFailed))!;
        Assert.Equal(GameMasterFrames.RemoveObjectFail, Assert.Single(removed.Messages).Opcode);
        Assert.Empty(removed.Messages[0].Payload);

        var giftFailed = GameMasterService.Acknowledged(Gm, GameMasterFrames.ReportOxGiveItem, GameMasterFrames.AckResult(GameMasterFrames.AckNotOxMap))!;
        Assert.Equal(GameMasterFrames.OxGiveItemFail, Assert.Single(giftFailed.Messages).Opcode);

        var issueFailed = GameMasterService.Acknowledged(Gm, GameMasterFrames.ReportSetIssueItem, GameMasterFrames.AckResult(GameMasterFrames.AckNoSuchNpc))!;
        Assert.Equal(GameMasterFrames.SetIssueItemFail, Assert.Single(issueFailed.Messages).Opcode);
        Assert.Equal(new byte[] { 1 }, issueFailed.Messages[0].Payload);

        Assert.Null(GameMasterService.Acknowledged(Gm, 0x7F, GameMasterFrames.AckResult(0)));
        Assert.Null(GameMasterService.Acknowledged(Gm, GameMasterFrames.ReportSetMusic, new byte[2]));
    }

    [Fact]
    public void OnlyCommandsWithAnAckExpectAReport()
    {
        Assert.Equal(GameMasterFrames.ReportSummonMonster, GameMasterFrames.ReportOf(GameMasterFrames.OrderSummonMonster));
        Assert.Equal(GameMasterFrames.ReportCharInfo, GameMasterFrames.ReportOf(GameMasterFrames.OrderCharInfo));
        Assert.Equal(GameMasterFrames.ReportSetIssueItem, GameMasterFrames.ReportOf(GameMasterFrames.OrderSetIssueItem));
        Assert.Equal(GameMasterFrames.ReportOxGiveItem, GameMasterFrames.ReportOf(GameMasterFrames.OrderOxGiveItem));
        Assert.Null(GameMasterFrames.ReportOf(GameMasterFrames.OrderOxQuestion));
    }
}
