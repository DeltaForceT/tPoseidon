using tPoseidon.Core.Common;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.Tests.Packets;

public class QuestPacketsTests
{
    [Fact]
    public void QuestAccepted_HeidiQuest1233_EncodesQuestIdLittleEndian()
    {
        var pkt = new QuestAcceptedPacket
        {
            QuestId = 1233,
            QuestName = "Trickster Registration",
        };
        var wire = pkt.EncodePayload();
        Assert.Equal(0xD1, wire[0]);
        Assert.Equal(0x04, wire[1]);
        Assert.Equal(0x00, wire[2]);
        Assert.Equal(0x00, wire[3]);
    }

    [Fact]
    public void QuestAccepted_StateField_DefaultsTo1AtOffset4()
    {
        var pkt = new QuestAcceptedPacket
        {
            QuestId = 1233,
            QuestName = "Trickster Registration",
        };
        var wire = pkt.EncodePayload();
        Assert.Equal(0x01, wire[4]);
        Assert.Equal(0x00, wire[5]);
    }

    [Fact]
    public void QuestAccepted_NameStartsAt0x06_NulTerminated()
    {
        var pkt = new QuestAcceptedPacket
        {
            QuestId = 1233,
            QuestName = "Trickster Registration",
        };
        var wire = pkt.EncodePayload();
        Assert.Equal((byte)'T', wire[6]);
        Assert.Equal((byte)'r', wire[7]);
        Assert.Equal(0x00, wire[6 + 22]);
    }

    [Fact]
    public void QuestAccepted_PadsToReferenceCapturedSize_For1233()
    {
        var pkt = new QuestAcceptedPacket
        {
            QuestId = 1233,
            QuestName = "Trickster Registration",
        };
        Assert.Equal(67, pkt.EncodePayload().Length);
    }

    [Fact]
    public void QuestAccepted_RoundTrip_PreservesAllFields()
    {
        var src = new QuestAcceptedPacket
        {
            QuestId = 1234,
            State = 0x0001,
            QuestName = "Prove Your Spirit",
        };
        var wire = src.EncodePayload();
        var dec = QuestAcceptedPacket.DecodePayload(wire);
        Assert.Equal(src.QuestId, dec.QuestId);
        Assert.Equal(src.State, dec.State);
        Assert.Equal(src.QuestName, dec.QuestName);
    }

    [Fact]
    public void QuestAccepted_Decode_TooShort_Throws()
    {
        Assert.Throws<InvalidPacketException>(() =>
            QuestAcceptedPacket.DecodePayload(new byte[6]));
    }

    [Fact]
    public void QuestCompleted_EmitsExactly4Bytes_QuestIdLittleEndian()
    {
        var pkt = new QuestCompletedPacket { QuestId = 1233 };
        var wire = pkt.EncodePayload();
        Assert.Equal(4, wire.Length);
        Assert.Equal(new byte[] { 0xD1, 0x04, 0x00, 0x00 }, wire);
    }

    [Fact]
    public void QuestCompleted_RoundTrip()
    {
        var src = new QuestCompletedPacket { QuestId = 1234 };
        var dec = QuestCompletedPacket.DecodePayload(src.EncodePayload());
        Assert.Equal(src.QuestId, dec.QuestId);
    }

    [Fact]
    public void QuestCompleted_Decode_WrongLength_Throws()
    {
        Assert.Throws<InvalidPacketException>(() =>
            QuestCompletedPacket.DecodePayload(new byte[3]));
        Assert.Throws<InvalidPacketException>(() =>
            QuestCompletedPacket.DecodePayload(new byte[5]));
    }

    [Fact]
    public void DialogStateAck_Default_Emits7F00()
    {
        var pkt = new DialogStateAckPacket();
        var wire = pkt.EncodePayload();
        Assert.Equal(new byte[] { 0x7F, 0x00 }, wire);
    }

    [Fact]
    public void DialogStateAck_RoundTrip()
    {
        var src = new DialogStateAckPacket { Marker = DialogStateAckPacket.StandardMarker };
        var dec = DialogStateAckPacket.DecodePayload(src.EncodePayload());
        Assert.Equal(src.Marker, dec.Marker);
    }

    [Fact]
    public void DialogStateAck_Decode_WrongLength_Throws()
    {
        Assert.Throws<InvalidPacketException>(() =>
            DialogStateAckPacket.DecodePayload(new byte[1]));
        Assert.Throws<InvalidPacketException>(() =>
            DialogStateAckPacket.DecodePayload(new byte[3]));
    }
}
