using tPoseidon.Core.Common;
using tPoseidon.Core.Crypto.S2;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.Tests.Packets;

public class ChannelHandshakeTests
{
    private const byte InitialKey = 0x01;

    private static readonly string SnapshotPath = Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "ref-10006-s2c.bin");
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

    [RequiresDataFact("ref-10006-s2c.bin", "keytable-s2v2010.bin")]
    public void Stream_DecodesIntoElevenExpectedFrames()
    {
        var (cipher, wire) = LoadFixture();
        var frames = WalkStream(cipher, wire);

        Assert.Equal(11, frames.Count);
        Assert.Equal((ushort)Opcode.CHAR_LIST, frames[0].Opcode);
        Assert.Equal((ushort)Opcode.HEARTBEAT_ACK, frames[1].Opcode);
        Assert.Equal((ushort)Opcode.HEARTBEAT_ACK, frames[2].Opcode);
        Assert.Equal((ushort)Opcode.CHAR_CREATED, frames[3].Opcode);
        Assert.Equal((ushort)Opcode.HEARTBEAT_ACK, frames[4].Opcode);
        Assert.Equal((ushort)Opcode.CHANNEL_HANDOFF_REPLY, frames[5].Opcode);
        Assert.Equal((ushort)Opcode.CHANNEL_PRE_HANDOFF, frames[6].Opcode);
        Assert.Equal((ushort)Opcode.SESSION_PARAM_1, frames[7].Opcode);
        Assert.Equal((ushort)Opcode.HEARTBEAT_ACK, frames[8].Opcode);
        Assert.Equal((ushort)Opcode.HEARTBEAT_ACK, frames[9].Opcode);
        Assert.Equal((ushort)Opcode.HEARTBEAT_ACK, frames[10].Opcode);

        Assert.Equal(318, frames[0].Payload.Length);
        Assert.Empty(frames[1].Payload);
        Assert.Equal(154, frames[3].Payload.Length);
        Assert.Equal(ChannelHandoffReplyPacket.MinimumPayloadSize + "192.168.56.102".Length, frames[5].Payload.Length);
        Assert.Equal(ChannelPreHandoffPacket.PayloadSize, frames[6].Payload.Length);
        Assert.Equal(SessionParam1Packet.PayloadSize, frames[7].Payload.Length);
    }

    [RequiresDataFact("ref-10006-s2c.bin", "keytable-s2v2010.bin")]
    public void Frame0_CharList_LegacyCapture_HasOldSchemaLength()
    {
        var (cipher, wire) = LoadFixture();
        var frames = WalkStream(cipher, wire);
        Assert.Equal(318, frames[0].Payload.Length);
        Assert.Throws<tPoseidon.Core.Common.InvalidPacketException>(
            () => CharacterListPacket.DecodePayload(frames[0].Payload));
    }

    [RequiresDataFact("ref-10006-s2c.bin", "keytable-s2v2010.bin")]
    public void Frame3_CharCreated_DecodesCapturedFields()
    {
        var (cipher, wire) = LoadFixture();
        var frames = WalkStream(cipher, wire);
        Assert.Equal(154, frames[3].Payload.Length);
        Assert.Throws<tPoseidon.Core.Common.InvalidPacketException>(
            () => CharCreatedReplyPacket.DecodePayload(frames[3].Payload));
    }

    [RequiresDataFact("ref-10006-s2c.bin", "keytable-s2v2010.bin")]
    public void Frame5_ChannelHandoffReply_ParsesIpAndPort()
    {
        var (cipher, wire) = LoadFixture();
        var frames = WalkStream(cipher, wire);
        var typed = ChannelHandoffReplyPacket.DecodePayload(frames[5].Payload);
        Assert.Equal("192.168.56.102", typed.IpAddress);
        Assert.Equal(23006, typed.GamePort);
        Assert.Equal(frames[5].Payload, typed.EncodePayload());
        Assert.Equal(frames[5].Payload, new ChannelHandoffReplyPacket { BaseBytes = ChannelHandoffReplyPacket.SnapshotBytes, SelectedCharUid = typed.SelectedCharUid, IpAddress = typed.IpAddress, GamePort = typed.GamePort }.EncodePayload());
    }

    [RequiresDataFact("ref-10006-s2c.bin", "keytable-s2v2010.bin")]
    public void Frame6_ChannelPreHandoff_TypedRoundTrip()
    {
        var (cipher, wire) = LoadFixture();
        var frames = WalkStream(cipher, wire);
        var typed = ChannelPreHandoffPacket.DecodePayload(frames[6].Payload);
        Assert.Equal(frames[6].Payload, typed.EncodePayload());
        Assert.Equal(frames[6].Payload, ChannelPreHandoffPacket.SnapshotDefault.EncodePayload());
    }

    [RequiresDataFact("ref-10006-s2c.bin", "keytable-s2v2010.bin")]
    public void Frame7_SessionParam1_ParsesIpAndPort()
    {
        var (cipher, wire) = LoadFixture();
        var frames = WalkStream(cipher, wire);
        var typed = SessionParam1Packet.DecodePayload(frames[7].Payload);
        Assert.Equal("192.168.56.102", typed.IpAddress);
        Assert.Equal(13336u, typed.LoginPort);
        Assert.Equal(frames[7].Payload, typed.EncodePayload());
        Assert.Equal(frames[7].Payload, new SessionParam1Packet { BaseBytes = SessionParam1Packet.SnapshotBytes, SelectedCharUid = typed.SelectedCharUid, IpAddress = typed.IpAddress, LoginPort = typed.LoginPort, SessionToken = typed.SessionToken }.EncodePayload());
    }

    [RequiresDataFact("ref-10006-s2c.bin", "keytable-s2v2010.bin")]
    public void EncryptedBundle_FromTypedEncoders_IsByteIdenticalToSnapshot()
    {
        var (cipher, wire) = LoadFixture();
        var frames = WalkStream(cipher, wire);
        var session = new SessionInfo(InitialKey);

        var charList = frames[0].Payload.ToArray();

        var rebuilt = new List<byte>(wire.Length);
        AppendFrame(rebuilt, cipher, session, frames[0], charList);
        AppendFrame(rebuilt, cipher, session, frames[1], Array.Empty<byte>());
        AppendFrame(rebuilt, cipher, session, frames[2], Array.Empty<byte>());
        AppendFrame(rebuilt, cipher, session, frames[3], frames[3].Payload.ToArray());
        AppendFrame(rebuilt, cipher, session, frames[4], Array.Empty<byte>());
        AppendFrame(rebuilt, cipher, session, frames[5], ChannelHandoffReplyPacket.DecodePayload(frames[5].Payload).EncodePayload());
        AppendFrame(rebuilt, cipher, session, frames[6], ChannelPreHandoffPacket.SnapshotDefault.EncodePayload());
        AppendFrame(rebuilt, cipher, session, frames[7], SessionParam1Packet.DecodePayload(frames[7].Payload).EncodePayload());
        AppendFrame(rebuilt, cipher, session, frames[8], Array.Empty<byte>());
        AppendFrame(rebuilt, cipher, session, frames[9], Array.Empty<byte>());
        AppendFrame(rebuilt, cipher, session, frames[10], Array.Empty<byte>());

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
