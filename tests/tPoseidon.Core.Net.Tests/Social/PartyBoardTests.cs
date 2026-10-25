using System.Text;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Core.Net.Tests.Social;

public sealed class PartyBoardTests
{
    private static readonly Encoding Text = Encoding.Latin1;

    private static byte[] Join(params byte[][] parts) => parts.SelectMany(part => part).ToArray();

    private static byte[] Padded(string value, int length)
    {
        var bytes = new byte[length];
        Text.GetBytes(value).CopyTo(bytes, 0);
        return bytes;
    }

    private static PartyBoard.PartyPostFields Fields(ushort partyId, ushort level, uint partyType = 1, string master = "Ana") =>
        new(partyId, 5, master, partyType, 2, level, 0, 0);

    [Fact]
    public void APartyRecordIs72BytesWithTheMasterFieldsAndTheComment()
    {
        var record = PartyBoardFrames.PartyRecord(0x0102, 5, 1, 41, "Ana", "Hi", Text);

        Assert.Equal(Join(
            new byte[] { 0x02, 0x01, 0x00, 0x00 },
            new byte[] { 0x05, 0x00, 0x00, 0x00 },
            new byte[] { 0x01, 0x00, 0x00, 0x00 },
            new byte[] { 0x29, 0x00 },
            Padded("Ana", 20),
            Padded("Hi", 35),
            new byte[3]), record);
        Assert.Equal(PartyBoardFrames.RecordLength, record.Length);
    }

    [Fact]
    public void ASeekerRecordIs72BytesWithTheCharacterIdTypeAndLevel()
    {
        var record = PartyBoardFrames.SeekerRecord(0x1122334455667788, 3, 60, "Bo", "Need a party", Text);

        Assert.Equal(Join(
            new byte[] { 0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11 },
            new byte[] { 0x03, 0x00, 0x00, 0x00 },
            new byte[] { 0x3C, 0x00 },
            Padded("Bo", 20),
            Padded("Need a party", 35),
            new byte[3]), record);
    }

    [Fact]
    public void NamesAndCommentsAreCutToTheirSlotsLeavingTheTerminator()
    {
        var record = PartyBoardFrames.PartyRecord(1, 1, 1, 1, new string('n', 30), new string('c', 50), Text);

        Assert.Equal(new string('n', 19), Text.GetString(record, 0x0E, 19));
        Assert.Equal(0, record[0x0E + 19]);
        Assert.Equal(new string('c', 34), Text.GetString(record, 0x22, 34));
        Assert.Equal(0, record[0x22 + 34]);
    }

    [Fact]
    public void APageStartsWithTheRecordCount()
    {
        var one = PartyBoardFrames.SeekerRecord(1, 1, 1, "A", string.Empty, Text);
        var two = PartyBoardFrames.SeekerRecord(2, 1, 1, "B", string.Empty, Text);

        var page = PartyBoardFrames.PageOf(new[] { one, two });

        Assert.Equal(Join(new byte[] { 2 }, one, two), page);
    }

    [Fact]
    public void AListRequestOfTheRecruitBoardCarriesPageSortOrderLevelSwitchAndLevel()
    {
        Assert.True(PartyBoardFrames.TryReadList(new byte[] { 0x02, 0x00, 0x04, 0x01, 0x01, 0x32, 0x00 }, withLevelFilter: true, out var list));

        Assert.Equal(new PartyBoardList(2, 4, true, true, 50), list);
    }

    [Fact]
    public void AListRequestOfTheSeekerBoardHasNoLevelFilter()
    {
        Assert.True(PartyBoardFrames.TryReadList(new byte[] { 0x00, 0x00, 0x08, 0x00 }, withLevelFilter: false, out var list));

        Assert.Equal(new PartyBoardList(0, 8, false, false, 0), list);
    }

    [Fact]
    public void ATruncatedListRequestIsRejected()
    {
        Assert.False(PartyBoardFrames.TryReadList(new byte[] { 0x00, 0x00, 0x04 }, withLevelFilter: true, out _));
    }

