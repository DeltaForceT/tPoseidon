using tPoseidon.Core.Crypto.S2;
using tPoseidon.Core.Net.S2;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Core.Net.Tests.Social;

public sealed class SystemLinkFramesTests
{
    [RequiresDataFact("keytable-s2v2010.bin")]
    public void AnUnencryptedFrameIsTheFirstFrameAReceiverAccepts()
    {
        var path = S2ServerHost.DefaultKeyTablePath();
        Assert.True(File.Exists(path));
        var cipher = new S2PacketCipher(new KeyTable(File.ReadAllBytes(path)), new S2v2010Derivation());
        var body = SystemLinkFrames.CharacterAndZone(0x10001F41, 0x0101);

        var frame = cipher.Encrypt(SystemLinkFrames.EnterZone, 0, 0, PackingFlags.None, body, new SessionInfo { IsStrict = false });
        var decoded = cipher.Decrypt(frame, new SessionInfo { IsStrict = false });

        Assert.Equal(SystemLinkFrames.EnterZone, decoded.Opcode);
        Assert.Equal(body, decoded.Payload.ToArray());
    }

    [Fact]
    public void AnOrderKeepsItsZonePrefixAndData()
    {
        var order = new OfficerOrder(7, 9, SystemLinkFrames.OrderNotifyPartyPlayer, 0x1122, new byte[] { 1, 2, 3 });

        var (zone, decoded) = SystemLinkFrames.DecodeOrder(SystemLinkFrames.EncodeOrder(0x0101, order));

        Assert.Equal(0x0101, zone);
        Assert.Equal(order with { Data = decoded.Data }, decoded);
        Assert.Equal(order.Data, decoded.Data);
    }

    [Fact]
    public void TheMirrorIsSeventyEightBytesWithRecipientMasterAndPartyAtTheEnd()
    {
        var mirror = new PartyMirror(new[] { new PartyMirrorEntry(0xAA, 3) }, 0xBB, 0xAA, 0x0C);

        var bytes = SystemLinkFrames.EncodeMirror(mirror);
        var back = SystemLinkFrames.DecodeMirror(bytes);

        Assert.Equal(SystemLinkFrames.MirrorLength, bytes.Length);
        Assert.Equal(0xBBUL, BitConverter.ToUInt64(bytes, 0x3C));
        Assert.Equal(0xAAUL, BitConverter.ToUInt64(bytes, 0x44));
        Assert.Equal(0x0C, BitConverter.ToUInt16(bytes, 0x4C));
        Assert.Equal(mirror.Others, back.Others);
        Assert.Equal((mirror.RecipientCharacterId, mirror.MasterCharacterId, mirror.PartyId), (back.RecipientCharacterId, back.MasterCharacterId, back.PartyId));
    }
}
