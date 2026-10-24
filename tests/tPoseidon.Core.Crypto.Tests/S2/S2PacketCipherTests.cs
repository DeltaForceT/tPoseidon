using System.Buffers.Binary;
using tPoseidon.Core.Common;
using tPoseidon.Core.Crypto.S2;
using Xunit;

namespace tPoseidon.Core.Crypto.Tests.S2;

public sealed class S2PacketCipherTests
{
    private static S2PacketCipher MakeIdentityCipher() =>
        new(KeyTable.Identity(), new IdentityDerivation());

    private static byte[] BuildIdentityFrame(
        ushort opcode,
        ushort sequence,
        byte randKey,
        PackingFlags flags,
        ReadOnlySpan<byte> payload,
        int dummyCount = 0)
    {
        var declaredLength = 9 + payload.Length + dummyCount + 2;
        if (declaredLength > ushort.MaxValue)
        {
            throw new ArgumentException("Frame would exceed u16.");
        }

        var frame = new byte[declaredLength];
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(0, 2), (ushort)declaredLength);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2, 2), opcode);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(4, 2), sequence);
        frame[6] = randKey;
        frame[7] = (byte)flags;
        frame[8] = 0;

        payload.CopyTo(frame.AsSpan(9, payload.Length));
        var checksumOff = 9 + payload.Length;

        ushort payloadSum = 0;
        foreach (var b in payload) payloadSum += b;
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(checksumOff, 2), payloadSum);

        return frame;
    }

    [Fact]
    public void Decrypt_IdentityFrame_NoFlags_RecoversOpcodeAndPayload()
    {
        var cipher = MakeIdentityCipher();
        var session = new SessionInfo(initialKey: 0) { IsStrict = false };

        var payload = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        var frame = BuildIdentityFrame(
            opcode: 0x1234,
            sequence: 0x0001,
            randKey: 0xAA,
            flags: PackingFlags.None,
            payload: payload);

        var result = cipher.Decrypt(frame, session);

        Assert.Equal(frame.Length, result.DeclaredLength);
        Assert.Equal(0x1234, result.Opcode);
        Assert.Equal(0x0001, result.SequenceNumber);
        Assert.Equal(PackingFlags.None, result.Flags);
        Assert.Equal(0xAA, result.RandKey);
        Assert.Equal(payload, result.Payload);
    }

    [Fact]
    public void Decrypt_IdentityFrame_WithDummyPadding_StripsPadding()
    {
        var cipher = MakeIdentityCipher();
        var session = new SessionInfo { IsStrict = false };

        var payload = new byte[] { 0x11, 0x22 };
        var frame = BuildIdentityFrame(
            opcode: 0xABCD,
            sequence: 0x0002,
            randKey: 3,
            flags: PackingFlags.DummyPadding,
            payload: payload,
            dummyCount: 3);

        var result = cipher.Decrypt(frame, session);
        Assert.Equal(payload, result.Payload);
    }

    [Fact]
    public void Decrypt_WrongChecksum_Throws()
    {
        var cipher = MakeIdentityCipher();
        var session = new SessionInfo { IsStrict = false };
        var frame = BuildIdentityFrame(
            opcode: 0x0001, sequence: 1, randKey: 1,
            flags: PackingFlags.None, payload: new byte[] { 0x40, 0x41 });
        frame[^1] ^= 0xFF;
        Assert.Throws<InvalidPacketException>(() => cipher.Decrypt(frame, session));
    }

    [Fact]
    public void Decrypt_FrameShorterThanMinimum_Throws()
    {
        var cipher = MakeIdentityCipher();
        var session = new SessionInfo { IsStrict = false };
        Assert.Throws<InvalidPacketException>(() => cipher.Decrypt(new byte[5], session));
    }

    [Fact]
    public void KeyUpdateFlag_MutatesSessionKey_ByDoubleByteChecksumXor()
    {
        var cipher = MakeIdentityCipher();
        var session = new SessionInfo(initialKey: 0x55) { IsStrict = false };

        var payload = new byte[] { 0x10, 0x20, 0x30 };
        ushort expectedChecksum = 0x60;
        var frame = BuildIdentityFrame(
            opcode: 1, sequence: 1, randKey: 1,
            flags: PackingFlags.KeyUpdate, payload: payload);

        var result = cipher.Decrypt(frame, session);
        Assert.Equal(payload, result.Payload);

        byte expected = 0x55;
        expected ^= (byte)(expectedChecksum & 0xFF);
        expected ^= (byte)((expectedChecksum >> 8) & 0xFF);
        Assert.Equal(expected, session.Key);
    }

    [Fact]
    public void KeyUpdateFlag_Clear_LeavesSessionKey()
    {
        var cipher = MakeIdentityCipher();
        var session = new SessionInfo(initialKey: 0x55) { IsStrict = false };

        var frame = BuildIdentityFrame(
            opcode: 1, sequence: 1, randKey: 1,
            flags: PackingFlags.None, payload: new byte[] { 0xFF });

        cipher.Decrypt(frame, session);
        Assert.Equal(0x55, session.Key);
    }

    [Fact]
    public void IsFirstPacket_FlipsAfterFirstSuccessfulDecrypt()
    {
        var cipher = MakeIdentityCipher();
        var session = new SessionInfo { IsStrict = false };
        Assert.True(session.IsFirstPacket);

        cipher.Decrypt(BuildIdentityFrame(1, 1, 1, PackingFlags.None, Array.Empty<byte>()), session);
        Assert.False(session.IsFirstPacket);
    }

    [Fact]
    public void KeyTable_Identity_IsSelfIndex()
    {
        var table = KeyTable.Identity();
        for (var i = 0; i < 4096; i++)
        {
            Assert.Equal((byte)i, table.Lookup(i));
        }
    }

    [Fact]
    public void KeyTable_DummyPair_IsPermutationPerPage()
    {
        var table = KeyTable.Dummy(seed: 42);
        for (var page = 0; page < 16; page++)
        {
            var seen = new HashSet<byte>();
            for (var col = 0; col < 256; col++)
            {
                Assert.True(seen.Add(table.Lookup((page << 8) | col)),
                    $"Page {page} col {col}: duplicate value, not a permutation.");
            }
        }
    }

    [Fact]
    public void KeyTable_WrongSize_Throws()
    {
        Assert.Throws<ArgumentException>(() => new KeyTable(new byte[100]));
        Assert.Throws<ArgumentException>(() => new KeyTable(new byte[4097]));
    }

    [Fact]
    public void RoundTrip_EmptyPayload_NoFlags()
    {
        var cipher = MakeIdentityCipher();
        var sessionEnc = new SessionInfo { IsStrict = false };
        var sessionDec = new SessionInfo { IsStrict = false };

        var frame = cipher.Encrypt(
            opcode: 0x1234, sequence: 0x0001, randKey: 0xAB,
            flags: PackingFlags.None, plaintextPayload: ReadOnlySpan<byte>.Empty,
            session: sessionEnc);

        var decoded = cipher.Decrypt(frame, sessionDec);
        Assert.Equal(0x1234, decoded.Opcode);
        Assert.Equal(0x0001, decoded.SequenceNumber);
        Assert.Equal(0xAB, decoded.RandKey);
        Assert.Empty(decoded.Payload);
    }

    [Fact]
    public void RoundTrip_SmallPayload_NoFlags()
    {
        var cipher = MakeIdentityCipher();
        var sessionEnc = new SessionInfo { IsStrict = false };
        var sessionDec = new SessionInfo { IsStrict = false };

        var payload = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0xCA, 0xFE };
        var frame = cipher.Encrypt(
            opcode: 0x07F2, sequence: 42, randKey: 0x55,
            flags: PackingFlags.None, plaintextPayload: payload,
            session: sessionEnc);

        var decoded = cipher.Decrypt(frame, sessionDec);
        Assert.Equal(0x07F2, decoded.Opcode);
        Assert.Equal(42, decoded.SequenceNumber);
        Assert.Equal(payload, decoded.Payload);
    }

    [Fact]
    public void RoundTrip_PayloadEncryptedFlag()
    {
        var cipher = MakeIdentityCipher();
        var sessionEnc = new SessionInfo { IsStrict = false };
        var sessionDec = new SessionInfo { IsStrict = false };

        var payload = new byte[100];
        for (var i = 0; i < payload.Length; i++) payload[i] = (byte)i;

        var frame = cipher.Encrypt(
            opcode: 1, sequence: 1, randKey: 1,
            flags: PackingFlags.PayloadEncrypted, plaintextPayload: payload,
            session: sessionEnc);

        var decoded = cipher.Decrypt(frame, sessionDec);
        Assert.Equal(payload, decoded.Payload);
    }

    [Fact]
    public void RoundTrip_KeyUpdateFlag_KeysEvolveSymmetrically()
    {
        var cipher = MakeIdentityCipher();
        var sessionEnc = new SessionInfo(initialKey: 0x10) { IsStrict = false };
        var sessionDec = new SessionInfo(initialKey: 0x10) { IsStrict = false };

        var payload = new byte[] { 0x40, 0x40, 0x40 };
        var frame = cipher.Encrypt(
            opcode: 1, sequence: 1, randKey: 1,
            flags: PackingFlags.KeyUpdate, plaintextPayload: payload,
            session: sessionEnc);

        cipher.Decrypt(frame, sessionDec);

        Assert.Equal(sessionEnc.SendKey, sessionDec.RecvKey);
        Assert.NotEqual(0x10, sessionEnc.SendKey);
    }

    [Fact]
    public void RoundTrip_DummyPadding_Stripped()
    {
        var cipher = MakeIdentityCipher();
        var sessionEnc = new SessionInfo { IsStrict = false };
        var sessionDec = new SessionInfo { IsStrict = false };

        var payload = new byte[] { 0x11, 0x22, 0x33 };
        var frame = cipher.Encrypt(
            opcode: 1, sequence: 1, randKey: 5,
            flags: PackingFlags.DummyPadding, plaintextPayload: payload,
            session: sessionEnc, dummyPaddingBytes: 5);

        var decoded = cipher.Decrypt(frame, sessionDec);
        Assert.Equal(payload, decoded.Payload);
    }

    [Fact]
    public void Encrypt_DummyPaddingBytesNonZeroWithoutFlag_Throws()
    {
        var cipher = MakeIdentityCipher();
        var session = new SessionInfo { IsStrict = false };

        Assert.Throws<ArgumentException>(() =>
            cipher.Encrypt(
                opcode: 1, sequence: 1, randKey: 1,
                flags: PackingFlags.None, plaintextPayload: new byte[] { 0x10 },
                session: session, dummyPaddingBytes: 3));
    }
}
