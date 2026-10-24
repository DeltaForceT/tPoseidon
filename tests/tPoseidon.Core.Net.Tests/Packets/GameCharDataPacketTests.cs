using tPoseidon.Core.Common;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.Tests.Packets;

public class GameCharDataPacketTests
{
    [Fact]
    public void EncodeDecode_RoundTrip_AllFieldsPreserved()
    {
        var src = new GameCharDataPacket
        {
            CharacterId = 0xDEADBEEFCAFEBABEul,
            UserId = 0x123456789ABCDEF0ul,
            Slot = 7,
            Name = "TestChar",
            Species = 0x0205,
            Timestamp = 0x00000000695C4BD0ul,
            StatA = 4,
            StatB = 1,
            StatC = 4,
            StatD = 1,
        };

        var wire = src.EncodePayload();
        Assert.Equal(GameCharDataPacket.PayloadSize, wire.Length);

        var dec = GameCharDataPacket.DecodePayload(wire);
        Assert.Equal(src.CharacterId, dec.CharacterId);
        Assert.Equal(src.UserId, dec.UserId);
        Assert.Equal(src.Slot, dec.Slot);
        Assert.Equal("TestChar", dec.Name);
        Assert.Equal(src.Species, dec.Species);
        Assert.Equal(src.Timestamp, dec.Timestamp);
        Assert.Equal(src.StatA, dec.StatA);
        Assert.Equal(src.StatB, dec.StatB);
        Assert.Equal(src.StatC, dec.StatC);
        Assert.Equal(src.StatD, dec.StatD);
    }

    [Fact]
    public void Encode_TimestampWrittenAtOffset0x2D_NotShiftedBy1()
    {
        var pkt = new GameCharDataPacket
        {
            Name = "X",
            Timestamp = 0x00000000_69ED2F10ul,
        };
        var wire = pkt.EncodePayload();
        Assert.Equal(0x10, wire[0x2D]);
        Assert.Equal(0x2F, wire[0x2E]);
        Assert.Equal(0xED, wire[0x2F]);
        Assert.Equal(0x69, wire[0x30]);
        for (var i = 0x31; i < 0x35; i++)
        {
            Assert.Equal((byte)0, wire[i]);
        }
    }

    [Fact]
    public void Encode_ProducesExactly69Bytes()
    {
        var pkt = new GameCharDataPacket { Name = "X" };
        Assert.Equal(69, pkt.EncodePayload().Length);
    }

    [Fact]
    public void Encode_SpeciesIsMirroredAtJobAndTypeOffsets()
    {
        var pkt = new GameCharDataPacket { Name = "X", Species = 0x0205 };
        var wire = pkt.EncodePayload();
        Assert.Equal(0x05, wire[0x25]);
        Assert.Equal(0x02, wire[0x26]);
        Assert.Equal(0x05, wire[0x27]);
        Assert.Equal(0x02, wire[0x28]);
    }

    [Fact]
    public void Encode_TheTypeIndexAndTheGrowthGraphFollowTheTypeByte()
    {
        var pkt = new GameCharDataPacket
        {
            Name = "X",
            Species = 6,
            FTypeIndex = 2,
            StatA = 4,
            StatB = 1,
            StatC = 3,
            StatD = 2,
        };

        var wire = pkt.EncodePayload();

        Assert.Equal(new byte[] { 0x06, 0x00, 0x06, 0x02, 0x04, 0x01, 0x03, 0x02 }, wire[0x25..0x2D]);
    }

    [Fact]
    public void Encode_NameFieldIsZeroPadded()
    {
        var pkt = new GameCharDataPacket { Name = "Hi" };
        var wire = pkt.EncodePayload();
        Assert.Equal((byte)'H', wire[0x11]);
        Assert.Equal((byte)'i', wire[0x12]);
        for (var i = 0x13; i < 0x21; i++)
        {
            Assert.Equal((byte)0, wire[i]);
        }
    }

    [Fact]
    public void Encode_NameTooLong_Throws()
    {
        var longName = new string('X', GameCharDataPacket.NameFieldBytes);
        Assert.Throws<InvalidOperationException>(() =>
            new GameCharDataPacket { Name = longName }.EncodePayload());
    }

    [Fact]
    public void Decode_RejectsWrongLength()
    {
        Assert.Throws<InvalidPacketException>(() =>
            GameCharDataPacket.DecodePayload(new byte[68]));
        Assert.Throws<InvalidPacketException>(() =>
            GameCharDataPacket.DecodePayload(new byte[70]));
    }

    [Fact]
    public void Decode_TrimsNameAtFirstNul()
    {
        var pkt = new GameCharDataPacket { Name = "ShortName" };
        var wire = pkt.EncodePayload();
        var dec = GameCharDataPacket.DecodePayload(wire);
        Assert.Equal("ShortName", dec.Name);
        Assert.Equal(9, dec.Name.Length);
    }
}
