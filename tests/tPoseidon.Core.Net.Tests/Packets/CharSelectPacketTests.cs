using tPoseidon.Core.Common;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.Tests.Packets;

public class CharSelectPacketTests
{
    private static readonly byte[] SyntheticPayload =
    {
        0xEF, 0xBE, 0xAD, 0xDE,
        0x00, 0x00, 0x00, 0x00,
    };

    [Fact]
    public void Decode_ProducesExpectedFields()
    {
        var dec = CharSelectPacket.DecodePayload(SyntheticPayload);
        Assert.Equal(0xDEADBEEFu, dec.CharacterId);
        Assert.Equal(0u, dec.Reserved4);
    }

    [Fact]
    public void EncodeAfterDecode_IsByteIdentical()
    {
        var dec = CharSelectPacket.DecodePayload(SyntheticPayload);
        var reencoded = dec.EncodePayload();
        Assert.Equal(SyntheticPayload, reencoded);
    }

    [Fact]
    public void Decode_RejectsWrongLength()
    {
        Assert.Throws<InvalidPacketException>(() =>
            CharSelectPacket.DecodePayload(new byte[7]));
        Assert.Throws<InvalidPacketException>(() =>
            CharSelectPacket.DecodePayload(new byte[9]));
    }

    [Fact]
    public void CharacterId_MatchesSessionInit()
    {
        var sessionInit = SessionInitPacket.DecodePayload(new byte[]
        {
            0xEF, 0xBE, 0xAD, 0xDE, 0x00, 0x00, 0x00, 0x00, 0x78, 0x56, 0x34, 0x12,
        });
        var charSelect = CharSelectPacket.DecodePayload(SyntheticPayload);
        Assert.Equal(sessionInit.CharacterId, charSelect.CharacterId);
    }
}
