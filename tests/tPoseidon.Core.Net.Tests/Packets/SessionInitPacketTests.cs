using tPoseidon.Core.Common;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.Tests.Packets;

public class SessionInitPacketTests
{
    private static readonly byte[] SyntheticPayload =
    {
        0xEF, 0xBE, 0xAD, 0xDE,
        0x00, 0x00, 0x00, 0x00,
        0x78, 0x56, 0x34, 0x12,
    };

    [Fact]
    public void Decode_ProducesExpectedFields()
    {
        var dec = SessionInitPacket.DecodePayload(SyntheticPayload);
        Assert.Equal(0xDEADBEEFu, dec.CharacterId);
        Assert.Equal(0u, dec.Reserved4);
        Assert.Equal(0x12345678u, dec.SessionToken);
    }

    [Fact]
    public void EncodeAfterDecode_IsByteIdentical()
    {
        var dec = SessionInitPacket.DecodePayload(SyntheticPayload);
        var reencoded = dec.EncodePayload();
        Assert.Equal(SyntheticPayload, reencoded);
    }

    [Fact]
    public void Decode_RejectsWrongLength()
    {
        Assert.Throws<InvalidPacketException>(() =>
            SessionInitPacket.DecodePayload(new byte[11]));
        Assert.Throws<InvalidPacketException>(() =>
            SessionInitPacket.DecodePayload(new byte[13]));
    }
}
