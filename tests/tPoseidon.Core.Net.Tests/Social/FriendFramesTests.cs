using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.Net.Packets;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Core.Net.Tests.Social;

public sealed class FriendFramesTests
{
    private static readonly Encoding Latin = Encoding.Latin1;
    private static readonly string[] RemovedBy = { "Gone" };
    private static readonly FriendGroup[] OneGroup = { new(0, "A") };
    private static readonly FriendListed[] OneFriend = { new(new FriendEntry(0x10001F42, 0, "Bo", 9), Online: true) };
    private static readonly FriendMessage[] OneMessage =
    {
        new(0, "GM", new DateTimeOffset(2026, 10, 16, 12, 30, 5, TimeSpan.Zero), "hi", 2),
    };

    [Fact]
    public void AnEmptyFriendListMatchesTheCapturedFrame()
    {
        var groups = new[]
        {
            new FriendGroup(0, "??"), new FriendGroup(1, "?????"), new FriendGroup(2, "???"),
            new FriendGroup(3, "????"), new FriendGroup(4, "?????"),
        };

        var payload = FriendFrames.List(Array.Empty<string>(), groups, Array.Empty<FriendListed>(), Array.Empty<FriendMessage>(), Latin);

        Assert.Equal(PassiveSkillGainPacket.SnapshotDefault.Bytes, payload);
        Assert.Equal(FriendFrames.ListWithoutFriends, FriendFrames.ListOpcode(0));
    }

    [Fact]
    public void AListWithFriendsCarriesEachFriendBetweenTheGroupsAndTheMessages()
    {
        var payload = FriendFrames.List(RemovedBy, OneGroup, OneFriend, OneMessage, Latin);

        var expected = new List<byte>();
        expected.AddRange(new byte[] { 1, 0, 0, 0 });
        expected.AddRange(Encoding.ASCII.GetBytes("Gone\0"));
        expected.AddRange(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 });
        expected.AddRange(Encoding.ASCII.GetBytes("A\0"));
        expected.AddRange(new byte[] { 1, 0, 0, 0, 0x42, 0x1F, 0x00, 0x10, 0, 0, 0, 0, 0 });
        expected.AddRange(Encoding.ASCII.GetBytes("Bo\0"));
        expected.AddRange(new byte[] { FriendFrames.OnlineStatus, 9, 0 });
        expected.AddRange(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
        expected.AddRange(Encoding.ASCII.GetBytes("2026-10-16 12:30:05\0hi\0GM\0"));
        expected.AddRange(new byte[] { 2, 0, 0, 0 });
        Assert.Equal(expected.ToArray(), payload);
        Assert.Equal(FriendFrames.ListWithFriends, FriendFrames.ListOpcode(1));
    }

    [Fact]
    public void TheAcceptorSeesTheRequesterInTheGroupItWasStoredIn()
    {
        var bytes = FriendFrames.AddedEntry(7, 0, "Tata", 0x0102, forRequester: false, System.Text.Encoding.Latin1);

        Assert.Equal(new byte[] { 7, 0, 0, 0, 0, 0, 0, 0, 0, (byte)'T', (byte)'a', (byte)'t', (byte)'a', 0, 0x02, 0x01 }, bytes);
    }

    [Fact]
    public void TheRequesterSeesTheAcceptorWithAnExtraByte()
    {
        var bytes = FriendFrames.AddedEntry(7, 0, "Tata", 0x0102, forRequester: true, System.Text.Encoding.Latin1);

        Assert.Equal(new byte[] { 7, 0, 0, 0, 0, 0, 0, 0, 0, (byte)'T', (byte)'a', (byte)'t', (byte)'a', 0, FriendFrames.OnlineStatus, 0x02, 0x01 }, bytes);
    }

