using tPoseidon.Core.Common;
using tPoseidon.Core.Crypto.S2;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.Tests.Packets;

public class ServerListPacketTests
{
    private static readonly string SnapshotPath = ResolveSnapshotPath();
    private static readonly string KeyTablePath = ResolveKeyTablePath();

    private static string ResolveSnapshotPath() =>
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "lobby-server-list-snapshot.bin");

    private static string ResolveKeyTablePath() =>
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "keytable-s2v2010.bin");

    private static (S2DecryptedPacket Decrypted, byte[] WireBytes, byte InitialKey) LoadAndDecryptSnapshot()
    {
        var wire = File.ReadAllBytes(SnapshotPath);
        var keyBytes = File.ReadAllBytes(KeyTablePath);
        var table = new KeyTable(keyBytes);
        var derivation = new S2v2010Derivation();
        var cipher = new S2PacketCipher(table, derivation);
        const byte initialKey = 0x01;
        var session = new SessionInfo(initialKey) { IsStrict = false };
        var decrypted = cipher.Decrypt(wire, session);
        return (decrypted, wire, initialKey);
    }

    [RequiresDataFact("lobby-server-list-snapshot.bin", "keytable-s2v2010.bin")]
    public void Decode_PlaintextSnapshot_HasExpectedFields()
    {
        var (decrypted, _, _) = LoadAndDecryptSnapshot();
        Assert.Equal((ushort)Opcode.SERVER_LIST, decrypted.Opcode);
        Assert.Equal(ServerListPacket.PayloadSize, decrypted.Payload.Length);

        var typed = ServerListPacket.DecodePayload(decrypted.Payload);
        Assert.Equal("http://www.chinesegamer.com", typed.PublisherUrl);
        Assert.Equal(0x01, typed.OccupiedEntry.ServerId);
        Assert.Equal(0x07D6, typed.OccupiedEntry.FieldA);
        Assert.Equal(1u, typed.OccupiedEntry.Status);
        Assert.Equal(0x0018, typed.OccupiedEntry.FieldB);
        Assert.Equal(0x0015, typed.OccupiedEntry.FieldC);
        Assert.Equal(0x001A, typed.OccupiedEntry.FieldD);
        Assert.Equal(0u, typed.OccupiedEntry.FieldE);
    }

    [RequiresDataFact("lobby-server-list-snapshot.bin", "keytable-s2v2010.bin")]
    public void EncodeAfterDecode_IsPlaintextByteIdentical()
    {
        var (decrypted, _, _) = LoadAndDecryptSnapshot();
        var typed = ServerListPacket.DecodePayload(decrypted.Payload);
        var reencoded = typed.EncodePayload();
        Assert.Equal(decrypted.Payload, reencoded);
    }

    [RequiresDataFact("lobby-server-list-snapshot.bin", "keytable-s2v2010.bin")]
    public void EncryptedFrame_FromTypedEncoder_IsByteIdenticalToSnapshot()
    {
        var (decrypted, wire, initialKey) = LoadAndDecryptSnapshot();
        var typed = ServerListPacket.DecodePayload(decrypted.Payload);
        var plaintext = typed.EncodePayload();

        var keyBytes = File.ReadAllBytes(KeyTablePath);
        var table = new KeyTable(keyBytes);
        var derivation = new S2v2010Derivation();
        var cipher = new S2PacketCipher(table, derivation);
        var session = new SessionInfo(initialKey);

        var reframed = cipher.Encrypt(
            opcode: decrypted.Opcode,
            sequence: decrypted.SequenceNumber,
            randKey: decrypted.RandKey,
            flags: decrypted.Flags,
            plaintextPayload: plaintext,
            session: session);

        Assert.Equal(wire.Length, reframed.Length);
        Assert.Equal(wire, reframed);
    }
}
