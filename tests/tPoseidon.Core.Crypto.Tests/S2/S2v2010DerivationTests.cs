using System.Buffers.Binary;
using tPoseidon.Core.Common;
using tPoseidon.Core.Crypto.S2;
using Xunit;

namespace tPoseidon.Core.Crypto.Tests.S2;

public sealed class S2v2010DerivationTests
{
    private static readonly string RealKeyTablePath =
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "data", "keytable-s2v2010.bin");

    private static byte[] MakeHeader(byte[] hdrBytes)
    {
        ArgumentNullException.ThrowIfNull(hdrBytes);
        if (hdrBytes.Length != 9)
        {
            throw new ArgumentException("Header must be exactly 9 bytes.", nameof(hdrBytes));
        }
        var frame = new byte[11];
        Array.Copy(hdrBytes, frame, 9);
        return frame;
    }

    [Fact]
    public void RecoverLength_AppliesXorAndTwoLookups()
    {
        var table = KeyTable.Identity();
        var derivation = new S2v2010Derivation();
        var sessionKey = (byte)0x33;

        var frame = MakeHeader([0xCD, 0xAB, 0, 0, 0, 0, 0, 0, 0]);
        var length = derivation.RecoverLength(frame, sessionKey, table);
        Assert.Equal(0xABCD, length);
    }

    [Fact]
    public void RecoverOpcode_AppliesXorAndTwoLookups()
    {
        var table = KeyTable.Identity();
        var derivation = new S2v2010Derivation();
        var frame = MakeHeader([0, 0, 0xCD, 0xAB, 0, 0, 0, 0, 0]);
        var opcode = derivation.RecoverOpcode(frame, sessionKey: 0xFF, table: table);
        Assert.Equal(0xABCD, opcode);
    }

    [Fact]
    public void RecoverSequence_AppliesXorAndTwoLookups()
    {
        var table = KeyTable.Identity();
        var derivation = new S2v2010Derivation();
        var frame = MakeHeader([0, 0, 0, 0, 0xCD, 0xAB, 0, 0, 0]);
        var sequence = derivation.RecoverSequence(frame, sessionKey: 0x77, table: table);
        Assert.Equal(0xABCD, sequence);
    }

    [Fact]
    public void ValidatePackingFlags_AcceptsValidPatternAndClearsBit1()
    {
        var table = KeyTable.Identity();
        var derivation = new S2v2010Derivation();
        var frame = MakeHeader([0, 0, 0, 0, 0, 0, 0xAB, 0x07, 0]);
        var decoded = derivation.ValidatePackingFlags(frame, table);
        Assert.Equal(0x05, decoded);
    }

    [Fact]
    public void ValidatePackingFlags_RejectsInvalidPattern()
    {
        var table = KeyTable.Identity();
        var derivation = new S2v2010Derivation();
        var frame = MakeHeader([0, 0, 0, 0, 0, 0, 0, 0x10, 0]);
        Assert.Throws<InvalidPacketException>(() =>
            derivation.ValidatePackingFlags(frame, table));
    }

    [Fact]
    public void ValidatePackingFlags_RejectsMissingBit2()
    {
        var table = KeyTable.Identity();
        var derivation = new S2v2010Derivation();
        var frame = MakeHeader([0, 0, 0, 0, 0, 0, 0, 0x0D, 0]);
        Assert.Throws<InvalidPacketException>(() =>
            derivation.ValidatePackingFlags(frame, table));
    }

    [Fact]
    public void RealKeyTable_LoadsAndIs65KbWithExpectedDistribution()
    {
        if (!File.Exists(RealKeyTablePath))
        {
            return;
        }

        var bytes = File.ReadAllBytes(RealKeyTablePath);
        Assert.Equal(KeyTable.RequiredLength, bytes.Length);
    }

    [Theory]
    [InlineData((ushort)11, (ushort)0x0042, (ushort)0, (byte)0x7F, (byte)0x01)]
    [InlineData((ushort)256, (ushort)0x0008, (ushort)42, (byte)0xAB, (byte)0x55)]
    [InlineData((ushort)65535, (ushort)0xFFFF, (ushort)0xFFFF, (byte)0xFF, (byte)0xFF)]
    [InlineData((ushort)11, (ushort)0, (ushort)0, (byte)0, (byte)0)]
    public void EncryptHeader_RoundTripsThroughDerivation(
        ushort length, ushort opcode, ushort sequence, byte randKey, byte sessionKey)
    {
        var table = KeyTable.Dummy(seed: 12345);
        var derivation = new S2v2010Derivation();
        var frame = new byte[Math.Max(11, (int)length)];

        derivation.EncryptHeader(frame, length, opcode, sequence, randKey,
            PackingFlags.PayloadEncrypted, sessionKey, table);

        Assert.Equal(randKey, frame[S2PacketCipher.OffsetRandKey]);

        var decodedFlags = derivation.ValidatePackingFlags(frame, table);
        Assert.Equal((byte)PackingFlags.PayloadEncrypted, decodedFlags);

        Assert.Equal(length, derivation.RecoverLength(frame, sessionKey, table));
        Assert.Equal(opcode, derivation.RecoverOpcode(frame, sessionKey, table));
        Assert.Equal(sequence, derivation.RecoverSequence(frame, sessionKey, table));
    }

    [Fact]
    public void EncryptHeader_RoundTripsValidFlagCombinations()
    {
        var table = KeyTable.Dummy(seed: 67);
        var derivation = new S2v2010Derivation();

        for (var f = 0; f < 16; f++)
        {
            if ((f & 0x02) != 0) continue;
            var flags = (PackingFlags)f;
            var frame = new byte[11];
            derivation.EncryptHeader(frame, length: 11, opcode: 0x1234, sequence: 5,
                randKey: 0x88, flags: flags, sessionKey: 0x42, table);
            var decoded = derivation.ValidatePackingFlags(frame, table);
            Assert.Equal((byte)f, decoded);
        }
    }

    [Fact]
    public void EncryptPayload_RoundTripsThroughDerivation()
    {
        var table = KeyTable.Dummy(seed: 999);
        var derivation = new S2v2010Derivation();

        var plaintext = new byte[64];
        for (var i = 0; i < plaintext.Length; i++) plaintext[i] = (byte)(i * 7 + 13);

        var working = (byte[])plaintext.Clone();
        derivation.EncryptPayload(working, opcode: 0x0008, randKey: 0xAB, table);
        Assert.NotEqual(plaintext, working);

        derivation.DecryptPayload(working, opcode: 0x0008, randKey: 0xAB, table);
        Assert.Equal(plaintext, working);
    }

    [Fact]
    public void EncryptTrailingChecksum_RoundTripsThroughDerivation()
    {
        var table = KeyTable.Dummy(seed: 17);
        var derivation = new S2v2010Derivation();

        var trailer = new byte[] { 0x42, 0x55 };
        var working = (byte[])trailer.Clone();

        derivation.EncryptTrailingChecksum(working, payloadLength: 30,
            opcode: 0x01CF, randKey: 0x77, table);
        derivation.DecryptTrailingChecksum(working, payloadLength: 30,
            opcode: 0x01CF, randKey: 0x77, table);

        Assert.Equal(trailer, working);
    }

    [Fact]
    public void S2PacketCipher_FullEncryptDecryptRoundTrip()
    {
        var table = KeyTable.Dummy(seed: 2024);
        var derivation = new S2v2010Derivation();
        var cipher = new S2PacketCipher(table, derivation);

        var payload = new byte[]
        {
            0x71, 0x00, 0x02, 0x00, 0xE9, 0x03, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0xD9, 0x02, 0x92, 0x05, 0xD9, 0x02,
            0x92, 0x05, 0x44, 0x6F, 0x6E, 0x20, 0x47, 0x69, 0x75, 0x76,
            0x61, 0x6E, 0x6E, 0x69, 0x00, 0x01, 0x80, 0x00,
        };

        var sessionEncrypt = new SessionInfo(0x01) { IsStrict = false };
        var encryptedFrame = cipher.Encrypt(
            opcode: 0x0008,
            sequence: 42,
            randKey: 0x7F,
            flags: PackingFlags.PayloadEncrypted,
            plaintextPayload: payload,
            session: sessionEncrypt);

        var sessionDecrypt = new SessionInfo(0x01) { IsStrict = false };
        var decoded = cipher.Decrypt(encryptedFrame, sessionDecrypt);

        Assert.Equal(0x0008, decoded.Opcode);
        Assert.Equal(42, decoded.SequenceNumber);
        Assert.Equal(0x7F, decoded.RandKey);
        Assert.Equal(PackingFlags.PayloadEncrypted, decoded.Flags);
        Assert.Equal(payload, decoded.Payload);
    }

    [Fact]
    public void S2PacketCipher_RoundTripsWithKeyUpdateFlag()
    {
        var table = KeyTable.Dummy(seed: 88);
        var cipher = new S2PacketCipher(table, new S2v2010Derivation());
        var payload = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        var encSession = new SessionInfo(0x01) { IsStrict = false };
        var decSession = new SessionInfo(0x01) { IsStrict = false };

        var frame = cipher.Encrypt(
            opcode: 0x0042, sequence: 1, randKey: 0xAA,
            flags: PackingFlags.PayloadEncrypted | PackingFlags.KeyUpdate,
            plaintextPayload: payload, session: encSession);

        var decoded = cipher.Decrypt(frame, decSession);
        Assert.Equal(payload, decoded.Payload);

        Assert.Equal(encSession.SendKey, decSession.RecvKey);
        Assert.NotEqual((byte)0x01, encSession.SendKey);
    }
}
