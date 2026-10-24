using tPoseidon.Core.Data;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class GuildGameFramesTests
{
    [Fact]
    public void BattleStartFollowsTheClientReader()
    {
        byte[] expected =
        [
            2,
            0x01, 0x03, 3, 0, 0, 0, 0, 0,
            0x02, 0x03, 4, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0,
            1, 0, 0, 0, 0, 0, 0, 0,
        ];
        Assert.Equal(expected, GuildGameFrames.BattleStart(new[] { new GuildGameFighterMode(0x0301, 3), new GuildGameFighterMode(0x0302, 4) }));
    }

    [Fact]
    public void ResultCarriesEachSidesRecordThenLadderChangesAndTotals()
    {
        var result = GuildGameFrames.Result(1, 3, 1, new GuildResultSide(7, 4, 2, 1, -12, 988), new GuildResultSide(3, 3, 0, 0, 12, 1012));
        byte[] expected =
        [
            1, 3, 1,
            7, 0, 0, 0, 4, 0, 0, 0, 2, 0, 0, 0, 1, 0, 0, 0,
            3, 0, 0, 0, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0xF4, 0xFF, 0xFF, 0xFF, 12, 0, 0, 0, 0xDC, 0x03, 0, 0, 0xF4, 0x03, 0, 0,
        ];
        Assert.Equal(expected, result);
        Assert.Equal(new GuildResultSide(7, 4, 2, 0, 0, 0), GuildResultSide.Before(new GuildLeagueRecord(7, 4, 2)));
    }

    [Fact]
    public void LadderChangeRoundTrips()
    {
        var team0 = new GuildResultSide(5, 3, 2, 0, 17, 1017);
        var team1 = new GuildResultSide(9, 1, 8, 0, -15, 985);
        var read = tPoseidon.Core.Net.Social.GuildGameOrders.ReadLadderChange(tPoseidon.Core.Net.Social.GuildGameOrders.LadderChange(44, team0, team1));
        Assert.Equal((44u, team0, team1), read);
        Assert.Null(tPoseidon.Core.Net.Social.GuildGameOrders.ReadLadderChange(new byte[8]));
    }
}
