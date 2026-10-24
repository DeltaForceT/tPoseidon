using System.Text;
using tPoseidon.Core.Game.PlayerShop;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class PlayerShopSearchFramesTests
{
    private static readonly byte[] QueryBytes = [12, 0, 1, 0, 20, 0, (byte)'p', (byte)'o', 0];
    private static readonly byte[] BuyBytes = [0x34, 0x12, 0x22, 0, 0, 0, 1, 0, 0, 0, 3, 0, 30, 0, 0, 0];
    private static readonly byte[] RowBytes =
    [
        1,
        0x34, 0x12,
        2,
        (byte)'b', (byte)'o', (byte)'b', 0,
        1,
        0x64, 0, 0, 0,
        0x22, 0, 0, 0, 1, 0, 0, 0,
        5, 0,
        10, 0, 0, 0,
        3, 0,
    ];

    [Fact]
    public void QueryAndBuyReadTheClientLayouts()
    {
        Assert.True(PlayerShopSearchFrames.TryReadQuery(QueryBytes, Encoding.ASCII, out var query));
        Assert.Equal(new PlayerShopSearchQuery(12, 0, 1, 20, "po"), query);
        Assert.False(PlayerShopSearchFrames.TryReadQuery(QueryBytes.AsSpan(0, 6), Encoding.ASCII, out _));
        Assert.True(PlayerShopSearchFrames.TryReadBuy(BuyBytes, out var buy));
        Assert.Equal(new PlayerShopRemoteBuy(0x1234, 0x1_0000_0022, 3, 30), buy);
    }

    [Fact]
    public void RowsCarryKeeperTypeNameItemPriceAndCount()
    {
        var row = new PlayerShopSearchRow(0x1234, 2, "bob", 1, 100, 0x1_0000_0022, 5, 10, 3);
        Assert.Equal(RowBytes, PlayerShopSearchFrames.Page([row], Encoding.ASCII));
        var bought = PlayerShopSearchFrames.Bought(500, new BagOperation(QuestResultFrames.Created, 1, 100, 0x1_0000_0044, 3), 0x1_0000_0022, 2);
        Assert.Equal(500u, BitConverter.ToUInt32(bought, 0));
        Assert.Equal(0x1_0000_0022UL, BitConverter.ToUInt64(bought, bought.Length - 10));
        Assert.Equal(2, BitConverter.ToUInt16(bought, bought.Length - 2));
    }
}
