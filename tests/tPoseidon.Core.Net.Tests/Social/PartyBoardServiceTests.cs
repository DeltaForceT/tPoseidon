using System.Text;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Core.Net.Tests.Social;

public sealed class PartyBoardServiceTests
{
    private const ulong Ana = 0x1001;
    private const ulong Bo = 0x1002;
    private const ulong Cy = 0x1003;
    private const ulong Dee = 0x1004;

    private static readonly Encoding Text = Encoding.Latin1;

    private sealed class ManualTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 25, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static PartyService NewService()
    {
        var service = new PartyService(Text, PartyRules.FromIni(1, 600_000), new ManualTime());
        service.Login(Ana, "Ana", 1, 40);
        service.Login(Bo, "Bo", 3, 42);
        service.Login(Cy, "Cy", 5, 44);
        service.Login(Dee, "Dee", 7, 60);
        return service;
    }

    private static PartyService NewServiceWithPartyOfAnaAndBo()
    {
        var service = NewService();
        service.Make(Ana, "Crew", "Bo");
        service.AcceptInvite(Bo);
        return service;
    }

    private static void AssertFrame(PartyMessage message, ulong to, ushort opcode, byte[] payload)
    {
        Assert.Equal(to, message.To);
        Assert.Equal(opcode, message.Opcode);
        Assert.Equal(payload, message.Payload);
    }

    private static PartyMessage Only(PartyOutbox outbox) => Assert.Single(outbox);

    private static byte[] HeadNotice(string comment, byte kind, bool active) =>
        PartyBoardFrames.HeadNoticeOrderData(comment, kind, active, Text);

    [Fact]
    public void PostingNeedsAParty()
    {
        var service = NewService();

        var sent = service.PostPartyRecruit(Ana, "Come");

        var refusal = Only(sent);
        AssertFrame(refusal, Ana, PartyBoardFrames.Refused, PartyBoardFrames.RefusedFor(PartyBoardFrames.NotInParty));
        Assert.Empty(sent.Orders);
        Assert.Equal(0, service.BoardPartyPosts);
    }

    [Fact]
    public void PostingAnswersEmptyAndShowsTheNoticeOverTheMaster()
    {
        var service = NewServiceWithPartyOfAnaAndBo();

        var sent = service.PostPartyRecruit(Bo, "Come");

        var ok = Only(sent);
        AssertFrame(ok, Bo, PartyBoardFrames.PartyPosted, Array.Empty<byte>());
        var order = Assert.Single(sent.Orders);
        Assert.Equal((Ana, PartyBoardFrames.OrderHeadNotice), (order.CharacterId, order.OrderType));
        Assert.Equal(HeadNotice("Come", PartyBoardFrames.HeadNoticeForParty, true), order.Data);
        Assert.Equal(1, service.BoardPartyPosts);
    }

    [Fact]
    public void ASecondPostOfTheSamePartyIsRefused()
    {
        var service = NewServiceWithPartyOfAnaAndBo();
        service.PostPartyRecruit(Ana, "Come");

        var refusal = Only(service.PostPartyRecruit(Bo, "Again"));

        Assert.Equal(PartyBoardFrames.RefusedFor(PartyBoardFrames.AlreadyPosted), refusal.Payload);
        Assert.Equal(PartyBoardFrames.Refused, refusal.Opcode);
        Assert.Equal(1, service.BoardPartyPosts);
    }

    [Fact]
    public void AFullPartyCannotPost()
    {
        var service = NewService();
        for (ulong id = 0x2001; id <= 0x2004; id++) service.Login(id, $"M{id}", 1, 40);
        service.Make(Ana, "Crew", "Bo");
        service.AcceptInvite(Bo);
        for (ulong id = 0x2001; id <= 0x2004; id++)
        {
            service.Invite(Ana, $"M{id}");
            service.AcceptInvite(id);
        }

        var refusal = Only(service.PostPartyRecruit(Ana, "Come"));

        Assert.Equal(PartyBoardFrames.RefusedFor(PartyBoardFrames.PartyFull), refusal.Payload);
    }

    [Fact]
    public void TheListCarriesTheMasterTheAverageLevelAndTheComment()
    {
        var service = NewServiceWithPartyOfAnaAndBo();
        service.PostPartyRecruit(Ana, "Come");

        var listed = Only(service.BoardListParties(Cy, new PartyBoardList(0, 0, false, false, 0)));

        Assert.Equal((Cy, PartyBoardFrames.PartyList), (listed.To, listed.Opcode));
        Assert.Equal(PartyBoardFrames.PageOf(new[] { PartyBoardFrames.PartyRecord(1, 1, 1, 41, "Ana", "Come", Text) }), listed.Payload);
    }

