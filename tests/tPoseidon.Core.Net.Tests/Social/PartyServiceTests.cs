using System.Text;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Core.Net.Tests.Social;

public sealed class PartyServiceTests
{
    private const ulong Ana = 0x1001;
    private const ulong Bo = 0x1002;
    private const ulong Cy = 0x1003;

    private static readonly Encoding Text = Encoding.Latin1;

    private sealed class ManualTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 16, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static PartyService NewService(ManualTime? time = null)
    {
        var service = new PartyService(Text, PartyRules.FromIni(1, 600_000), time ?? new ManualTime());
        service.Login(Ana, "Ana", 1, 40);
        service.Login(Bo, "Bo", 3, 42);
        service.Login(Cy, "Cy", 5, 44);
        return service;
    }

    private static PartyService NewPartyOfAnaAndBo(ManualTime? time = null)
    {
        var service = NewService(time);
        service.Make(Ana, "Crew", "Bo");
        service.AcceptInvite(Bo);
        return service;
    }

    [Fact]
    public void MakingAPartyTellsTheMakerAndInvitesTheCandidate()
    {
        var service = NewService();

        var sent = service.Make(Ana, "Crew", "Bo");

        Assert.Collection(
            sent,
            m => Assert.Equal((Ana, PartyFrames.Made), (m.To, m.Opcode)),
            m =>
            {
                Assert.Equal((Bo, PartyFrames.Invitation), (m.To, m.Opcode));
                Assert.Equal(PartyFrames.InvitationFor("Crew", "Ana", 1, PartyDivision.ByLevel, 0, 40, Text), m.Payload);
            });
    }

    [Fact]
    public void AcceptingFormsThePartyAndSendsTheJoinerTheWholeState()
    {
        var service = NewService();
        service.Make(Ana, "Crew", "Bo");

        var sent = service.AcceptInvite(Bo).ToList();

        var formed = Assert.Single(sent, m => m.Opcode == PartyFrames.Formed);
        Assert.Equal(Ana, formed.To);
        Assert.True(sent.IndexOf(formed) < sent.FindIndex(m => m.To == Ana && m.Opcode == PartyFrames.MemberJoined));
        var state = Assert.Single(sent, m => m.Opcode == PartyFrames.JoinState);
        Assert.Equal(Bo, state.To);
        Assert.Contains(sent, m => m.To == Ana && m.Opcode == PartyFrames.JoinedNotice);
        Assert.Equal(1, service.PartyCount);
    }

    [Fact]
    public void RefusingTheFirstInviteBreaksTheMakersEmptyParty()
    {
        var service = NewService();
        service.Make(Ana, "Crew", "Bo");

        var sent = service.RefuseInvite(Bo, 7);

        Assert.Contains(sent, m => m.To == Ana && m.Opcode == PartyFrames.InviteFailed && m.Payload[^1] == 7);
        Assert.Contains(sent, m => m.To == Ana && m.Opcode == PartyFrames.LeaveDone);
        Assert.Equal(0, service.PartyCount);
    }

    [Fact]
    public void LeavingATwoMemberPartyBreaksIt()
    {
        var service = NewPartyOfAnaAndBo();

        var sent = service.Leave(Bo);

        Assert.Contains(sent, m => m.To == Ana && m.Opcode == PartyFrames.MemberLeft);
        Assert.Contains(sent, m => m.To == Ana && m.Opcode == PartyFrames.Broken);
        Assert.Contains(sent, m => m.To == Bo && m.Opcode == PartyFrames.LeaveDone);
        Assert.Equal(0, service.PartyCount);
    }

    [Fact]
    public void OnlyTheMasterInvitesAndAFullPartyRefuses()
    {
        var service = NewPartyOfAnaAndBo();

        var fromMember = Assert.Single(service.Invite(Bo, "Cy"));

        Assert.Equal(PartyFrames.InviteFailed, fromMember.Opcode);
        Assert.Equal(PartyCodes.NotMaster, fromMember.Payload[^1]);
        Assert.Contains(service.Invite(Ana, "Cy"), m => m.To == Cy && m.Opcode == PartyFrames.Invitation);
    }

    [Fact]
    public void PartyChatReachesEveryOnlineMember()
    {
        var service = NewPartyOfAnaAndBo();

        var sent = service.Chat(Bo, "hi");

        Assert.Equal(new[] { Ana, Bo }, sent.Select(m => m.To).OrderBy(id => id));
        Assert.All(sent, m => Assert.Equal(PartyFrames.ChatLine, m.Opcode));
    }

