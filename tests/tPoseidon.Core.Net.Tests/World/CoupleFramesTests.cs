using System.Text;
using tPoseidon.Core.Game.Wedding;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class CoupleFramesTests
{
    private static readonly Encoding Text = Encoding.Latin1;

    [Fact]
    public void MakeStateCarriesBothReadyBytes()
    {
        Assert.Equal(new byte[] { 1, 0, (byte)'B', (byte)'o', 0, 0x04, 0x00, 1 }, CoupleFrames.MakeState(1, 0, "Bo", 4, 1, Text));
        Assert.Equal(new byte[] { 2, (byte)'A', 0 }, CoupleFrames.Proposed(2, "A", Text));
    }

    [Fact]
    public void ExchangeListWritesItemsThenTitles()
    {
        var row = new MarriageExchangeEntry(39500, "R", 1, 0, 0, default, 30000, 128);
        var title = new MarriageTitleEntry(12, "t", default);
        Assert.Equal(
            new byte[] { 1, 0, 0, 0, 0x4C, 0x9A, 0x80, 0, 0, 0, (byte)'R', 0, 0x30, 0x75, 0, 0, 1, 0, 0, 0, 12, 0, 0, 0 },
            CoupleFrames.ExchangeList([row], [title], Text));
    }

    [Fact]
    public void OptionRequestIsSixteenBytes()
    {
        var bytes = new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0x4C, 0x9A, 0, 0, 0x58, 0x98, 12, 0 };
        Assert.True(CoupleFrames.TryReadOption(bytes, out var request));
        Assert.Equal(new MarriageOptionRequest(1, 39500, 39000, 12), request);
        Assert.False(CoupleFrames.TryReadOption(bytes.AsSpan(0, 15), out _));
    }

    [Fact]
    public void SceneBodiesFollowTheClientReader()
    {
        Assert.Equal(new byte[] { 2, 0, 0, 0, (byte)'G', 0, (byte)'B', 0 }, CoupleFrames.Scene(WeddingScene.StartMessage, "G", "B", "x", "y", Text));
        Assert.Equal(new byte[] { 9, 0, 0, 0, (byte)'B', 0 }, CoupleFrames.Scene(WeddingScene.BrideStartMessage, "G", "B", "x", "y", Text));
        Assert.Equal(new byte[] { 18, 0, 0, 0, (byte)'x', 0 }, CoupleFrames.Scene(WeddingScene.GroomSay, "G", "B", "x", "y", Text));
        Assert.Equal(new byte[] { 7, 0, 0, 0 }, CoupleFrames.Scene(WeddingScene.GroomWalking, "G", "B", "x", "y", Text));
        Assert.Equal(new byte[] { 25, 0, 0, 0, (byte)'G', 0, (byte)'B', 0 }, CoupleFrames.Scene(WeddingScene.SceneEnd, "G", "B", "x", "y", Text));
    }
}
