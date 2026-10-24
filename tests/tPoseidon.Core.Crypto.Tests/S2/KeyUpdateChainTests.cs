using tPoseidon.Core.Common;
using tPoseidon.Core.Crypto.S2;

namespace tPoseidon.Core.Crypto.Tests.S2;

public sealed class KeyUpdateChainTests
{
    [Fact]
    public void HundredFrameChain_RoundTripsWithKeyUpdateOnEach()
    {
        var table = KeyTable.Dummy(seed: 9001);
        var derivation = new S2v2010Derivation();
        var cipher = new S2PacketCipher(table, derivation);

        const byte InitialKey = 0x01;
        var encSession = new SessionInfo(InitialKey) { IsStrict = false };
        var decSession = new SessionInfo(InitialKey) { IsStrict = false };

        var allKeysSeen = new HashSet<byte> { InitialKey };
        for (var i = 0; i < 100; i++)
        {
            var payload = new byte[8];
            payload[0] = (byte)(i & 0xFF);
            payload[1] = (byte)(i >> 8);
            payload[7] = (byte)(i * 17);

            var frame = cipher.Encrypt(
                opcode: 0x0018,
                sequence: (ushort)i,
                randKey: (byte)(0x40 + (i & 0x3F)),
                flags: PackingFlags.PayloadEncrypted | PackingFlags.KeyUpdate,
                plaintextPayload: payload,
                session: encSession);

            var decoded = cipher.Decrypt(frame, decSession);

            Assert.Equal(payload, decoded.Payload);
            Assert.Equal(encSession.SendKey, decSession.RecvKey);
            allKeysSeen.Add(decSession.RecvKey);
        }

        Assert.NotEqual(InitialKey, encSession.SendKey);
        Assert.True(allKeysSeen.Count > 30,
            $"Expected diverse session-key drift, only saw {allKeysSeen.Count} distinct values.");
    }

    [Fact]
    public void EncryptDecryptDesyncs_IfDecryptSideMissesAKeyUpdate()
    {
        var table = KeyTable.Dummy(seed: 1234);
        var derivation = new S2v2010Derivation();
        var cipher = new S2PacketCipher(table, derivation);

        var encSession = new SessionInfo(0x01) { IsStrict = false };
        var decSession = new SessionInfo(0x01) { IsStrict = false };

        var frame1 = cipher.Encrypt(
            opcode: 0x0018, sequence: 1, randKey: 0xAA,
            flags: PackingFlags.PayloadEncrypted | PackingFlags.KeyUpdate,
            plaintextPayload: new byte[] { 1, 2, 3, 4 },
            session: encSession);
        cipher.Decrypt(frame1, decSession);
        Assert.Equal(encSession.SendKey, decSession.RecvKey);

        var frame2 = cipher.Encrypt(
            opcode: 0x0018, sequence: 2, randKey: 0xBB,
            flags: PackingFlags.PayloadEncrypted | PackingFlags.KeyUpdate,
            plaintextPayload: new byte[] { 5, 6, 7, 8 },
            session: encSession);
        decSession.RecvKey = 0x99;

        var frame3 = cipher.Encrypt(
            opcode: 0x0018, sequence: 3, randKey: 0xCC,
            flags: PackingFlags.PayloadEncrypted,
            plaintextPayload: new byte[] { 9, 10, 11, 12 },
            session: encSession);
        Assert.Throws<InvalidPacketException>(() =>
            cipher.Decrypt(frame3, decSession));
    }
}