    [Fact]
    public void KickingRemovesTheMemberAndBreaksATwoMemberParty()
    {
        var service = NewService();
        service.Make(Ana, "Crew", "Bo");
        var joinState = Assert.Single(service.AcceptInvite(Bo), m => m.Opcode == PartyFrames.JoinState);
        var boMemberId = BitConverter.ToUInt16(joinState.Payload, 2 + "Crew\0".Length + 2);

        var sent = service.Kick(Ana, boMemberId);

        Assert.Contains(sent, m => m.To == Bo && m.Opcode == PartyFrames.MemberKicked);
        Assert.Contains(sent, m => m.To == Ana && m.Opcode == PartyFrames.Broken);
        Assert.Equal(0, service.PartyCount);
    }

    [Fact]
    public void AMemberWhoDisconnectsIsKeptAndReturnsOnLogin()
    {
        var service = NewPartyOfAnaAndBo();

        var offline = service.Logout(Bo);
        var back = service.Login(Bo, "Bo", 3, 42);

        Assert.Contains(offline, m => m.To == Ana && m.Opcode == PartyFrames.MemberOffline);
        Assert.Contains(back, m => m.To == Ana && m.Opcode == PartyFrames.MemberReturned);
        Assert.Equal(1, service.PartyCount);
        Assert.Contains(service.Chat(Bo, "back"), m => m.To == Ana);
    }

    [Fact]
    public void AnOfflineMemberExpiresAfterTheKeepTime()
    {
        var time = new ManualTime();
        var service = NewPartyOfAnaAndBo(time);
        service.Logout(Bo);

        time.Now += TimeSpan.FromMilliseconds(600_000);
        var sent = service.ExpireOfflineMembers();

        var expired = Assert.Single(sent);
        Assert.Equal((Ana, PartyFrames.OfflineMemberExpired), (expired.To, expired.Opcode));
        Assert.Empty(service.Login(Bo, "Bo", 3, 42));
    }

    [Fact]
    public void MemberInfoIsSixtyBytesWithTheFieldsAtTheirOffsets()
    {
        var w = new PacketWriter(PartyFrames.MemberInfoLength);
        PartyFrames.WriteMemberInfo(w, new PartyMemberInfo("Ana", 0x0102, 0x0304, 0x0506, 0x0708, 0x1122334455667788), Text);
        var bytes = w.ToArray();

        Assert.Equal(PartyFrames.MemberInfoLength, bytes.Length);
        Assert.Equal((byte)'A', bytes[0]);
        Assert.Equal(0x0102, BitConverter.ToUInt16(bytes, 0x14));
        Assert.Equal(0x0304, BitConverter.ToUInt16(bytes, 0x17));
        Assert.Equal(0x0506, BitConverter.ToUInt16(bytes, 0x19));
        Assert.Equal(0x0708, BitConverter.ToUInt16(bytes, 0x2B));
        Assert.Equal(0x1122334455667788UL, BitConverter.ToUInt64(bytes, 0x33));
    }

    [Fact]
    public void LevelSharesAlwaysAddUpToOneHundred()
    {
        var shares = PartyService.LevelShares(new (ushort, ushort)[] { (1, 10), (2, 10), (3, 10) });

        Assert.Equal(100, shares.Sum(s => s.Percent));
        Assert.Equal(new byte[] { 34, 33, 33 }, shares.Select(s => s.Percent));
    }

    [Theory]
    [InlineData(new ushort[] { 1, 3 }, new ushort[] { 20, 40 }, PartyType.Beginner)]
    [InlineData(new ushort[] { 1, 3 }, new ushort[] { 40, 140 }, PartyType.Fixed)]
    [InlineData(new ushort[] { 1, 3 }, new ushort[] { 40, 50 }, PartyType.Normal)]
    [InlineData(new ushort[] { 1, 4, 5, 8 }, new ushort[] { 50, 51, 52, 53 }, PartyType.Royal)]
    [InlineData(new ushort[] { 1, 2, 10, 20 }, new ushort[] { 50, 51, 52, 53 }, PartyType.Type)]
    [InlineData(new ushort[] { 1, 2, 9, 10 }, new ushort[] { 50, 51, 52, 53 }, PartyType.Normal)]
    [InlineData(new ushort[] { 1, 3, 14, 25 }, new ushort[] { 50, 51, 52, 53 }, PartyType.SameGender)]
    [InlineData(new ushort[] { 1, 3, 5, 9 }, new ushort[] { 40, 40, 40, 40 }, PartyType.Level)]
    [InlineData(new ushort[] { 1, 3, 5, 8 }, new ushort[] { 40, 80, 120, 160 }, PartyType.Fixed)]
    [InlineData(new ushort[] { 1, 3, 5, 8 }, new ushort[] { 32, 64, 96, 128 }, PartyType.MultipleLv)]
    public void PartyTypeFollowsTheComposition(ushort[] kinds, ushort[] levels, PartyType expected)
    {
        var members = kinds.Zip(levels, (k, l) => (k, l)).ToArray();

        Assert.Equal(expected, PartyService.TypeOf(members));
    }

