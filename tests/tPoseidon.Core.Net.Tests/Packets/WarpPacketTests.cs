using tPoseidon.Core.Common;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.Tests.Packets;

public class WarpPacketTests
{
    [Fact]
    public void LeaveZoneRequest_5B_RoundTrips()
    {
        var src = new LeaveZoneRequestPacket
        {
            DestZone = 0x0115,
            SourceEntity = 0x0318,
            ChangeType = 0x02,
        };
        var bytes = src.EncodePayload();
        Assert.Equal(LeaveZoneRequestPacket.PayloadSize, bytes.Length);
        Assert.Equal(new byte[] { 0x15, 0x01, 0x18, 0x03, 0x02 }, bytes);

        var got = LeaveZoneRequestPacket.DecodePayload(bytes);
        Assert.Equal(src.DestZone, got.DestZone);
        Assert.Equal(src.SourceEntity, got.SourceEntity);
        Assert.Equal(src.ChangeType, got.ChangeType);
    }

    [Fact]
    public void LeaveZoneRequest_WrongLength_Throws()
    {
        Assert.Throws<InvalidPacketException>(() => LeaveZoneRequestPacket.DecodePayload(new byte[3]));
        Assert.Throws<InvalidPacketException>(() => LeaveZoneRequestPacket.DecodePayload(new byte[6]));
    }

    [Fact]
    public void LeaveZoneAck_MatchesReferenceCaptureBytes()
    {
        var src = new LeaveZoneAckPacket
        {
            Header = 0x000C1F26,
            CharUid = 0x0000_0000_0609_8BA2,
            DestinationIp = "192.168.56.101",
            DestinationPort = 0x000059DE,
            SessionToken = 0x000041BB,
            DestMapId = 0x0094,
            SpawnX = 0x017B,
            SpawnY = 0x0332,
            ChangeType = 0x02,
        };
        var bytes = src.EncodePayload();

        Assert.Equal(42, bytes.Length);
        Assert.Equal(0x26, bytes[0]);
        Assert.Equal(0x1F, bytes[1]);
        Assert.Equal(0x0C, bytes[2]);
        Assert.Equal(0x00, bytes[3]);
        Assert.Equal((byte)'1', bytes[12]);
        Assert.Equal((byte)'9', bytes[13]);
        Assert.Equal(0x00, bytes[26]);
    }

    [Fact]
    public void LeaveZoneAck_RoundTrips_Loopback()
    {
        var src = new LeaveZoneAckPacket
        {
            Header = 0x12345678,
            CharUid = 0xCAFE_BABE_DEAD_BEEFUL,
            DestinationIp = "127.0.0.1",
            DestinationPort = 22006,
            SessionToken = 0xABCDEF01,
            DestMapId = 155,
            SpawnX = 1958,
            SpawnY = 100,
            ChangeType = 2,
        };
        var bytes = src.EncodePayload();
        var got = LeaveZoneAckPacket.DecodePayload(bytes);

        Assert.Equal(src.Header, got.Header);
        Assert.Equal(src.CharUid, got.CharUid);
        Assert.Equal(src.DestinationIp, got.DestinationIp);
        Assert.Equal(src.DestinationPort, got.DestinationPort);
        Assert.Equal(src.SessionToken, got.SessionToken);
        Assert.Equal(src.DestMapId, got.DestMapId);
        Assert.Equal(src.SpawnX, got.SpawnX);
        Assert.Equal(src.SpawnY, got.SpawnY);
        Assert.Equal(src.ChangeType, got.ChangeType);
    }

    [Fact]
    public void LeaveZoneAck_NoNulInIp_DecodeThrows()
    {
        var bytes = new byte[40];
        for (var i = 12; i < 40; i++) bytes[i] = (byte)'X';
        Assert.Throws<InvalidPacketException>(() => LeaveZoneAckPacket.DecodePayload(bytes));
    }

    [Fact]
    public void EnterMap_13B_RoundTrips()
    {
        var src = new EnterMapPacket
        {
            CharId = 0xCAFE_BABE_DEAD_BEEFUL,
            AuxToken = 0x1234,
            Reserved10 = 0,
            ProtocolConst = 0x02,
        };
        var bytes = src.EncodePayload();

        Assert.Equal(EnterMapPacket.PayloadSize, bytes.Length);
        var got = EnterMapPacket.DecodePayload(bytes);
        Assert.Equal(src.CharId, got.CharId);
        Assert.Equal(src.AuxToken, got.AuxToken);
        Assert.Equal(src.ProtocolConst, got.ProtocolConst);
    }

    [Fact]
    public void EnterMap_WrongLength_Throws()
    {
        Assert.Throws<InvalidPacketException>(() => EnterMapPacket.DecodePayload(new byte[10]));
    }

    [Fact]
    public void ChannelHandoffPreambel_6B_OneNonZeroByte()
    {
        var bytes = new ChannelHandoffPreambelPacket().EncodePayload();
        Assert.Equal(ChannelHandoffPreambelPacket.PayloadSize, bytes.Length);
        Assert.Equal(new byte[] { 0x99, 0x00, 0x00, 0x00, 0x00, 0x00 }, bytes);
    }

    [Fact]
    public void OpcodeEnum_HasWarpEntries()
    {
        Assert.Equal(0x01D4, (int)Opcode.C2S_REQUEST_MOVE);
        Assert.Equal(0x009C, (int)Opcode.C2S_ENTER_MAP);
        Assert.Equal(0x0099, (int)Opcode.GAME_UNK_99);
        Assert.Equal(0x009A, (int)Opcode.GAME_UNK_9A);
    }
}
