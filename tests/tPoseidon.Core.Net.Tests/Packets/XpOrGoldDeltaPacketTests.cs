using tPoseidon.Core.Common;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.Tests.Packets;

public class XpOrGoldDeltaPacketTests
{
    [Theory]
    [InlineData(new byte[] { 0x01, 0x06, 0xC8, 0x0A, 0x00, 0x00 }, 0x06, 0x00000AC8u)]
    [InlineData(new byte[] { 0x01, 0x09, 0x30, 0x75, 0x00, 0x00 }, 0x09, 0x00007530u)]
    [InlineData(new byte[] { 0x01, 0x06, 0x00, 0x00, 0x00, 0x00 }, 0x06, 0x00000000u)]
    [InlineData(new byte[] { 0x01, 0x09, 0x00, 0x00, 0x00, 0x00 }, 0x09, 0x00000000u)]
    public void Decode_CapturedSamples(byte[] wire, byte expectedType, uint expectedValue)
    {
        var dec = XpOrGoldDeltaPacket.DecodePayload(wire);
        Assert.Equal((byte)0x01, dec.Header);
        Assert.Equal(expectedType, dec.ResourceType);
        Assert.Equal(expectedValue, dec.Value);
    }

    [Fact]
    public void EncodeDecode_RoundTrip_ArbitraryValue()
    {
        var src = new XpOrGoldDeltaPacket
        {
            Header = 0x01,
            ResourceType = 0x06,
            Value = 123456,
        };
        var dec = XpOrGoldDeltaPacket.DecodePayload(src.EncodePayload());
        Assert.Equal(src.Value, dec.Value);
        Assert.Equal(src.ResourceType, dec.ResourceType);
    }

    [Fact]
    public void DefaultHeader_IsObservedConstant()
    {
        var pkt = new XpOrGoldDeltaPacket();
        Assert.Equal(0x01, pkt.Header);
    }

    [Fact]
    public void Decode_RejectsWrongLength()
    {
        Assert.Throws<InvalidPacketException>(() =>
            XpOrGoldDeltaPacket.DecodePayload(new byte[5]));
        Assert.Throws<InvalidPacketException>(() =>
            XpOrGoldDeltaPacket.DecodePayload(new byte[7]));
    }
}
