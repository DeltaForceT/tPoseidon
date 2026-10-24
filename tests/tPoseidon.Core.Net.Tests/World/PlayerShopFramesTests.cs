using System.Buffers.Binary;
using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.PlayerShop;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class PlayerShopFramesTests
{
    private static readonly byte[] Register =
    [
        1, 1, 0,
        0x55, 0, 0, 0, 0, 0, 0, 0,
        101, 0, 0, 0,
        0x2C, 1, 0, 0,
        10, 0,
        (byte)'H', (byte)'i', 0,
    ];

    private static readonly byte[] BuyRequest =
    [
        7, 0,
        0x55, 0, 0, 0, 0, 0, 0, 0,
        101, 0, 0, 0,
        4, 0,
        0x2C, 1, 0, 0,
    ];

    private static readonly byte[] SignBytes = [7, 0, (byte)'H', (byte)'i', 0, 1];

    [Fact]
    public void RegisterIsTypeCountSlotsThenTitle()
    {
        Assert.True(PlayerShopFrames.TryReadRegister(Register, Encoding.ASCII, out var request));
        Assert.Equal(1, request.ShopType);
        Assert.Equal(new PlayerShopSlot(0x55, 101, 300, 10), request.Slots.Single());
        Assert.Equal("Hi", request.Title);
        Assert.False(PlayerShopFrames.TryReadRegister(Register.AsSpan(0, 20), Encoding.ASCII, out _));
        Assert.Equal(SignBytes, PlayerShopFrames.Sign(7, "Hi", 1, Encoding.ASCII));
    }

    [Fact]
    public void BuyIsKeeperUidItemCountPrice()
    {
        Assert.True(PlayerShopFrames.TryReadBuy(BuyRequest, out var purchase));
        Assert.Equal(new PlayerShopPurchase(7, 0x55, 101, 4, 300), purchase);
        Assert.False(PlayerShopFrames.TryReadBuy(BuyRequest.AsSpan(0, 19), out _));
    }

    [Fact]
    public void ListWritesPriceCountItemAndPropertyBlocks()
    {
        var rare = new TradeItemView(2, 202, 0x66, 1, 1, 7, 0, 3);
        var properties = new[] { new ItemProperty(1, 10000, 5, 0) };
        var frame = PlayerShopFrames.List(new[]
        {
            new PlayerShopListedSlot(new PlayerShopSlot(0x55, 101, 300, 10), null, Array.Empty<ItemProperty>()),
            new PlayerShopListedSlot(new PlayerShopSlot(0x66, 202, 5000, 1), rare, properties),
        });

        Assert.Equal(2u, BinaryPrimitives.ReadUInt32LittleEndian(frame));
        Assert.Equal(300u, BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(4)));
        Assert.Equal(10, BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(8)));
        Assert.Equal(0, frame[10]);
        Assert.Equal(5000u, BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(11)));
        Assert.Equal(2, frame[17]);
        Assert.Equal(202u, BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(18)));
        Assert.Equal(7, frame[33]);
        Assert.Equal(3, frame[35]);
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(36)));
        Assert.Equal(0x66ul, BinaryPrimitives.ReadUInt64LittleEndian(frame.AsSpan(38)));
        Assert.Equal(1, frame[46]);
        Assert.Equal(10000, BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(51)));
        Assert.Equal(46 + 1 + 12, frame.Length);
    }

    [Fact]
    public void BoughtAndSoldCarryMoneyAndOneOperation()
    {
        var op = new BagOperation(QuestResultFrames.Created, 1, 101, 0x77, 4);
        var bought = PlayerShopFrames.Bought(1800, op);
        var sold = PlayerShopFrames.Sold(1200, 4, op);
        Assert.Equal(1800u, BinaryPrimitives.ReadUInt32LittleEndian(bought));
        Assert.Equal(QuestResultFrames.ItemOperation(op), bought[4..]);
        Assert.Equal(4, BinaryPrimitives.ReadUInt16LittleEndian(sold.AsSpan(4)));
        Assert.Equal(QuestResultFrames.ItemOperation(op), sold[6..]);
    }
}
