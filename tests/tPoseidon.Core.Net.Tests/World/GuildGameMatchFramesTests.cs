using System.Text;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class GuildGameMatchFramesTests
{
    private static readonly Encoding Text = Encoding.ASCII;
    private static readonly GuildScoreSide[] Sides = { new(100, "A"), new(200, "B") };

    private static byte[] Side(byte guildCode, byte[] nameWithNul) =>
        [0, 0, 0, 0, guildCode, 0, 0, 0, 0, 0, 0, 0, .. nameWithNul, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

    private static byte[] RoomBlock(byte gameType, byte[] first, byte[] second) =>
        [7, 0, 0, 0, gameType, 0, 0, 0, 0, 0, 0, 0, 0, 0, .. first, .. second, 0];

    private static byte[] RoomBlock(byte gameType) => RoomBlock(gameType, Side(100, [(byte)'A', 0]), Side(200, [(byte)'B', 0]));

    private static readonly byte[] Keys = [0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0];

    [Fact]
    public void TeamMatchKillNoticeIsKillerVictimAndTheRespawnFlag()
    {
        byte[] expected = [0x01, 0x11, 0, 0, 0, 0, 0, 0, 0x02, 0x22, 0, 0, 0, 0, 0, 0, 1];
        Assert.Equal(expected, GuildGameFrames.Kill(0, 0x1101, 0x2202, 1));
    }

    [Fact]
    public void DeathMatchKillNoticeHasNoRespawnFlag()
    {
        byte[] expected = [0x01, 0x11, 0, 0, 0, 0, 0, 0, 0x02, 0x22, 0, 0, 0, 0, 0, 0];
        Assert.Equal(expected, GuildGameFrames.Kill(1, 0x1101, 0x2202, 0));
    }

    [Fact]
    public void TeamMatchScoreBoardCarriesKillsAndDeathsPerFighter()
    {
        var entry = new GuildScoreEntry(1, 2, 55, "Zed", 0x1122, 0, 3, 1, false);
        var frame = GuildGameFrames.ScoreBoard(7, 0, Sides, 1200, new[] { entry }, Text);
        byte[] body =
        [
            0xB0, 0x04, 0, 0,
            1, 0,
            1, 2, 0, 0x37, 0, (byte)'Z', (byte)'e', (byte)'d', 0, 0x22, 0x11, 0, 0, 0, 0, 0, 0, 0, 3, 0, 1, 0,
        ];
        Assert.Equal([.. RoomBlock(0), .. body, .. Keys], frame);
    }

    [Fact]
    public void DeathMatchScoreBoardCarriesKillsAndTheDownFlag()
    {
        var entry = new GuildScoreEntry(0, 1, 40, "Ann", 0x0A, 0, 2, 0, true);
        var frame = GuildGameFrames.ScoreBoard(7, 1, Sides, 90, new[] { entry }, Text);
        byte[] body =
        [
            90, 0, 0, 0,
            1, 0,
            0, 1, 0, 40, 0, (byte)'A', (byte)'n', (byte)'n', 0, 0x0A, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 1,
        ];
        Assert.Equal([.. RoomBlock(1), .. body, .. Keys], frame);
    }

    [Fact]
    public void ASideWithoutAGuildIsWrittenEmpty()
    {
        var frame = GuildGameFrames.ScoreBoard(7, 0, new[] { Sides[0] }, 5, Array.Empty<GuildScoreEntry>(), Text);
        byte[] body = [5, 0, 0, 0, 0, 0];
        Assert.Equal([.. RoomBlock(0, Side(100, [(byte)'A', 0]), Side(0, [0])), .. body, .. Keys], frame);
    }
}