    [Fact]
    public void ASearchRequestCarriesPageSortOrderModeAndText()
    {
        var body = Join(new byte[] { 0x01, 0x00, 0x01, 0x01, 0x02 }, Text.GetBytes("45"), new byte[] { 0 });

        Assert.True(PartyBoardFrames.TryReadSearch(body, Text, out var search));

        Assert.Equal(new PartyBoardSearch(1, 1, true, 2, "45"), search);
    }

    [Theory]
    [InlineData("45", 45)]
    [InlineData("  7abc", 7)]
    [InlineData("-3", 65533)]
    [InlineData("+12", 12)]
    [InlineData("abc", 0)]
    [InlineData("", 0)]
    public void SearchNumbersParseLikeTheCRuntime(string text, int expected)
    {
        Assert.Equal((ushort)expected, PartyBoardFrames.ParseNumber(text));
    }

    [Fact]
    public void PartyDetailsCarryTheMasterLocationCountsAndComment()
    {
        var details = PartyBoardFrames.PartyDetailsFor("Ana", 41, 0x0102, 4, 2, 1, "Hello", Text);

        Assert.Equal(Join(
            Text.GetBytes("Ana\0"),
            new byte[] { 0x29, 0x00 },
            new byte[] { 0x02, 0x01, 0x00, 0x00 },
            new byte[] { 0x04, 0x02, 0x01 },
            Text.GetBytes("Hello\0")), details);
    }

    [Fact]
    public void SeekerDetailsCarryTheNameLocationAndComment()
    {
        var details = PartyBoardFrames.SeekerDetailsFor("Bo", 9, "Hello", Text);

        Assert.Equal(Join(Text.GetBytes("Bo\0"), new byte[] { 9, 0, 0, 0 }, Text.GetBytes("Hello\0")), details);
    }

    [Fact]
    public void ARefusalIsTheCodeAsAFourByteInteger()
    {
        Assert.Equal(new byte[] { 0x0F, 0x00, 0x00, 0x00 }, PartyBoardFrames.RefusedFor(PartyBoardFrames.AlreadyPosted));
    }

    [Fact]
    public void TheHeadNoticeOrderIs40BytesOfCommentKindAndSwitch()
    {
        var data = PartyBoardFrames.HeadNoticeOrderData("Join us", PartyBoardFrames.HeadNoticeForParty, active: true, Text);

        Assert.Equal(Join(Padded("Join us", 35), new byte[] { 1, 0, 0, 0 }, new byte[] { 1 }), data);
        Assert.Equal(PartyBoardFrames.HeadNoticeDataLength, data.Length);
        Assert.True(PartyBoardFrames.TryReadHeadNoticeOrder(data, Text, out var comment, out var kind, out var active));
        Assert.Equal(("Join us", PartyBoardFrames.HeadNoticeForParty, true), (comment, kind, active));
    }

    [Fact]
    public void AClearingHeadNoticeOrderHasAnEmptyCommentAndTheSwitchOff()
    {
        var data = PartyBoardFrames.HeadNoticeOrderData(string.Empty, PartyBoardFrames.HeadNoticeForSeeker, active: false, Text);

        Assert.Equal(Join(new byte[35], new byte[] { 2, 0, 0, 0 }, new byte[] { 0 }), data);
    }

    [Fact]
    public void AShortHeadNoticeOrderIsRejected()
    {
        Assert.False(PartyBoardFrames.TryReadHeadNoticeOrder(new byte[39], Text, out _, out _, out _));
    }

    [Fact]
    public void TheShownHeadNoticeIsEntityKindAndText()
    {
        var body = PartyBoardFrames.HeadNoticeShownFor(0x0304, 1, "Hi", Text);

        Assert.Equal(Join(new byte[] { 0x04, 0x03, 0x01 }, Text.GetBytes("Hi\0")), body);
    }

    [Fact]
    public void ThePartyListShowsTheNewestPostFirstInPagesOfEight()
    {
        var board = new PartyBoard(Text);
        for (ushort id = 1; id <= 10; id++) board.AddParty(Fields(id, 40), "post");

        var first = board.ListParties(new PartyBoardList(0, 0, false, false, 0))!;
        var second = board.ListParties(new PartyBoardList(1, 0, false, false, 0))!;
        var third = board.ListParties(new PartyBoardList(2, 0, false, false, 0))!;

        Assert.Equal(8, first[0]);
        Assert.Equal(10, BitConverter.ToUInt16(first, 1));
        Assert.Equal(2, second[0]);
        Assert.Equal(2, BitConverter.ToUInt16(second, 1));
        Assert.Equal(new byte[] { 0 }, third);
    }