    [Fact]
    public void AnUnknownSortGetsNoReply()
    {
        var service = NewServiceWithPartyOfAnaAndBo();

        Assert.Empty(service.BoardListParties(Cy, new PartyBoardList(0, 2, false, false, 0)));
    }

    [Fact]
    public void JoiningRefreshesTheMemberCountAndTheAverageLevel()
    {
        var service = NewServiceWithPartyOfAnaAndBo();
        service.PostPartyRecruit(Ana, "Come");
        service.Invite(Ana, "Cy");
        service.AcceptInvite(Cy);

        var listed = Only(service.BoardListParties(Dee, new PartyBoardList(0, 0, false, false, 0)));

        Assert.Equal(PartyBoardFrames.PageOf(new[] { PartyBoardFrames.PartyRecord(1, 1, 1, 42, "Ana", "Come", Text) }), listed.Payload);
        var details = Only(service.BoardPartyDetails(Dee, 1));
        Assert.Equal(PartyBoardFrames.PartyDetails, details.Opcode);
        Assert.Equal(3, details.Payload[Text.GetByteCount("Ana\0") + 2 + 4]);
    }

    [Fact]
    public void ThePartyDetailsCarryTheMasterLevelLocationCountsAndComment()
    {
        var service = NewServiceWithPartyOfAnaAndBo();
        service.ZoneEntered(Ana, 12);
        service.PostPartyRecruit(Ana, "Come");

        var details = Only(service.BoardPartyDetails(Cy, 1));

        Assert.Equal((Cy, PartyBoardFrames.PartyDetails), (details.To, details.Opcode));
        Assert.Equal(PartyBoardFrames.PartyDetailsFor("Ana", 40, 12, 2, PartyDivision.Equal, 0, "Come", Text), details.Payload);
    }

    [Fact]
    public void DetailsOfAMissingPostAreRefusedWithAnEmptyFrame()
    {
        var service = NewServiceWithPartyOfAnaAndBo();

        var missing = Only(service.BoardPartyDetails(Cy, 1));

        AssertFrame(missing, Cy, PartyBoardFrames.PartyDetailsMissing, Array.Empty<byte>());
    }

    [Fact]
    public void OnlyTheMasterRemovesThePost()
    {
        var service = NewServiceWithPartyOfAnaAndBo();
        service.PostPartyRecruit(Ana, "Come");

        var refusal = Only(service.RemovePartyRecruit(Bo));

        AssertFrame(refusal, Bo, PartyBoardFrames.Refused, PartyBoardFrames.RefusedFor(PartyBoardFrames.MasterOnly));
        Assert.Equal(1, service.BoardPartyPosts);
    }

    [Fact]
    public void RemovingWithoutAPartyAnswersTheDedicatedEmptyFrame()
    {
        var service = NewService();

        var sent = Only(service.RemovePartyRecruit(Ana));

        AssertFrame(sent, Ana, PartyBoardFrames.PartyRemoveWithoutParty, Array.Empty<byte>());
    }

    [Fact]
    public void TheMasterRemovesThePostAndTheNoticeIsCleared()
    {
        var service = NewServiceWithPartyOfAnaAndBo();
        service.PostPartyRecruit(Ana, "Come");

        var sent = service.RemovePartyRecruit(Ana);

        var ok = Only(sent);
        Assert.Equal((Ana, PartyBoardFrames.PartyRemoved), (ok.To, ok.Opcode));
        var order = Assert.Single(sent.Orders);
        Assert.Equal(Ana, order.CharacterId);
        Assert.Equal(HeadNotice(string.Empty, PartyBoardFrames.HeadNoticeForParty, false), order.Data);
        Assert.Equal(0, service.BoardPartyPosts);
    }

    [Fact]
    public void RemovingWhenNothingWasPostedSaysNothing()
    {
        var service = NewServiceWithPartyOfAnaAndBo();

        Assert.Empty(service.RemovePartyRecruit(Ana));
    }

    [Fact]
    public void BreakingThePartyRemovesThePost()
    {
        var service = NewServiceWithPartyOfAnaAndBo();
        service.PostPartyRecruit(Ana, "Come");

        var sent = service.Dissolve(Ana);

        Assert.Equal(0, service.BoardPartyPosts);
        Assert.Contains(sent.Orders, order => order.CharacterId == Ana && order.OrderType == PartyBoardFrames.OrderHeadNotice
            && order.Data is not null && order.Data[PartyBoardFrames.CommentLength + sizeof(uint)] == 0);
    }

