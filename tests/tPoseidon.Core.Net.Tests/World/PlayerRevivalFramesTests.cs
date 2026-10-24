using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class PlayerRevivalFramesTests
{
    [Fact]
    public void Resurrected_IsTheRevivedIdThenTheReviverId()
    {
        Assert.Equal(
            new byte[] { 0x01, 0x03, 0x00, 0x00 },
            PlayerRevivalFrames.Resurrected(0x0301, PlayerRevivalFrames.NoReviver));
        Assert.Equal(
            new byte[] { 0x01, 0x03, 0x02, 0x03 },
            PlayerRevivalFrames.Resurrected(0x0301, 0x0302));
    }

    [Fact]
    public void RestartNotice_IsTheMapTwoEmptyWordsAndTheHp()
    {
        Assert.Equal(
            new byte[] { 0x21, 0x00, 0x00, 0x00, 0x00, 0x00, 0x24, 0x00, 0x00, 0x00 },
            PlayerRevivalFrames.RestartNotice(33, 36));
    }

    [Fact]
    public void ItemUsed_IsTheItemUid()
    {
        Assert.Equal(
            new byte[] { 0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01 },
            PlayerRevivalFrames.ItemUsed(0x0102030405060708));
    }

    [Fact]
    public void LeftMap_IsThePlayerId()
    {
        Assert.Equal(new byte[] { 0x01, 0x03 }, PlayerRevivalFrames.LeftMap(0x0301));
    }

    [Fact]
    public void TimingsAreTheServerValues()
    {
        Assert.Equal(1500, PlayerRevivalFrames.CompletionDelay.TotalMilliseconds);
        Assert.Equal(5000, PlayerRevivalFrames.Protection.TotalMilliseconds);
    }
}
