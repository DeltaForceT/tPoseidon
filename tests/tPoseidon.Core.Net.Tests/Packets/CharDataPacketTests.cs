using tPoseidon.Core.Common;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.Tests.Packets;

public class CharDataPacketTests
{
    [Fact]
    public void EncodeDecode_RoundTrip_AllFieldsPreserved()
    {
        var src = new CharDataPacket
        {
            RecordHeader = 0xBEEF,
            Unknown2 = 0x0072,
            Reserved4 = 0x55,
            LoginCounter = 0x42,
            Unknown6 = 0x002C,
            Reserved8 = 0x0123456789ABCDEFul,
            Reserved16 = 0xFEDCBA9876543210ul,
            Unknown24 = 0x00CFFAE4,
            Unknown28 = 0x42CFFAE4,
            Unknown32 = 0x0066E513,
            ServerTimestamp1 = 0x31C4E750,
            ServerTimestamp2 = 0x31C4E76C,
            Unknown44 = 0x27574008,
            Unknown48 = 0x06000000,
            Reserved52 = 0xDEADBEEF,
            Unknown56A = 0x5D,
            Unknown56B = 0x8C,
            Unknown56C = 0xE2,
            Reserved59 = 0x12345678,
        };

        var wire = src.EncodePayload();
        Assert.Equal(CharDataPacket.PayloadSize, wire.Length);

        var dec = CharDataPacket.DecodePayload(wire);

        Assert.Equal(src.RecordHeader, dec.RecordHeader);
        Assert.Equal(src.Unknown2, dec.Unknown2);
        Assert.Equal(src.Reserved4, dec.Reserved4);
        Assert.Equal(src.LoginCounter, dec.LoginCounter);
        Assert.Equal(src.Unknown6, dec.Unknown6);
        Assert.Equal(src.Reserved8, dec.Reserved8);
        Assert.Equal(src.Reserved16, dec.Reserved16);
        Assert.Equal(src.Unknown24, dec.Unknown24);
        Assert.Equal(src.Unknown28, dec.Unknown28);
        Assert.Equal(src.Unknown32, dec.Unknown32);
        Assert.Equal(src.ServerTimestamp1, dec.ServerTimestamp1);
        Assert.Equal(src.ServerTimestamp2, dec.ServerTimestamp2);
        Assert.Equal(src.Unknown44, dec.Unknown44);
        Assert.Equal(src.Unknown48, dec.Unknown48);
        Assert.Equal(src.Reserved52, dec.Reserved52);
        Assert.Equal(src.Unknown56A, dec.Unknown56A);
        Assert.Equal(src.Unknown56B, dec.Unknown56B);
        Assert.Equal(src.Unknown56C, dec.Unknown56C);
        Assert.Equal(src.Reserved59, dec.Reserved59);
    }

    [Fact]
    public void Encode_ProducesExactly63Bytes()
    {
        var pkt = new CharDataPacket();
        Assert.Equal(63, pkt.EncodePayload().Length);
    }

    [Fact]
    public void Decode_RejectsWrongLength()
    {
        Assert.Throws<InvalidPacketException>(() =>
            CharDataPacket.DecodePayload(new byte[62]));
        Assert.Throws<InvalidPacketException>(() =>
            CharDataPacket.DecodePayload(new byte[64]));
    }

    [Fact]
    public void EncodeAfterDecode_OfSyntheticBytes_IsByteIdentical()
    {
        var synthetic = new byte[]
        {
            0x01, 0x02, 0x72, 0x00, 0x00, 0x40, 0x2C, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0xE4, 0xFA, 0xCF, 0x00, 0xE4, 0xFA, 0xCF, 0x42,
            0x13, 0xE5, 0x66, 0x00, 0x50, 0xE7, 0xC4, 0x31,
            0x6C, 0xE7, 0xC4, 0x31, 0x08, 0x40, 0x57, 0x27,
            0x00, 0x00, 0x00, 0x06, 0x00, 0x00, 0x00, 0x00,
            0x5D, 0x8C, 0xE2, 0x00, 0x00, 0x00, 0x00,
        };
        var dec = CharDataPacket.DecodePayload(synthetic);
        var reencoded = dec.EncodePayload();
        Assert.Equal(synthetic, reencoded);
    }

    [Fact]
    public void Decode_ExtractsKnownFieldOffsets()
    {
        var bytes = new byte[CharDataPacket.PayloadSize];
        bytes[5] = 0x46;
        bytes[36] = 0x20;
        bytes[37] = 0x7B;
        bytes[38] = 0xC5;
        bytes[39] = 0x31;
        bytes[40] = 0x3C;
        bytes[41] = 0x7B;
        bytes[42] = 0xC5;
        bytes[43] = 0x31;

        var dec = CharDataPacket.DecodePayload(bytes);
        Assert.Equal(0x46, dec.LoginCounter);
        Assert.Equal(0x31C57B20u, dec.ServerTimestamp1);
        Assert.Equal(0x31C57B3Cu, dec.ServerTimestamp2);
    }
}