    [Fact]
    public void ALeaveThatLeavesOneMemberRemovesThePost()
    {
        var service = NewServiceWithPartyOfAnaAndBo();
        service.PostPartyRecruit(Ana, "Come");

        service.Leave(Bo);

        Assert.Equal(0, service.BoardPartyPosts);
    }

    [Fact]
    public void AFullPartyLosesItsPostAndTheMasterIsTold()
    {
        var service = NewService();
        for (ulong id = 0x2001; id <= 0x2003; id++) service.Login(id, $"M{id}", 1, 40);
        service.Make(Ana, "Crew", "Bo");
        service.AcceptInvite(Bo);
        service.PostPartyRecruit(Ana, "Come");
        var last = new PartyOutbox();
        for (ulong id = 0x2001; id <= 0x2003; id++)
        {
            service.Invite(Ana, $"M{id}");
            service.AcceptInvite(id);
        }
        service.Invite(Ana, "Cy");
        last = service.AcceptInvite(Cy);

        Assert.Equal(0, service.BoardPartyPosts);
        Assert.Contains(last, m => (m.To, m.Opcode) == (Ana, PartyFrames.MasterResult) && m.Payload.SequenceEqual(PartyFrames.Code32(PartyCodes.PartyBecameFull)));
    }

    [Fact]
    public void TheNoticeMovesWithTheMaster()
    {
        var service = NewServiceWithPartyOfAnaAndBo();
        service.PostPartyRecruit(Ana, "Come");

        var sent = service.ChangeMaster(Ana, 2);

        Assert.Collection(
            sent.Orders.Where(order => order.OrderType == PartyBoardFrames.OrderHeadNotice),
            cleared =>
            {
                Assert.Equal(Ana, cleared.CharacterId);
                Assert.Equal(HeadNotice(string.Empty, PartyBoardFrames.HeadNoticeForParty, false), cleared.Data);
            },
            shown =>
            {
                Assert.Equal(Bo, shown.CharacterId);
                Assert.Equal(HeadNotice("Come", PartyBoardFrames.HeadNoticeForParty, true), shown.Data);
            });
        var listed = Only(service.BoardListParties(Cy, new PartyBoardList(0, 0, false, false, 0)));
        Assert.Equal(PartyBoardFrames.PageOf(new[] { PartyBoardFrames.PartyRecord(1, 3, 1, 41, "Bo", "Come", Text) }), listed.Payload);
    }

    [Fact]
    public void ChangingTheDivisionRefreshesThePost()
    {
        var service = NewServiceWithPartyOfAnaAndBo();
        service.PostPartyRecruit(Ana, "Come");
        service.ChangeDivision(Ana, PartyDivision.ByLevel, Array.Empty<PartyDivisionShare>());

        var details = Only(service.BoardPartyDetails(Cy, 1));

        Assert.Equal(PartyBoardFrames.PartyDetailsFor("Ana", 40, 0, 2, PartyDivision.ByLevel, 0, "Come", Text), details.Payload);
    }

    [Fact]
    public void ASeekerMustNotBeInAParty()
    {
        var service = NewServiceWithPartyOfAnaAndBo();

        var refusal = Only(service.PostSeeker(Ana, "Need"));

        Assert.Equal(PartyBoardFrames.RefusedFor(PartyBoardFrames.AlreadyInParty), refusal.Payload);
        Assert.Equal(0, service.BoardSeekerPosts);
    }

    [Fact]
    public void ASeekerPostsOnceAndGetsTheNotice()
    {
        var service = NewService();

        var sent = service.PostSeeker(Cy, "Need");

        AssertFrame(Only(sent), Cy, PartyBoardFrames.SeekerPosted, Array.Empty<byte>());
        var order = Assert.Single(sent.Orders);
        Assert.Equal(Cy, order.CharacterId);
        Assert.Equal(HeadNotice("Need", PartyBoardFrames.HeadNoticeForSeeker, true), order.Data);
        var again = Only(service.PostSeeker(Cy, "Again"));
        Assert.Equal(PartyBoardFrames.RefusedFor(PartyBoardFrames.AlreadyPosted), again.Payload);
    }

    [Fact]
    public void TheSeekerListCarriesTheCharacterTypeAndLevel()
    {
        var service = NewService();
        service.PostSeeker(Cy, "Need");

        var listed = Only(service.BoardListSeekers(Ana, new PartyBoardList(0, 0, false, false, 0)));

        Assert.Equal((Ana, PartyBoardFrames.SeekerList), (listed.To, listed.Opcode));
        Assert.Equal(PartyBoardFrames.PageOf(new[] { PartyBoardFrames.SeekerRecord(Cy, 5, 44, "Cy", "Need", Text) }), listed.Payload);
    }

