using System.Buffers.Binary;
using tPoseidon.Core.Common;
using tPoseidon.Core.Crypto;
using Xunit;

namespace tPoseidon.Core.Crypto.Tests;

public sealed class PacketCipherTests
{
    private static PacketCipher BuildCipherWithDummy(int seed = 17, int headerPadding = 0)
    {
        var (enc, dec) = ArraySubstitutionTable.GenerateDummyPair(seed);
        return new PacketCipher(new Crc16(), enc, dec, headerPadding);
    }

    [Fact]
    public void RoundTrip_EmptyPayload_Recovers()
    {
        var cipher = BuildCipherWithDummy();
        var frame = cipher.Encrypt(ReadOnlySpan<byte>.Empty);
        Assert.Equal(4, frame.Length);
        var back = cipher.Decrypt(frame);
        Assert.Empty(back);
    }

    [Fact]
    public void RoundTrip_SmallPayload_Recovers()
    {
        var cipher = BuildCipherWithDummy();
        var payload = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0xCA, 0xFE };
        var frame = cipher.Encrypt(payload);

        Assert.Equal(payload.Length + 4, frame.Length);
        var declared = BinaryPrimitives.ReadUInt16LittleEndian(frame);
        Assert.Equal(frame.Length, declared);

        var back = cipher.Decrypt(frame);
        Assert.Equal(payload, back);
    }

    [Fact]
    public void RoundTrip_LargePayload_Recovers()
    {
        var cipher = BuildCipherWithDummy(seed: 999);
        var payload = new byte[4000];
        for (var i = 0; i < payload.Length; i++)
        {
            payload[i] = (byte)(i * 7 + 3);
        }

        var frame = cipher.Encrypt(payload);
        var back = cipher.Decrypt(frame);
        Assert.Equal(payload, back);
    }

    [Theory]
    [MemberData(nameof(RoundTripShapes))]
    public void RoundTrip_MultipleShapes(byte[] payload)
    {
        var cipher = BuildCipherWithDummy();
        var frame = cipher.Encrypt(payload);
        Assert.Equal(payload, cipher.Decrypt(frame));
    }

    public static TheoryData<byte[]> RoundTripShapes => new()
    {
        Array.Empty<byte>(),
        new byte[] { 0x00 },
        new byte[] { 0xFF, 0xFF },
        new byte[] { 0x01, 0x02, 0x03, 0x04 },
        Enumerable.Range(0, 256).Select(i => (byte)i).ToArray(),
    };

    [Fact]
    public void EncryptedPayload_DoesNotEqualPlaintext_ForNonEmptyInput()
    {
        var cipher = BuildCipherWithDummy(seed: 1);
        var payload = new byte[] { 0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48 };
        var frame = cipher.Encrypt(payload);

        var encryptedBytes = frame[PacketCipher.MinHeaderSize..];
        Assert.NotEqual(payload, encryptedBytes);
    }

    [Fact]
    public void Decrypt_BitFlipInPayload_Throws()
    {
        var cipher = BuildCipherWithDummy();
        var frame = cipher.Encrypt(new byte[] { 0x11, 0x22, 0x33, 0x44 });
        frame[PacketCipher.MinHeaderSize] ^= 0x01;
        Assert.Throws<InvalidPacketException>(() => cipher.Decrypt(frame));
    }

    [Fact]
    public void Decrypt_BitFlipInCrc_Throws()
    {
        var cipher = BuildCipherWithDummy();
        var frame = cipher.Encrypt(new byte[] { 0x11, 0x22, 0x33, 0x44 });
        frame[PacketCipher.LengthFieldSize] ^= 0x01;
        Assert.Throws<InvalidPacketException>(() => cipher.Decrypt(frame));
    }

    [Fact]
    public void Decrypt_LengthMismatch_Throws()
    {
        var cipher = BuildCipherWithDummy();
        var frame = cipher.Encrypt(new byte[] { 0x11, 0x22 });
        BinaryPrimitives.WriteUInt16LittleEndian(frame, (ushort)(frame.Length - 1));
        Assert.Throws<InvalidPacketException>(() => cipher.Decrypt(frame));
    }

    [Fact]
    public void Decrypt_FrameShorterThanHeader_Throws()
    {
        var cipher = BuildCipherWithDummy();
        Assert.Throws<InvalidPacketException>(() => cipher.Decrypt(new byte[] { 0x04, 0x00, 0x00 }));
    }

    [Fact]
    public void RoundTrip_WithHeaderPadding_Recovers()
    {
        var cipher = BuildCipherWithDummy(seed: 50, headerPadding: 4);
        var payload = new byte[] { 0x01, 0x02, 0x03 };
        var frame = cipher.Encrypt(payload);
        Assert.Equal(11, frame.Length);
        Assert.Equal(payload, cipher.Decrypt(frame));
    }

    [Fact]
    public void SameInput_ProducesSameFrame_DeterministicCipher()
    {
        var cipher = BuildCipherWithDummy(seed: 7);
        var payload = new byte[] { 0xAA, 0xBB };
        var a = cipher.Encrypt(payload);
        var b = cipher.Encrypt(payload);
        Assert.Equal(a, b);
    }

    [Fact]
    public void IdentityTables_CrcStillValidates()
    {
        var cipher = new PacketCipher(
            new Crc16(),
            ArraySubstitutionTable.Identity(),
            ArraySubstitutionTable.Identity());
        var payload = new byte[] { 0xDE, 0xAD };
        var frame = cipher.Encrypt(payload);

        Assert.Equal(payload[0], frame[PacketCipher.MinHeaderSize + 0]);
        Assert.Equal(payload[1], frame[PacketCipher.MinHeaderSize + 1]);
        Assert.Equal(payload, cipher.Decrypt(frame));
    }
}