    [Fact]
    public void SortingByLevelOrdersTheFrozenLevelsHighToLowOrLowToHigh()
    {
        var board = new PartyBoard(Text);
        board.AddParty(Fields(1, 40), "a");
        board.AddParty(Fields(2, 60), "b");
        board.AddParty(Fields(3, 50), "c");

        var descending = board.ListParties(new PartyBoardList(0, PartyBoardFrames.PartySortByLevel, true, false, 0))!;
        var ascending = board.ListParties(new PartyBoardList(0, PartyBoardFrames.PartySortByLevel, false, false, 0))!;

        Assert.Equal(new ushort[] { 2, 3, 1 }, Ids(descending));
        Assert.Equal(new ushort[] { 1, 3, 2 }, Ids(ascending));
    }

    [Fact]
    public void SortingByTypeUsesThePartyTypeAtPostingTime()
    {
        var board = new PartyBoard(Text);
        var early = board.AddParty(Fields(1, 40, partyType: 1), "a");
        board.AddParty(Fields(2, 40, partyType: 3), "b");
        early.Refresh(Fields(1, 40, partyType: 9));

        var descending = board.ListParties(new PartyBoardList(0, PartyBoardFrames.PartySortByType, true, false, 0))!;

        Assert.Equal(new ushort[] { 2, 1 }, Ids(descending));
    }

    [Fact]
    public void ARefreshChangesTheShownFieldsButNotTheSortKeys()
    {
        var board = new PartyBoard(Text);
        board.AddParty(Fields(1, 40), "a");
        board.AddParty(Fields(2, 60), "b");
        board.Party(1)!.Refresh(Fields(1, 99));

        var descending = board.ListParties(new PartyBoardList(0, PartyBoardFrames.PartySortByLevel, true, false, 0))!;

        Assert.Equal(new ushort[] { 2, 1 }, Ids(descending));
        Assert.Equal(99, BitConverter.ToUInt16(descending, 1 + PartyBoardFrames.RecordLength + 0x0C));
    }

    [Fact]
    public void AnUnknownSortAnswersNothing()
    {
        var board = new PartyBoard(Text);

        Assert.Null(board.ListParties(new PartyBoardList(0, 2, false, false, 0)));
        Assert.Null(board.ListSeekers(new PartyBoardList(0, 4, false, false, 0)));
    }

    [Fact]
    public void TheLevelSwitchKeepsPostsWithinThreeLevelsAndAtMost24()
    {
        var board = new PartyBoard(Text);
        for (ushort id = 1; id <= 30; id++) board.AddParty(Fields(id, 50), "x");
        board.AddParty(Fields(100, 54), "far");
        board.AddParty(Fields(101, 47), "edge");

        var all = Enumerable.Range(0, 5)
            .SelectMany(page => Ids(board.ListParties(new PartyBoardList((ushort)page, 0, true, true, 50))!))
            .ToArray();

        Assert.Equal(24, all.Length);
        Assert.DoesNotContain((ushort)100, all);
        Assert.Contains((ushort)101, all);
    }

    [Fact]
    public void TheLevelSwitchOrdersTheFilteredPostsByTheChosenDirection()
    {
        var board = new PartyBoard(Text);
        board.AddParty(Fields(1, 50), "a");
        board.AddParty(Fields(2, 51), "b");
        board.AddParty(Fields(3, 52), "c");

        var descending = board.ListParties(new PartyBoardList(0, PartyBoardFrames.PartySortByLevel, true, true, 51))!;
        var ascending = board.ListParties(new PartyBoardList(0, PartyBoardFrames.PartySortByLevel, false, true, 51))!;

        Assert.Equal(new ushort[] { 3, 2, 1 }, Ids(descending));
        Assert.Equal(new ushort[] { 1, 2, 3 }, Ids(ascending));
    }

    [Fact]
    public void AnUnknownSortWithTheLevelSwitchAnswersAnEmptyPage()
    {
        var board = new PartyBoard(Text);
        board.AddParty(Fields(1, 50), "a");

        Assert.Equal(new byte[] { 0 }, board.ListParties(new PartyBoardList(0, 2, true, true, 50)));
    }

