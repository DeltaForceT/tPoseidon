using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.Tests.Packets;

public class NpcSpawnPacketTests
{
    [Fact]
    public void EncodeDecode_RoundTrips_NpcExample()
    {
        var pkt = new NpcSpawnPacket
        {
            EntityId = 0x0071,
            Kind = NpcSpawnPacket.EntityKind.Npc,
            TemplateId = 0x03E9,
            UnknownField6 = 0,
            UnknownField8 = 0,
            SpawnX = 0x02D9,
            SpawnY = 0x0592,
            CurrentX = 0x02D9,
            CurrentY = 0x0592,
            Name = "TestVendor",
            Trailer = new byte[] { 0x00, 0x01, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0E, 0x00, 0x01, 0x00, 0x00, 0x00 },
        };

        var wire = pkt.EncodePayload();
        var round = NpcSpawnPacket.DecodePayload(wire);

        Assert.Equal(pkt.EntityId, round.EntityId);
        Assert.Equal(pkt.Kind, round.Kind);
        Assert.Equal(pkt.TemplateId, round.TemplateId);
        Assert.Equal(pkt.UnknownField6, round.UnknownField6);
        Assert.Equal(pkt.UnknownField8, round.UnknownField8);
        Assert.Equal(pkt.SpawnX, round.SpawnX);
        Assert.Equal(pkt.SpawnY, round.SpawnY);
        Assert.Equal(pkt.CurrentX, round.CurrentX);
        Assert.Equal(pkt.CurrentY, round.CurrentY);
        Assert.Equal(pkt.Name, round.Name);
        Assert.Equal(pkt.Trailer.ToArray(), round.Trailer.ToArray());
    }

    [Fact]
    public void EncodeDecode_RoundTrips_MobExample()
    {
        var trailer = new byte[]
        {
            0x1E, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        };
        var pkt = new NpcSpawnPacket
        {
            EntityId = 0x1D69,
            Kind = NpcSpawnPacket.EntityKind.Mob,
            TemplateId = 0x0860,
            UnknownField6 = 0x0090,
            UnknownField8 = 0x0040,
            SpawnX = 0x04B8,
            SpawnY = 0x11F8,
            CurrentX = 0x04B8,
            CurrentY = 0x11F8,
            Name = "TestMob",
            Trailer = trailer,
        };

        var wire = pkt.EncodePayload();
        var round = NpcSpawnPacket.DecodePayload(wire);
        Assert.Equal(pkt.Name, round.Name);
        Assert.Equal(pkt.TemplateId, round.TemplateId);
        Assert.Equal(NpcSpawnPacket.EntityKind.Mob, round.Kind);
        Assert.Equal(trailer, round.Trailer.ToArray());
    }

    [Fact]
    public void Decode_OfSyntheticBytes_ProducesExpectedFields()
    {
        var original = new NpcSpawnPacket
        {
            EntityId = 0xDEAD,
            Kind = NpcSpawnPacket.EntityKind.Npc,
            TemplateId = 0x1234,
            UnknownField6 = 0xCAFE,
            UnknownField8 = 0xBEEF,
            SpawnX = 0x0100,
            SpawnY = 0x0200,
            CurrentX = 0x0100,
            CurrentY = 0x0200,
            Name = "TestNpc",
            Trailer = new byte[] { 0x80, 0x00, 0x00, 0x00, 0xA5, 0x00 },
        };
        var wire = original.EncodePayload();

        var dec = NpcSpawnPacket.DecodePayload(wire);
        Assert.Equal("TestNpc", dec.Name);
        Assert.Equal(NpcSpawnPacket.EntityKind.Npc, dec.Kind);
        Assert.Equal(0xDEAD, dec.EntityId);
        Assert.Equal(0x1234, dec.TemplateId);
        Assert.Equal(0xCAFE, dec.UnknownField6);
        Assert.Equal(0xBEEF, dec.UnknownField8);
        Assert.Equal(dec.SpawnX, dec.CurrentX);
        Assert.Equal(dec.SpawnY, dec.CurrentY);
        Assert.Equal(0x0100, dec.SpawnX);
        Assert.Equal(0x0200, dec.SpawnY);
    }

    [Theory]
    [InlineData(NpcSpawnPacket.EntityKind.Npc, "ShortName")]
    [InlineData(NpcSpawnPacket.EntityKind.Mob, "AnotherTestMob")]
    [InlineData(NpcSpawnPacket.EntityKind.Npc, "X")]
    [InlineData(NpcSpawnPacket.EntityKind.Mob, "ALongerEntityNameForCoverage")]
    public void EncodeAfterDecode_IsByteIdentical(NpcSpawnPacket.EntityKind kind, string name)
    {
        var original = new NpcSpawnPacket
        {
            EntityId = 0x1000,
            Kind = kind,
            TemplateId = 0x2000,
            UnknownField6 = 0x3000,
            UnknownField8 = 0x4000,
            SpawnX = 0x0500,
            SpawnY = 0x0600,
            CurrentX = 0x0500,
            CurrentY = 0x0600,
            Name = name,
            Trailer = new byte[] { 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
        };
        var wire = original.EncodePayload();

        var dec = NpcSpawnPacket.DecodePayload(wire);
        var reencoded = dec.EncodePayload();
        Assert.Equal(wire, reencoded);
    }

    [Fact]
    public void TheTrailerSpeedIsReadBack()
    {
        var trailer = new byte[NpcSpawnPacket.TrailerSpeedOffset + 1];
        trailer[NpcSpawnPacket.TrailerSpeedOffset] = 0x1C;
        var wire = new NpcSpawnPacket { EntityId = 0x0042, Kind = NpcSpawnPacket.EntityKind.Mob, Name = "Mob", Trailer = trailer }.EncodePayload();

        Assert.Equal(0x1C, NpcSpawnPacket.DecodePayload(wire).TrailerSpeedQuarter);
    }

    [Fact]
    public void AShortTrailerHasNoSpeed()
    {
        var wire = new NpcSpawnPacket { EntityId = 0x0042, Name = "Npc" }.EncodePayload();

        Assert.Equal(0, NpcSpawnPacket.DecodePayload(wire).TrailerSpeedQuarter);
    }
}
