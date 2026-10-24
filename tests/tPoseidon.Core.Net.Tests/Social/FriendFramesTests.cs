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
}