    [Fact]
    public void TheSeekerDetailsCarryTheNameLocationAndComment()
    {
        var service = NewService();
        service.ZoneEntered(Cy, 9);
        service.PostSeeker(Cy, "Need");

        var details = Only(service.BoardSeekerDetails(Ana, Cy));

        AssertFrame(details, Ana, PartyBoardFrames.SeekerDetails, PartyBoardFrames.SeekerDetailsFor("Cy", 9, "Need", Text));
    }

    [Fact]
    public void DetailsOfAnUnknownSeekerAreRefusedWithAnEmptyFrame()
    {
        var service = NewService();

        var missing = Only(service.BoardSeekerDetails(Ana, Cy));

        AssertFrame(missing, Ana, PartyBoardFrames.SeekerDetailsMissing, Array.Empty<byte>());
    }

    [Fact]
    public void ASeekerRemovesTheOwnPost()
    {
        var service = NewService();
        service.PostSeeker(Cy, "Need");

        var sent = service.RemoveSeeker(Cy);

        Assert.Equal((Cy, PartyBoardFrames.SeekerRemoved), (Only(sent).To, Only(sent).Opcode));
        Assert.Equal(HeadNotice(string.Empty, PartyBoardFrames.HeadNoticeForSeeker, false), Assert.Single(sent.Orders).Data);
        Assert.Empty(service.RemoveSeeker(Cy));
    }

    [Fact]
    public void JoiningAPartyRemovesTheSeekerPost()
    {
        var service = NewServiceWithPartyOfAnaAndBo();
        service.PostSeeker(Cy, "Need");
        service.Invite(Ana, "Cy");

        var sent = service.AcceptInvite(Cy);

        Assert.Equal(0, service.BoardSeekerPosts);
        Assert.Contains(sent.Orders, order => order.CharacterId == Cy && order.OrderType == PartyBoardFrames.OrderHeadNotice);
    }

    [Fact]
    public void FormingAPartyRemovesTheSeekerPostOfBothPlayers()
    {
        var service = NewService();
        service.PostSeeker(Ana, "Need");
        service.PostSeeker(Bo, "Need");
        service.Make(Ana, "Crew", "Bo");

        service.AcceptInvite(Bo);

        Assert.Equal(0, service.BoardSeekerPosts);
    }

    [Fact]
    public void LoggingOutRemovesTheSeekerPost()
    {
        var service = NewService();
        service.PostSeeker(Cy, "Need");

        service.Logout(Cy);

        Assert.Equal(0, service.BoardSeekerPosts);
    }

    [Fact]
    public void TheLastOnlineMemberLoggingOutRemovesThePost()
    {
        var service = NewServiceWithPartyOfAnaAndBo();
        service.PostPartyRecruit(Ana, "Come");

        service.Logout(Bo);
        Assert.Equal(1, service.BoardPartyPosts);
        service.Logout(Ana);

        Assert.Equal(0, service.BoardPartyPosts);
    }

    [Fact]
    public void AMasterLoggingOutHandsTheNoticeToTheNewMaster()
    {
        var service = NewServiceWithPartyOfAnaAndBo();
        service.PostPartyRecruit(Ana, "Come");

        var sent = service.Logout(Ana);

        Assert.Contains(sent.Orders, order => order.CharacterId == Bo && order.OrderType == PartyBoardFrames.OrderHeadNotice
            && order.Data is not null && order.Data[PartyBoardFrames.CommentLength + sizeof(uint)] == 1);
        Assert.Equal(1, service.BoardPartyPosts);
    }

    [Fact]
    public void SearchingByTheMasterNameFindsThePartyPost()
    {
        var service = NewServiceWithPartyOfAnaAndBo();
        service.PostPartyRecruit(Ana, "Come");

        var found = Only(service.BoardSearchParties(Cy, new PartyBoardSearch(0, 0, false, PartyBoardFrames.SearchByName, "Ana")));

        Assert.Equal((Cy, PartyBoardFrames.PartyList), (found.To, found.Opcode));
        Assert.Equal(1, found.Payload[0]);
    }

    [Fact]
    public void RequestsFromAnUnknownCharacterAreIgnored()
    {
        var service = NewService();

        Assert.Empty(service.BoardListParties(0x9999, new PartyBoardList(0, 0, false, false, 0)));
        Assert.Empty(service.PostSeeker(0x9999, "x"));
        Assert.Empty(service.PostPartyRecruit(0x9999, "x"));
    }
}
