using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.Tests.Packets;

public class ChannelClientFramesTests
{
    private static readonly string FrameDir = Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data");

    [RequiresDataFact("ref-10006-c2s.frame00-op07D0.plain.bin")]
    public void ChannelIntro_CapturedFrame00_DecodesAndRoundTrips()
    {
        var bytes = File.ReadAllBytes(Path.Combine(FrameDir, "ref-10006-c2s.frame00-op07D0.plain.bin"));
        Assert.Equal(30, bytes.Length);
        var typed = ChannelIntroPacket.DecodePayload(bytes);
        Assert.Equal("tester", typed.Username);
        Assert.Equal("tester", typed.Password);
        Assert.Equal(new byte[] { 0x21, 0x56, 0x5B, 0x56 }, typed.Nonce);
        Assert.Equal(
            new byte[] { 0xC1, 0x8B, 0x1E, 0x06, 0x68, 0xF4, 0x25, 0xED, 0x9C, 0xD1, 0x4E, 0x02 },
            typed.LoginResultToken);
        Assert.Equal(bytes, typed.EncodePayload());
        Assert.Equal(bytes, ChannelIntroPacket.SnapshotDefault.EncodePayload());
    }

    [Fact]
    public void ChannelIntro_LiveDevVariant_RoundTrips24Bytes()
    {
        var typed = new ChannelIntroPacket
        {
            Username = "dev",
            Password = "dev",
            Nonce = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD },
            LoginResultToken = new byte[]
            {
                0x11, 0x22, 0x33, 0x44, 0x55, 0x66,
                0x77, 0x88, 0x99, 0xAA, 0xBB, 0xCC,
            },
        };
        var encoded = typed.EncodePayload();
        Assert.Equal(24, encoded.Length);
        var decoded = ChannelIntroPacket.DecodePayload(encoded);
        Assert.Equal("dev", decoded.Username);
        Assert.Equal("dev", decoded.Password);
        Assert.Equal(typed.Nonce, decoded.Nonce);
        Assert.Equal(typed.LoginResultToken, decoded.LoginResultToken);
    }

    [Fact]
    public void ChannelIntro_DecodesCapturedKr2014Frame()
    {
        var captured = new byte[]
        {
            0x64, 0x65, 0x76, 0x00,
            0x64, 0x65, 0x73, 0x63, 0x00,
            0xCF, 0xB2, 0x97, 0xB2,
            0xC1, 0x8B, 0x1E, 0x06, 0x68, 0xF4, 0x25, 0xED,
            0x9C, 0xD1, 0x4E, 0x02,
            0x00,
        };
        Assert.Equal(26, captured.Length);

        var dec = ChannelIntroPacket.DecodePayload(captured);
        Assert.Equal("dev", dec.Username);
        Assert.Equal("desc", dec.Password);
        Assert.Equal(new byte[] { 0xCF, 0xB2, 0x97, 0xB2 }, dec.Nonce);
        Assert.Equal(
            new byte[] { 0xC1, 0x8B, 0x1E, 0x06, 0x68, 0xF4, 0x25, 0xED, 0x9C, 0xD1, 0x4E, 0x02 },
            dec.LoginResultToken);
        Assert.Single(dec.ExtraTrailerBytes);
        Assert.Equal((byte)0, dec.ExtraTrailerBytes[0]);

        Assert.Equal(captured, dec.EncodePayload());
    }

    [RequiresDataFact("ref-10006-c2s.frame03-op07D6.plain.bin")]
    public void CharCreateRequest_Frame03_DecodesCapturedFieldsPerSection13_14()
    {
        var bytes = File.ReadAllBytes(Path.Combine(FrameDir, "ref-10006-c2s.frame03-op07D6.plain.bin"));
        var typed = CharCreateRequestPacket.DecodePayload(bytes);
        Assert.Equal("Test", typed.Name);
        Assert.Equal((ushort)6, typed.SpeciesSelect);
        Assert.Equal(tPoseidon.Core.Data.Species.Lion, typed.SpeciesEnum);
        Assert.Equal((byte)0x0A, typed.Hair);
        Assert.Equal((byte)6, typed.SpeciesDerived);
        Assert.Equal((byte)2, typed.FType);
        Assert.Equal((byte)4, typed.StatA);
        Assert.Equal((byte)1, typed.StatB);
        Assert.Equal((byte)4, typed.StatC);
        Assert.Equal((byte)1, typed.StatD);
        Assert.Equal(bytes, typed.EncodePayload());
        Assert.Null(typed.ValidateRules());
    }

    [Fact]
    public void CharCreateRequest_ValidatorRejects_StatSumNot10()
    {
        var pkt = new CharCreateRequestPacket
        {
            Name = "X", SpeciesSelect = 1, Hair = 0, SpeciesDerived = 1, FType = 0,
            StatA = 4, StatB = 1, StatC = 1, StatD = 1,
        };
        Assert.NotNull(pkt.ValidateRules());
    }

    [Fact]
    public void CharCreateRequest_ValidatorRejects_FTypeMatchedStatNot4()
    {
        var pkt = new CharCreateRequestPacket
        {
            Name = "X", SpeciesSelect = 1, Hair = 0, SpeciesDerived = 1, FType = 0,
            StatA = 3, StatB = 3, StatC = 2, StatD = 2,
        };
        Assert.NotNull(pkt.ValidateRules());
    }
}
