using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.Tests.S2;

public class DynamicNpcSpawnTests
{
    private static readonly byte[] CapturedHeidi =
    {
        0x53, 0x01, 0x02, 0x00, 0x1F, 0x05,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x26, 0x04, 0x1C, 0x08,
        0x26, 0x04, 0x1C, 0x08,
        0x48, 0x65, 0x69, 0x64, 0x69, 0x00,
        0x00, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x02, 0x00, 0x01, 0x00, 0x00, 0x00,
    };

    private static readonly byte[] CapturedBunnyMaid =
    {
        0x4C, 0x01, 0x02, 0x00, 0xF3, 0x03,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0xFA, 0x02, 0xDC, 0x05,
        0xFA, 0x02, 0xDC, 0x05,
        0x42, 0x75, 0x6E, 0x6E, 0x79, 0x20, 0x4D, 0x61, 0x69, 0x64, 0x00,
        0x00, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x03, 0x00, 0x00, 0x00, 0x00, 0x00,
    };

    private static byte[] BuildHeidiViaPacket()
    {
        var trailer = new byte[18];
        trailer[1] = 0x80;
        trailer[12] = 0x02;
        trailer[14] = 0x01;

        return new NpcSpawnPacket
        {
            EntityId = 0x0153,
            Kind = NpcSpawnPacket.EntityKind.Npc,
            TemplateId = 0x051F,
            SpawnX = 0x0426,
            SpawnY = 0x081C,
            CurrentX = 0x0426,
            CurrentY = 0x081C,
            Name = "Heidi",
            Trailer = trailer,
        }.EncodePayload();
    }

    private static byte[] BuildBunnyMaidViaPacket()
    {
        var trailer = new byte[18];
        trailer[1] = 0x80;
        trailer[12] = 0x03;

        return new NpcSpawnPacket
        {
            EntityId = 0x014C,
            Kind = NpcSpawnPacket.EntityKind.Npc,
            TemplateId = 0x03F3,
            SpawnX = 0x02FA,
            SpawnY = 0x05DC,
            CurrentX = 0x02FA,
            CurrentY = 0x05DC,
            Name = "Bunny Maid",
            Trailer = trailer,
        }.EncodePayload();
    }

    [Fact]
    public void DynamicHeidiBuilder_MatchesCapturedBytes()
    {
        Assert.Equal(CapturedHeidi, BuildHeidiViaPacket());
    }

    [Fact]
    public void DynamicBunnyMaidBuilder_MatchesCapturedBytes()
    {
        Assert.Equal(CapturedBunnyMaid, BuildBunnyMaidViaPacket());
    }

    [Fact]
    public void DynamicHeidi_PayloadIs46Bytes()
    {
        Assert.Equal(46, BuildHeidiViaPacket().Length);
    }

    [Fact]
    public void DynamicBunnyMaid_PayloadIs51Bytes()
    {
        Assert.Equal(51, BuildBunnyMaidViaPacket().Length);
    }
}