    [Fact]
    public void SearchingByNameFindsTheExactMasterOrAnEmptyPage()
    {
        var board = new PartyBoard(Text);
        board.AddParty(Fields(1, 50, master: "Ana"), "a");
        board.AddParty(Fields(2, 50, master: "Bo"), "b");

        var found = board.SearchParties(new PartyBoardSearch(0, 0, false, PartyBoardFrames.SearchByName, "Bo"))!;
        var missing = board.SearchParties(new PartyBoardSearch(0, 0, false, PartyBoardFrames.SearchByName, "bo"))!;

        Assert.Equal(new ushort[] { 2 }, Ids(found));
        Assert.Equal(new byte[] { 0 }, missing);
    }

    [Fact]
    public void SearchingForLevelsAtLeastAndAtMostUsesAThreeLevelWindow()
    {
        var board = new PartyBoard(Text);
        board.AddParty(Fields(1, 40), "a");
        board.AddParty(Fields(2, 43), "b");
        board.AddParty(Fields(3, 44), "c");
        board.AddParty(Fields(4, 37), "d");
        board.AddParty(Fields(5, 36), "e");

        var atLeast = board.SearchParties(new PartyBoardSearch(0, 0, true, PartyBoardFrames.SearchLevelAtLeast, "40"))!;
        var atMost = board.SearchParties(new PartyBoardSearch(0, 0, true, PartyBoardFrames.SearchLevelAtMost, "40"))!;

        Assert.Equal(new ushort[] { 2, 1 }, Ids(atLeast).OrderByDescending(id => id).ToArray());
        Assert.Equal(new ushort[] { 4, 1 }, Ids(atMost).OrderByDescending(id => id).ToArray());
    }

    [Fact]
    public void AnUnknownSearchModeAnswersNothing()
    {
        var board = new PartyBoard(Text);

        Assert.Null(board.SearchParties(new PartyBoardSearch(0, 0, false, 3, "x")));
        Assert.Null(board.SearchSeekers(new PartyBoardSearch(0, 0, false, 3, "x")));
    }

    [Fact]
    public void TheSeekerListShowsTheNewestFirstAndSortsByTheLevelAtPosting()
    {
        var board = new PartyBoard(Text);
        board.AddSeeker(0x11, 1, 40, "Ana", "a");
        board.AddSeeker(0x22, 3, 60, "Bo", "b");
        board.AddSeeker(0x33, 5, 50, "Cy", "c");

        var newest = board.ListSeekers(new PartyBoardList(0, 0, false, false, 0))!;
        var alias = board.ListSeekers(new PartyBoardList(0, PartyBoardFrames.SeekerSortDefaultAlias, false, false, 0))!;
        var byLevel = board.ListSeekers(new PartyBoardList(0, PartyBoardFrames.SeekerSortByLevel, true, false, 0))!;

        Assert.Equal(new ulong[] { 0x33, 0x22, 0x11 }, SeekerIds(newest));
        Assert.Equal(newest, alias);
        Assert.Equal(new ulong[] { 0x22, 0x33, 0x11 }, SeekerIds(byLevel));
    }

    [Fact]
    public void SearchingSeekersByNameReturnsTheOnePost()
    {
        var board = new PartyBoard(Text);
        board.AddSeeker(0x11, 1, 40, "Ana", "a");
        board.AddSeeker(0x22, 3, 60, "Bo", "b");

        var found = board.SearchSeekers(new PartyBoardSearch(0, 0, false, PartyBoardFrames.SearchByName, "Bo"))!;

        Assert.Equal(new ulong[] { 0x22 }, SeekerIds(found));
    }

    private static ushort[] Ids(byte[] page) =>
        Enumerable.Range(0, page[0]).Select(i => BitConverter.ToUInt16(page, 1 + i * PartyBoardFrames.RecordLength)).ToArray();

    private static ulong[] SeekerIds(byte[] page) =>
        Enumerable.Range(0, page[0]).Select(i => BitConverter.ToUInt64(page, 1 + i * PartyBoardFrames.RecordLength)).ToArray();
}
