using System.Text;
using tPoseidon.Core.Game.PlayerShop;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class PlayerShopHelperFramesTests
{
    [Fact]
    public void QueryIsOneNulString()
    {
        Assert.True(PlayerShopHelperFrames.TryReadQuery([(byte)'a', (byte)'b', 0], Encoding.ASCII, out var query));
        Assert.Equal("ab", query);
        Assert.False(PlayerShopHelperFrames.TryReadQuery([(byte)'a'], Encoding.ASCII, out _));
        Assert.False(PlayerShopHelperFrames.TryReadQuery([], Encoding.ASCII, out _));
    }

    [Fact]
    public void ReplyIsCountThenNameKeeperTitlePriceAndSpot()
    {
        var reply = PlayerShopHelperFrames.Reply([new PlayerShopHelperRow("Bo", "Kay", "Hi", 300, 0x0102, 0x0304)], Encoding.ASCII);
        Assert.Equal(new byte[]
        {
            1, 0,
            (byte)'B', (byte)'o', 0, (byte)'K', (byte)'a', (byte)'y', 0, (byte)'H', (byte)'i', 0,
            0x2C, 1, 0, 0, 2, 1, 4, 3,
        }, reply);
        Assert.Equal(new byte[] { 0, 0 }, PlayerShopHelperFrames.Reply([], Encoding.ASCII));
    }

    [Fact]
    public void ReplyStopsBeforeTheBufferMargin()
    {
        var row = new PlayerShopHelperRow(new string('i', 100), new string('k', 100), new string('t', 100), 1, 1, 1);
        var reply = PlayerShopHelperFrames.Reply(Enumerable.Repeat(row, 20), Encoding.ASCII);
        var expected = (PlayerShopHelperRules.BufferSize - PlayerShopHelperRules.BufferMargin - PlayerShopHelperRules.CountSize - 1) / 311;
        Assert.Equal(expected, BitConverter.ToUInt16(reply, 0));
        Assert.True(reply.Length < PlayerShopHelperRules.BufferSize - PlayerShopHelperRules.BufferMargin);
    }
}
