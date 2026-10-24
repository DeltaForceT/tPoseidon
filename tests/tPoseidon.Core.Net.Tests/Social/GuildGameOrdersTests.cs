using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Core.Net.Tests.Social;

public sealed class GuildGameOrdersTests
{
    [Fact]
    public void EnterMatchRoundTrips()
    {
        var order = new GuildEnterMatch(9, 100, 179, 3, string.Empty, 0, "A", new ulong[] { 1, 2 }, new GuildLeagueRecord(7, 4, 2));
        var bytes = GuildGameOrders.EnterMatch(order, Encoding.ASCII);
        var read = GuildGameOrders.ReadEnterMatch(bytes, Encoding.ASCII);
        Assert.NotNull(read);
        Assert.Equal(
            (order.RoomKey, order.GuildCode, order.MapChoice, order.NoLadderFlag, order.GameType, order.GuildName, order.League),
            (read.RoomKey, read.GuildCode, read.MapChoice, read.NoLadderFlag, read.GameType, read.GuildName, read.League));
        Assert.Equal(order.Members, read.Members);
        Assert.Equal(new byte[] { 7, 0, 0, 0, 4, 0, 0, 0, 2, 0, 0, 0 }, bytes[^GuildGameOrders.LeagueRecordSize..]);
        Assert.Null(GuildGameOrders.ReadEnterMatch(bytes.AsSpan(..^1), Encoding.ASCII));
    }

    [Fact]
    public void ResultRoundTripsAndNoticeCarriesBothGuildNames()
    {
        var begin = new DateTimeOffset(new DateTime(2026, 10, 23, 20, 5, 7, 250, DateTimeKind.Local));
        var end = begin.AddMinutes(20);
        var result = new GuildMatchResult(9, false, 0, new GuildMatchTerms(20, 0, 0), 179, begin, end, 1, 3,
            new[] { new GuildMatchSideResult(100, 3, 4, 5, "A"), new GuildMatchSideResult(200, 3, 5, 4, "B") });
        var bytes = GuildGameOrders.Result(result, Encoding.ASCII);
        Assert.Equal(new byte[] { 9, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 20, 0, 0, 0, 179, 0, 0xEA, 0x07, 10, 0 }, bytes[..22]);
        Assert.Equal(new byte[] { 1, 3 }, bytes[(GuildGameOrders.ResultHeaderSize - 2)..GuildGameOrders.ResultHeaderSize]);
        var read = GuildGameOrders.ReadResult(bytes, Encoding.ASCII);
        Assert.NotNull(read);
        Assert.Equal((result.RoomKey, result.NoLadderChange, result.GameType, result.Terms, result.MapId, result.WinTeam, result.MatchResult),
            (read.RoomKey, read.NoLadderChange, read.GameType, read.Terms, read.MapId, read.WinTeam, read.MatchResult));
        Assert.Equal((begin, end), (read.Begin, read.End));
        Assert.Equal(result.Sides, read.Sides);
        Assert.Equal(new byte[] { 1, 3, (byte)'A', 0, (byte)'B', 0 }, GuildGameOrders.ResultNotice(result, Encoding.ASCII));
    }

    [Fact]
    public void LobbyNoticeFollowsTheOrderLayout()
    {
        Assert.Equal(new byte[] { 0, 179, (byte)'A', 0, 3, 0, 40, 0 }, GuildGameOrders.LobbyNotice(0, 179, "A", 3, 40, Encoding.ASCII));
    }

    [Fact]
    public void StartNoticeWritesBothSidesAfterTheTypeAndMap()
    {
        var bytes = GuildGameOrders.StartNotice(1, 0x01B3, new GuildNoticeSide("A", 3, 0, 40), GuildNoticeSide.Empty, Encoding.ASCII);
        Assert.Equal(new byte[] { 1, 0xB3, (byte)'A', 0, 3, 0, 40, 0, 0, 0, 0, 0, 0 }, bytes);
        Assert.True(GuildGameOrders.TryReadStartNoticeRequest([2], out var gameType));
        Assert.Equal(2, gameType);
    }
}