    [Fact]
    public void EnteringAZoneTellsThePartyAndRefreshesTheMembersMirror()
    {
        var service = NewPartyOfAnaAndBo();

        var sent = service.ZoneEntered(Bo, 0x0101);

        Assert.Contains(sent, m => m.To == Ana && m.Opcode == PartyFrames.MemberZone);
        var order = Assert.Single(sent.Orders, o => o.OrderType == SystemLinkFrames.OrderNotifyPartyPlayer);
        Assert.Equal(Bo, order.CharacterId);
        var mirror = SystemLinkFrames.DecodeMirror(order.Data);
        Assert.Equal(Ana, mirror.MasterCharacterId);
        Assert.Equal(Ana, Assert.Single(mirror.Others).CharacterId);
    }

    [Fact]
    public void LeavingCancelsTheLeaversMirror()
    {
        var service = NewService();
        service.Make(Ana, "Crew", "Bo");
        service.AcceptInvite(Bo);
        service.Invite(Ana, "Cy");
        service.AcceptInvite(Cy);

        var sent = service.Leave(Cy);

        Assert.Contains(sent.Orders, o => o.CharacterId == Cy && o.IsCancel);
        Assert.Contains(sent.Orders, o => o.CharacterId == Bo && !o.IsCancel);
    }

    private static ushort PartyIdOf(PartyService service)
    {
        var info = Assert.Single(service.Info(Cy, "Ana"), m => m.Opcode == PartyFrames.Info);
        return BitConverter.ToUInt16(info.Payload, 0);
    }

    private static PartyQuestBegin QuestFor(ushort partyId, ushort target) => new(
        partyId, 0x0101, Ana, 77, 3006, 2094, target, 0, 1_200_000,
        new[] { new PartyQuestMember(Ana, 0), new PartyQuestMember(Bo, 1) });

    [Fact]
    public void AQuestKillIsTalliedToEveryMemberUntilTheTarget()
    {
        var service = NewPartyOfAnaAndBo();
        Assert.True(service.BeginQuest(QuestFor(PartyIdOf(service), target: 2)));

        var first = service.QuestKill(Bo, 2094);
        var wrongMonster = service.QuestKill(Ana, 1);
        var second = service.QuestKill(Ana, 2094);
        var after = service.QuestKill(Ana, 2094);

        Assert.Equal(new[] { Ana, Bo }, first.Orders.Select(o => o.CharacterId).OrderBy(id => id));
        var tally = SystemLinkFrames.DecodeQuestTally(first.Orders[0].Data);
        Assert.Equal(1, tally.TeamKills);
        Assert.Contains((Bo, (ushort)1), tally.Members);
        Assert.Empty(wrongMonster.Orders);
        Assert.Equal(2, SystemLinkFrames.DecodeQuestTally(second.Orders[0].Data).TeamKills);
        Assert.Empty(after.Orders);
    }

    [Fact]
    public void ARunningQuestRefusesInvitesAndTheMasterCanTearItDown()
    {
        var service = NewPartyOfAnaAndBo();
        service.BeginQuest(QuestFor(PartyIdOf(service), target: 10));

        var invite = Assert.Single(service.Invite(Ana, "Cy"));
        var teardown = service.TearDownQuest(Ana);

        Assert.Equal(PartyCodes.PartyQuestRunning, invite.Payload[^1]);
        Assert.All(teardown.Orders, o => Assert.Equal(SystemLinkFrames.OrderCancelQuest, o.OrderType));
        Assert.Equal(SystemLinkFrames.QuestCancelByMaster, SystemLinkFrames.DecodeQuestCancel(teardown.Orders[0].Data).Reason);
        Assert.Contains(service.Invite(Ana, "Cy"), m => m.Opcode == PartyFrames.Invitation);
    }
}
