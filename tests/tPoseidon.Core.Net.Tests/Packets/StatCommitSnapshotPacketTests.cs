using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.Tests.Packets;

public class StatCommitSnapshotPacketTests
{
    [Fact]
    public void EncodePayload_ProducesFixed45Bytes()
    {
        var pkt = new StatCommitSnapshotPacket();
        var bytes = pkt.EncodePayload();
        Assert.Equal(StatCommitSnapshotPacket.PayloadSize, bytes.Length);
        Assert.Equal(45, bytes.Length);
    }

    [Fact]
    public void EncodePayload_RoundTripsReferenceCapturedBytes()
    {
        var pkt = new StatCommitSnapshotPacket
        {
            ApAccPts = 16,
            AcAccPts = 16,
            DxAccPts = 16,
            MaxMpAccPts = 4,
            MaAccPts = 4,
            MdAccPts = 4,
            MaxWtAccPts = 4,
            DaAccPts = 4,
            LkAccPts = 4,
            MaxHpAccPts = 16,
            DpAccPts = 16,
            HvAccPts = 16,
            AttrPts = 16,
            UnknownU16At26 = 1,
            TmLevel = 1,
            Level = 5,
            UnknownByteAt32 = 1,
            MaxHp = 240,
            MaxMp = 110,
            MaxWt = 12,
        };

        var actual = pkt.EncodePayload();

        var expected = new byte[]
        {
            0x10, 0x00, 0x10, 0x00, 0x10, 0x00,
            0x04, 0x00, 0x04, 0x00, 0x04, 0x00, 0x04, 0x00, 0x04, 0x00, 0x04, 0x00,
            0x10, 0x00, 0x10, 0x00, 0x10, 0x00,
            0x10, 0x00,
            0x01, 0x00, 0x01, 0x00,
            0x05, 0x00,
            0x01,
            0xF0, 0x00, 0x00, 0x00,
            0x6E, 0x00, 0x00, 0x00,
            0x0C, 0x00, 0x00, 0x00,
        };

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EncodePayload_RoundTripsLevelUpCapture_20260502_145138()
    {
        var pkt = new StatCommitSnapshotPacket
        {
            ApAccPts = 12, AcAccPts = 12, DxAccPts = 12,
            MaxMpAccPts = 3, MaAccPts = 3, MdAccPts = 3,
            MaxWtAccPts = 3, DaAccPts = 3, LkAccPts = 3,
            MaxHpAccPts = 12, DpAccPts = 12, HvAccPts = 12,
            AttrPts = 12,
            UnknownU16At26 = 0,
            TmLevel = 0,
            Level = 4,
            UnknownByteAt32 = 1,
            MaxHp = 210,
            MaxMp = 80,
            MaxWt = 40,
        };

        var actual = pkt.EncodePayload();

        var expected = new byte[]
        {
            0x0C, 0x00, 0x0C, 0x00, 0x0C, 0x00,
            0x03, 0x00, 0x03, 0x00, 0x03, 0x00, 0x03, 0x00, 0x03, 0x00, 0x03, 0x00,
            0x0C, 0x00, 0x0C, 0x00, 0x0C, 0x00,
            0x0C, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x04, 0x00,
            0x01,
            0xD2, 0x00, 0x00, 0x00,
            0x50, 0x00, 0x00, 0x00,
            0x28, 0x00, 0x00, 0x00,
        };

        Assert.Equal(expected, actual);
    }
}
