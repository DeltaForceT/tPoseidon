using tPoseidon.Core.Common;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.Tests.Packets;

public class LoginRequestPacketTests
{
    [Fact]
    public void EncodeDecode_RoundTrip_ShortCredentials()
    {
        var src = new LoginRequestPacket { Username = "alice", Password = "pw123" };
        var wire = src.EncodePayload();
        Assert.Equal(LoginRequestPacket.PayloadSize, wire.Length);

        var dec = LoginRequestPacket.DecodePayload(wire);
        Assert.Equal("alice", dec.Username);
        Assert.Equal("pw123", dec.Password);
    }

    [Fact]
    public void EncodeDecode_RoundTrip_MaxLengthCredentials()
    {
        var maxUser = new string('A', LoginRequestPacket.UsernameFieldBytes);
        var maxPass = new string('B', LoginRequestPacket.PasswordFieldBytes);
        var src = new LoginRequestPacket { Username = maxUser, Password = maxPass };
        var wire = src.EncodePayload();
        Assert.Equal(LoginRequestPacket.PayloadSize, wire.Length);

        var dec = LoginRequestPacket.DecodePayload(wire);
        Assert.Equal(maxUser, dec.Username);
        Assert.Equal(maxPass, dec.Password);
    }

    [Fact]
    public void Encode_UsernameTooLong_Throws()
    {
        var tooLong = new string('A', LoginRequestPacket.UsernameFieldBytes + 1);
        Assert.Throws<InvalidOperationException>(() =>
            new LoginRequestPacket { Username = tooLong, Password = "p" }.EncodePayload());
    }

    [Fact]
    public void Encode_PasswordTooLong_Throws()
    {
        var tooLong = new string('B', LoginRequestPacket.PasswordFieldBytes + 1);
        Assert.Throws<InvalidOperationException>(() =>
            new LoginRequestPacket { Username = "u", Password = tooLong }.EncodePayload());
    }

    [Fact]
    public void Decode_RejectsWrongLength()
    {
        Assert.Throws<InvalidPacketException>(() =>
            LoginRequestPacket.DecodePayload(new byte[37]));
        Assert.Throws<InvalidPacketException>(() =>
            LoginRequestPacket.DecodePayload(new byte[39]));
        Assert.Throws<InvalidPacketException>(() =>
            LoginRequestPacket.DecodePayload(new byte[45]));
        Assert.Throws<InvalidPacketException>(() =>
            LoginRequestPacket.DecodePayload(new byte[47]));
    }

    [Fact]
    public void EncodeDecode_RoundTrip_Kr2014VariantWithExtras()
    {
        var extras = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };
        var src = new LoginRequestPacket
        {
            Username = "alice",
            Password = "pw123",
            ExtraBytes = extras,
        };
        var wire = src.EncodePayload();
        Assert.Equal(LoginRequestPacket.PayloadSizeKr2014, wire.Length);

        var dec = LoginRequestPacket.DecodePayload(wire);
        Assert.Equal("alice", dec.Username);
        Assert.Equal("pw123", dec.Password);
        Assert.Equal(extras, dec.ExtraBytes);
    }

    [Fact]
    public void Decode_AcceptsCapturedKr2014Frame()
    {
        var captured = new byte[]
        {
            0x67, 0x61, 0x6D, 0x65, 0x69, 0x64, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x64, 0x75, 0x6D, 0x6D, 0x79, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        };
        Assert.Equal(LoginRequestPacket.PayloadSizeKr2014, captured.Length);

        var dec = LoginRequestPacket.DecodePayload(captured);
        Assert.Equal("gameid", dec.Username);
        Assert.Equal("dummy", dec.Password);
        Assert.Equal(8, dec.ExtraBytes.Length);
        Assert.All(dec.ExtraBytes, b => Assert.Equal(0, b));
    }

    [Fact]
    public void Encode_NulPadsShortFields()
    {
        var src = new LoginRequestPacket { Username = "ab", Password = "c" };
        var wire = src.EncodePayload();
        Assert.Equal((byte)'a', wire[0]);
        Assert.Equal((byte)'b', wire[1]);
        for (var i = 2; i < LoginRequestPacket.UsernameFieldBytes; i++)
        {
            Assert.Equal((byte)0, wire[i]);
        }
        Assert.Equal((byte)'c', wire[LoginRequestPacket.UsernameFieldBytes]);
        for (var i = LoginRequestPacket.UsernameFieldBytes + 1; i < LoginRequestPacket.PayloadSize; i++)
        {
            Assert.Equal((byte)0, wire[i]);
        }
    }
}
