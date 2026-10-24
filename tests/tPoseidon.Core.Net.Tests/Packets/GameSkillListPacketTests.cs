using tPoseidon.Core.Common;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.Tests.Packets;

public class GameSkillListPacketTests
{
    [Fact]
    public void EncodeDecode_RoundTrip_EmptyList()
    {
        var pkt = new GameSkillListPacket { Entries = Array.Empty<GameSkillListPacket.Entry>() };
        var wire = pkt.EncodePayload();
        Assert.Equal(2, wire.Length);
        Assert.Equal(0, wire[0]);
        Assert.Equal(0, wire[1]);

        var dec = GameSkillListPacket.DecodePayload(wire);
        Assert.Empty(dec.Entries);
    }

    [Fact]
    public void EncodeDecode_RoundTrip_MultipleEntries()
    {
        var entries = new[]
        {
            new GameSkillListPacket.Entry { SkillId = 0x0BC7, Level = 1 },
            new GameSkillListPacket.Entry { SkillId = 0x0BB9, Level = 0 },
            new GameSkillListPacket.Entry { SkillId = 0x0BBB, Level = 10 },
        };
        var pkt = new GameSkillListPacket { Entries = entries };
        var wire = pkt.EncodePayload();

        Assert.Equal(17, wire.Length);
        Assert.Equal(3, wire[0]);

        var dec = GameSkillListPacket.DecodePayload(wire);
        Assert.Equal(3, dec.Entries.Count);
        Assert.Equal(0x0BC7, dec.Entries[0].SkillId);
        Assert.Equal((byte)1, dec.Entries[0].Level);
        Assert.Equal(0x0BBB, dec.Entries[2].SkillId);
        Assert.Equal((byte)10, dec.Entries[2].Level);
    }

    [Fact]
    public void Decode_OfRealCapturedShape_ExtractsExpectedSkills()
    {
        var capturedShape = new byte[]
        {
            0x0B, 0x00,
            0xC7, 0x0B, 0x01, 0x0F, 0xA4,
            0xB9, 0x0B, 0x00, 0x0F, 0xA4,
            0xBB, 0x0B, 0x0A, 0x0F, 0xA4,
            0xBF, 0x0B, 0x0B, 0x0F, 0xA4,
            0xBE, 0x0B, 0x0B, 0x0F, 0xA4,
            0x9B, 0x13, 0x01, 0x0F, 0xA4,
            0x1D, 0x0C, 0x0B, 0x0F, 0xA4,
            0x26, 0x0C, 0x0A, 0x0F, 0xA4,
            0x27, 0x0C, 0x0A, 0x0F, 0xA4,
            0x25, 0x0C, 0x0A, 0x0F, 0xA4,
            0xC8, 0x0B, 0x0A, 0x0F, 0xA4,
        };

        var dec = GameSkillListPacket.DecodePayload(capturedShape);
        Assert.Equal(11, dec.Entries.Count);
        Assert.Equal(0x0BC7, dec.Entries[0].SkillId);
        Assert.Equal((byte)1, dec.Entries[0].Level);
        Assert.Equal(0x139B, dec.Entries[5].SkillId);
        Assert.Equal((byte)1, dec.Entries[5].Level);

        foreach (var entry in dec.Entries)
        {
            Assert.Equal(0x0F, entry.UnknownByte3);
            Assert.Equal(0xA4, entry.UnknownByte4);
        }

        var reencoded = dec.EncodePayload();
        Assert.Equal(capturedShape, reencoded);
    }

    [Fact]
    public void Decode_RejectsTooShort()
    {
        Assert.Throws<InvalidPacketException>(() =>
            GameSkillListPacket.DecodePayload(new byte[1]));
    }

    [Fact]
    public void Decode_RejectsCountMismatch()
    {
        var bad = new byte[12];
        bad[0] = 5;
        Assert.Throws<InvalidPacketException>(() =>
            GameSkillListPacket.DecodePayload(bad));
    }
}
