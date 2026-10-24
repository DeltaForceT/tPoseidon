using tPoseidon.Core.Common;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.Tests.Packets;

public class NpcDialogFollowupPacketTests
{
    [Fact]
    public void EncodeDecode_RoundTrip_PreservesAllFields()
    {
        var src = new NpcDialogFollowupPacket
        {
            EntityId = 0x0153,
            PageId = 0x0007,
            Text = "Page two text.",
            BoundQuestId = 0x04D2,
        };
        var wire = src.EncodePayload();
        Assert.Equal(4 + src.Text.Length + NpcDialogFollowupPacket.TrailerPadBytes + 2, wire.Length);

        var dec = NpcDialogFollowupPacket.DecodePayload(wire);
        Assert.Equal(src.EntityId, dec.EntityId);
        Assert.Equal(src.PageId, dec.PageId);
        Assert.Equal(src.Text, dec.Text);
        Assert.Equal(src.BoundQuestId, dec.BoundQuestId);
    }

    [Fact]
    public void Encode_TrailerHasThreeNulsThenQuestIdLittleEndian()
    {
        var pkt = new NpcDialogFollowupPacket
        {
            EntityId = 0x0153,
            PageId = 0,
            Text = "",
            BoundQuestId = 0x04D2,
        };
        var wire = pkt.EncodePayload();
        Assert.Equal(9, wire.Length);
        Assert.Equal(0x00, wire[4]);
        Assert.Equal(0x00, wire[5]);
        Assert.Equal(0x00, wire[6]);
        Assert.Equal(0xD2, wire[7]);
        Assert.Equal(0x04, wire[8]);
    }

    [Fact]
    public void Decode_RejectsTooShortPayload()
    {
        Assert.Throws<InvalidPacketException>(() =>
            NpcDialogFollowupPacket.DecodePayload(new byte[8]));
    }

    [Fact]
    public void Encode_EmptyTextWithBoundQuestZero_ProducesMinimalFrame()
    {
        var pkt = new NpcDialogFollowupPacket
        {
            EntityId = 0x0153,
            PageId = 0,
            Text = string.Empty,
            BoundQuestId = 0,
        };
        var wire = pkt.EncodePayload();
        Assert.Equal(9, wire.Length);
        var dec = NpcDialogFollowupPacket.DecodePayload(wire);
        Assert.Equal((ushort)0x0153, dec.EntityId);
        Assert.Equal((ushort)0, dec.PageId);
        Assert.Equal(string.Empty, dec.Text);
        Assert.Equal((ushort)0, dec.BoundQuestId);
    }

    [Fact]
    public void Encode_MatchesCapturedReferenceTrailerLayout()
    {
        var pkt = new NpcDialogFollowupPacket
        {
            EntityId = 0x0153,
            PageId = 0x0008,
            Text = "is me!\0",
            BoundQuestId = 1233,
        };
        var wire = pkt.EncodePayload();
        Assert.Equal(0x00, wire[^5]);
        Assert.Equal(0x00, wire[^4]);
        Assert.Equal(0x00, wire[^3]);
        Assert.Equal(0xD1, wire[^2]);
        Assert.Equal(0x04, wire[^1]);
    }
}
