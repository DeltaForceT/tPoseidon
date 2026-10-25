using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class CardIdentifyFramesTests
{
    private static readonly byte[] EndedBytes = [3, 0, 0, 0, 0x10, 0x27, 0, 0, 0xE8, 0x03, 0, 0];
    private static readonly byte[] CardBytes = [0xF2, 0x55, 0, 0];
    private static readonly byte[] LifeBytes = [0x1E, 0];
    private static readonly byte[] DealtBytes = [2, 0, 0xFF, 0xFF, 0xFF, 0xFF];
    private static readonly byte[] FlippedBytes = [0xD3, 0x07, 0, 0, 3, 0, 0, 0, 6, 0];
    private static readonly byte[] MatchedBytes = [0xD3, 0x07, 0, 0, 1, 0, 0x3C, 0, 0, 0, 1];
    private static readonly byte[] TakenBytes = [0xD3, 0x07, 0, 0, 1];
    private static readonly byte[] PairRequest = [2, 0, 0, 0, 5, 0, 0, 0];

    [Fact]
    public void MarkerIsTheEntityId()
    {
        Assert.Equal(new byte[] { 0x34, 0x12 }, CardIdentifyFrames.Marker(0x1234));
    }

    [Fact]
    public void EndIsScoreExpTmExp()
    {
        Assert.Equal(EndedBytes, CardIdentifyFrames.Ended(3, 10000, 1000));
        Assert.Equal(12, CardIdentifyFrames.Ended(CardIdentifyFrames.NoScore, 0, 0).Length);
    }

    [Fact]
    public void MonsterCardUsedIsTheItemId()
    {
        Assert.Equal(CardBytes, ItemUseFrames.MonsterCardUsed(22002));
    }

    [Fact]
    public void GameFramesMatchTheServerWriters()
    {
        Assert.Equal(LifeBytes, CardIdentifyFrames.Life(30));
        Assert.Equal(DealtBytes, CardIdentifyFrames.Dealt(2, -1));
        Assert.Equal(FlippedBytes, CardIdentifyFrames.Flipped(2003, 3, 6));
        Assert.Equal(MatchedBytes, CardIdentifyFrames.Matched(2003, 1, 60, true));
        Assert.Equal(TakenBytes, CardIdentifyFrames.Taken(2003, true));
        Assert.True(CardIdentifyFrames.TryReadPair(PairRequest, out var first, out var second));
        Assert.Equal((2, 5), (first, second));
        Assert.True(CardIdentifyFrames.TryReadPosition(PairRequest, out var position));
        Assert.Equal(2, position);
        Assert.True(CardIdentifyFrames.TryReadCard(CardBytes, out var card));
        Assert.Equal(22002u, card);
        Assert.False(CardIdentifyFrames.TryReadPair(PairRequest.AsSpan(0, 7), out _, out _));
    }
}
