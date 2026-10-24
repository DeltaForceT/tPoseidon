using System.Text;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class ShopFramesTests
{
    [Fact]
    public void OpenedCarriesTheShopIdAndTheGreeting()
    {
        var payload = ShopFrames.Opened(0x1234, "Hi", Encoding.ASCII);

        Assert.Equal(new byte[] { 0x34, 0x12, (byte)'H', (byte)'i', 0x00 }, payload);
    }

    [Fact]
    public void OpenedWritesTheGreetingInTheGivenEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = Encoding.GetEncoding(949);
        const string greeting = "안녕";

        var payload = ShopFrames.Opened(0x0001, greeting, encoding);

        Assert.Equal(encoding.GetBytes(greeting), payload[2..^1]);
        Assert.Equal(0, payload[^1]);
    }

    [Fact]
    public void BuyPricesListItemIdsAndUnitPrices()
    {
        var payload = ShopFrames.BuyPrices(new[] { (2000, 50), (2001, 120) });

        Assert.Equal(new byte[]
        {
            0x02, 0x00,
            0xD0, 0x07, 0x00, 0x00, 0x32, 0x00, 0x00, 0x00,
            0xD1, 0x07, 0x00, 0x00, 0x78, 0x00, 0x00, 0x00,
        }, payload);
    }

    [Fact]
    public void SellRatesStartWithTheShopRateAsAFloat()
    {
        var payload = ShopFrames.SellRates(0.5f, new[] { (9000, 0.25f) });

        Assert.Equal(new byte[]
        {
            0x00, 0x00, 0x00, 0x3F,
            0x01, 0x00,
            0x28, 0x23, 0x00, 0x00, 0x00, 0x00, 0x80, 0x3E,
        }, payload);
    }

    [Fact]
    public void TradedCarriesTheNewMoneyThenTheBagOperations()
    {
        var operations = new[] { new BagOperation(QuestResultFrames.Created, 1, 2000, 0x0000000100000005UL, 3) };

        var payload = ShopFrames.Traded(1234, operations);

        Assert.Equal(new byte[] { 0xD2, 0x04, 0x00, 0x00 }, payload[..4]);
        Assert.Equal(QuestResultFrames.ItemOperations(operations), payload[4..]);
    }

    [Fact]
    public void MoneyFullIsAZeroWord()
    {
        Assert.Equal(new byte[] { 0x00, 0x00 }, ShopFrames.MoneyFull());
    }

    [Fact]
    public void PlayerInShopKeepsTheLowThreeBitsOfTheMoveType()
    {
        var payload = ShopFrames.PlayerInShop(0x2E7E, 1330, 886, 0x0A);

        Assert.Equal(new byte[] { 0x7E, 0x2E, 0x32, 0x05, 0x76, 0x03, 0x02 }, payload);
    }
}
