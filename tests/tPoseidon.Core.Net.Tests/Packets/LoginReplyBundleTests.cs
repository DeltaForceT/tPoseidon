using tPoseidon.Core.Common;
using tPoseidon.Core.Crypto.S2;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.Tests.Packets;

public class LoginReplyBundleTests
{
    private const byte InitialKey = 0x01;

    private static readonly string SnapshotPath = Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "login-reply-snapshot.bin");
    private static readonly string KeyTablePath = Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "keytable-s2v2010.bin");

    private static (S2PacketCipher Cipher, byte[] WireBytes) LoadFixture()
    {
        var keyBytes = File.ReadAllBytes(KeyTablePath);
        var cipher = new S2PacketCipher(new KeyTable(keyBytes), new S2v2010Derivation());
        return (cipher, File.ReadAllBytes(SnapshotPath));
    }

    private static List<S2DecryptedPacket> WalkStream(S2PacketCipher cipher, byte[] wire)
    {
        var session = new SessionInfo(InitialKey) { IsStrict = false };
        var frames = new List<S2DecryptedPacket>();
        var offset = 0;
        while (offset < wire.Length)
        {
            var slice = new ReadOnlySpan<byte>(wire, offset, wire.Length - offset);
            var pkt = cipher.Decrypt(slice, session);
            frames.Add(pkt);
            offset += pkt.DeclaredLength;
        }
        return frames;
    }

    [RequiresDataFact("login-reply-snapshot.bin", "keytable-s2v2010.bin")]
    public void Stream_DecodesIntoFourExpectedFrames()
    {
        var (cipher, wire) = LoadFixture();
        var frames = WalkStream(cipher, wire);

        Assert.Equal(4, frames.Count);
        Assert.Equal((ushort)Opcode.SESSION_OPEN, frames[0].Opcode);
        Assert.Equal((ushort)Opcode.S2C_OP_1773_TBD, frames[1].Opcode);
        Assert.Equal((ushort)Opcode.CHAR_DATA, frames[2].Opcode);
        Assert.Equal((ushort)Opcode.S2C_OP_2159_TBD, frames[3].Opcode);
        Assert.Empty(frames[0].Payload);
        Assert.Equal(PartyDataUpdatePacket.PayloadSize, frames[1].Payload.Length);
        Assert.Equal(CharDataPacket.PayloadSize, frames[2].Payload.Length);
        Assert.Equal(PassiveSkillGainPacket.PayloadSize, frames[3].Payload.Length);
    }

    [RequiresDataFact("login-reply-snapshot.bin", "keytable-s2v2010.bin")]
    public void Frame0_SessionOpen_TypedRoundTrip()
    {
        var (cipher, wire) = LoadFixture();
        var frames = WalkStream(cipher, wire);
        var typed = SessionOpenPacket.DecodePayload(frames[0].Payload);
        Assert.NotNull(typed);
        Assert.Equal(new byte[] { 0x00, 0x00 }, SessionOpenPacket.EncodePayload());
    }

    [RequiresDataFact("login-reply-snapshot.bin", "keytable-s2v2010.bin")]
    public void Frame1_PartyDataUpdate_TypedRoundTrip()
    {
        var (cipher, wire) = LoadFixture();
        var frames = WalkStream(cipher, wire);
        var typed = PartyDataUpdatePacket.DecodePayload(frames[1].Payload);
        Assert.Equal(0xFF, typed.Flags);
        Assert.Equal(0x00, typed.Reserved1);
        Assert.Equal(0x01, typed.State);
        Assert.Equal(0x00, typed.Reserved2);
        Assert.Equal(frames[1].Payload, typed.EncodePayload());
        Assert.Equal(frames[1].Payload, PartyDataUpdatePacket.NoParty.EncodePayload());
    }

    [RequiresDataFact("login-reply-snapshot.bin", "keytable-s2v2010.bin")]
    public void Frame2_CharData_TypedRoundTrip()
    {
        var (cipher, wire) = LoadFixture();
        var frames = WalkStream(cipher, wire);
        var typed = CharDataPacket.DecodePayload(frames[2].Payload);
        Assert.Equal(frames[2].Payload, typed.EncodePayload());
        Assert.Equal(frames[2].Payload, CharDataPacket.SnapshotDefault.EncodePayload());
    }

    [RequiresDataFact("login-reply-snapshot.bin", "keytable-s2v2010.bin")]
    public void Frame3_PassiveSkillGain_TypedRoundTrip()
    {
        var (cipher, wire) = LoadFixture();
        var frames = WalkStream(cipher, wire);
        var typed = PassiveSkillGainPacket.DecodePayload(frames[3].Payload);
        Assert.Equal(frames[3].Payload, typed.EncodePayload());
        Assert.Equal(frames[3].Payload, PassiveSkillGainPacket.SnapshotDefault.EncodePayload());
    }

    [RequiresDataFact("login-reply-snapshot.bin", "keytable-s2v2010.bin")]
    public void EncryptedBundle_FromTypedEncoders_IsByteIdenticalToSnapshot()
    {
        var (cipher, wire) = LoadFixture();
        var frames = WalkStream(cipher, wire);
        var session = new SessionInfo(InitialKey);

        var rebuilt = new List<byte>(wire.Length);
        AppendFrame(rebuilt, cipher, session, frames[0], frames[0].Payload);
        AppendFrame(rebuilt, cipher, session, frames[1],
            PartyDataUpdatePacket.NoParty.EncodePayload());
        AppendFrame(rebuilt, cipher, session, frames[2],
            CharDataPacket.SnapshotDefault.EncodePayload());
        AppendFrame(rebuilt, cipher, session, frames[3],
            PassiveSkillGainPacket.SnapshotDefault.EncodePayload());

        Assert.Equal(wire.Length, rebuilt.Count);
        Assert.Equal(wire, rebuilt.ToArray());
    }

    private static void AppendFrame(
        List<byte> sink,
        S2PacketCipher cipher,
        SessionInfo session,
        S2DecryptedPacket reference,
        byte[] plaintextPayload)
    {
        var frame = cipher.Encrypt(
            opcode: reference.Opcode,
            sequence: reference.SequenceNumber,
            randKey: reference.RandKey,
            flags: reference.Flags,
            plaintextPayload: plaintextPayload,
            session: session);
        sink.AddRange(frame);
    }
}