    [Fact]
    public void ADeletedOfflineFriendIsAUidAndAName()
    {
        var bytes = FriendFrames.CharacterAndName(0x0102030405060708, "Bo", Latin);

        Assert.Equal(new byte[] { 8, 7, 6, 5, 4, 3, 2, 1, (byte)'B', (byte)'o', 0 }, bytes);
        Assert.Equal(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, (byte)'B', (byte)'o', 0 }, FriendFrames.CharacterAndName(0, "Bo", Latin));
    }

    [Fact]
    public void ATypeChangeIsAUidAndTwoBytes()
    {
        Assert.Equal(new byte[] { 7, 0, 0, 0, 0, 0, 0, 0, 5, 3 }, FriendFrames.TypeChanged(7, 5, 3));
    }

    [Fact]
    public void TheNewFriendOpcodesMatchTheClientTable()
    {
        Assert.Equal(0x2139, FriendFrames.DeleteOffline);
        Assert.Equal(0x2155, FriendFrames.DeletedOffline);
        Assert.Equal(0x2177, FriendFrames.DeleteOfflineFailed);
        Assert.Equal(0x2186, FriendFrames.FriendTypeChanged);
        Assert.Equal(0x2188, FriendFrames.AddPairMismatch);
        Assert.Equal(0x2189, FriendFrames.FriendRenamed);
    }

    [Theory]
    [InlineData(false, false, AcceptClass.Insert)]
    [InlineData(false, true, AcceptClass.Insert)]
    [InlineData(true, true, AcceptClass.AlreadyFriends)]
    [InlineData(true, false, AcceptClass.PairMismatch)]
    public void AnAcceptIsClassifiedByBothLists(bool acceptorListsRequester, bool requesterListsAcceptor, AcceptClass expected)
    {
        Assert.Equal(expected, FriendDecisions.ClassifyAccept(acceptorListsRequester, requesterListsAcceptor));
    }

    [Fact]
    public void AGuildSmsForTheMasterCarriesTheSenderAndTheText()
    {
        var bytes = FriendFrames.GuildSmsFrom(7, "Cy", "hi", Latin);

        Assert.Equal(new byte[] { 7, 0, 0, 0, 0, 0, 0, 0, (byte)'C', (byte)'y', 0, (byte)'h', (byte)'i', 0 }, bytes);
        Assert.Equal(0x2185, FriendFrames.GuildSmsReceived);
    }

    [Fact]
    public void ARelayedPositionIsTheZoneAlone()
    {
        Assert.Equal(new byte[] { 0x94, 0x00 }, FriendFrames.Zone(0x94));
        Assert.Equal(0x2168, FriendFrames.PositionZone);
    }

    [Fact]
    public void AZoneMoveAnswerIsOneByte()
    {
        Assert.Equal(new byte[] { 1 }, FriendFrames.ZoneMoveAnswer(ZoneMoveAnswer.SameZone));
        Assert.Equal(ZoneMoveAnswer.SameZone, FriendDecisions.ZoneMove(0x94, 0x94));
        Assert.Equal(ZoneMoveAnswer.NotReachable, FriendDecisions.ZoneMove(0x94, 0x95));
        Assert.Equal(ZoneMoveAnswer.NotReachable, FriendDecisions.ZoneMove(null, 0x95));
    }

    [Fact]
    public void TheRelayOpcodesMatchTheServerTable()
    {
        Assert.Equal(0x2149, FriendFrames.RequestSignalOn);
        Assert.Equal(0x214A, FriendFrames.RequestSignalOff);
        Assert.Equal(0x214B, FriendFrames.RequestSignalOnRelayed);
        Assert.Equal(0x214C, FriendFrames.RequestSignalOffRelayed);
        Assert.Equal(0x214E, FriendFrames.RequestInFlight);
        Assert.Equal(0x2165, FriendFrames.RequestWaitRelay);
        Assert.Equal(0x217D, FriendFrames.MessageBlockRelay);
        Assert.Equal(0x217E, FriendFrames.MessageBlocked);
        Assert.Equal(0x217F, FriendFrames.RequestShown);
        Assert.Equal(0x2180, FriendFrames.RequestShownTo);
        Assert.Equal(0x218A, FriendFrames.ChangeZoneToRequest);
        Assert.Equal(0x218C, FriendFrames.ChangeZoneToAnswer);
    }

    [Fact]
    public void AnImmediateMemoIsTheNameThenTheText()
    {
        Assert.Equal(new byte[] { (byte)'B', (byte)'o', 0, (byte)'h', (byte)'i', 0 }, FriendFrames.MemoNow("Bo", "hi", Latin));
        Assert.Equal(0x2183, FriendFrames.GmMemoNow);
    }
}
