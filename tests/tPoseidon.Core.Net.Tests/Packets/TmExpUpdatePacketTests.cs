using tPoseidon.Core.Common;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.Tests.Packets;

public sealed class TmExpUpdatePacketTests
{
    [Fact]
    public void EncodePayload_WritesLeadingByteThenLittleEndianTmExp()
    {
        var bytes = new TmExpUpdatePacket { TmExp = 256 }.EncodePayload();

        Assert.Equal(new byte[] { 0x00, 0x00, 0x01, 0x00, 0x00 }, bytes);
    }

    [Fact]
    public void DecodePayload_RoundTrips()
    {
        var source = new byte[] { 0x01, 0x82, 0x01, 0x00, 0x00 };

        var typed = TmExpUpdatePacket.DecodePayload(source);

        Assert.Equal(1, typed.OpaqueAt0x00Tbd);
        Assert.Equal(386u, typed.TmExp);
        Assert.Equal(source, typed.EncodePayload());
    }

    [Fact]
    public void DecodePayload_RejectsWrongLength()
    {
        Assert.Throws<InvalidPacketException>(() => TmExpUpdatePacket.DecodePayload(new byte[4]));
    }
}
