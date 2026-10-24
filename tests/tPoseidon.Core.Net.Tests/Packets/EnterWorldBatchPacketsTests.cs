using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.Tests.Packets;

public class EnterWorldBatchPacketsTests
{
    private static readonly string FrameDir = Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data");

    private static byte[] LoadFrame(string filename) =>
        File.ReadAllBytes(Path.Combine(FrameDir, filename));

    [RequiresDataFact("_23006-s2c.frame30-op0024.plain.bin")]
    public void HealthStatus_Frame30_Decodes41Hp()
    {
        var bytes = LoadFrame("_23006-s2c.frame30-op0024.plain.bin");
        Assert.Equal(HealthStatusPacket.PayloadSize, bytes.Length);
        var typed = HealthStatusPacket.DecodePayload(bytes);
        Assert.Equal(41u, typed.CurrentHp);
        Assert.Equal(bytes, typed.EncodePayload());
    }

    [RequiresDataFact("_23006-s2c.frame31-op0025.plain.bin")]
    public void ManaStatus_Frame31_Decodes5Mp()
    {
        var bytes = LoadFrame("_23006-s2c.frame31-op0025.plain.bin");
        Assert.Equal(ManaStatusPacket.PayloadSize, bytes.Length);
        var typed = ManaStatusPacket.DecodePayload(bytes);
        Assert.Equal(5u, typed.CurrentMp);
        Assert.Equal(bytes, typed.EncodePayload());
    }

    [RequiresDataFact("_23006-s2c.frame28-op03A5.plain.bin")]
    public void EntityIdEcho_Frame28_DecodesPlayerEntityId()
    {
        var bytes = LoadFrame("_23006-s2c.frame28-op03A5.plain.bin");
        Assert.Equal(EntityIdEchoPacket.PayloadSize, bytes.Length);
        var typed = EntityIdEchoPacket.DecodePayload(bytes);
        Assert.Equal(0x2E7Eu, typed.EntityId);
        Assert.Equal(bytes, typed.EncodePayload());
    }

    [RequiresDataFact("_23006-s2c.frame02-op00E7.plain.bin")]
    public void GameItemList_Frame02_Decodes4Items()
    {
        var bytes = LoadFrame("_23006-s2c.frame02-op00E7.plain.bin");
        var typed = GameItemListPacket.DecodePayload(bytes);
        Assert.Equal(4, typed.Records.Count);

        Assert.Equal((byte)2, typed.Records[0].Class);
        Assert.Equal(6905u, typed.Records[0].ItemIndex);
        Assert.Equal(0x05C8FE1Fu, typed.Records[0].ItemUid);

        Assert.Equal((byte)2, typed.Records[1].Class);
        Assert.Equal(6955u, typed.Records[1].ItemIndex);
        Assert.Equal(0x05C8FE20u, typed.Records[1].ItemUid);

        Assert.Equal((byte)1, typed.Records[2].Class);
        Assert.Equal(2006u, typed.Records[2].ItemIndex);
        Assert.Equal(0x05C8FE21u, typed.Records[2].ItemUid);
        Assert.Equal((ushort)30, typed.Records[2].StackCount);

        Assert.Equal((byte)1, typed.Records[3].Class);
        Assert.Equal(2206u, typed.Records[3].ItemIndex);
        Assert.Equal((ushort)30, typed.Records[3].StackCount);

        Assert.Equal(bytes, typed.EncodePayload());
    }

    [RequiresDataFact("_23006-s2c.frame03-op00E9.plain.bin")]
    public void GameCharStats_Frame03_PreservesEquippedUidsAndOpaqueBytes()
    {
        var bytes = LoadFrame("_23006-s2c.frame03-op00E9.plain.bin");
        Assert.Equal(GameCharStatsPacket.PayloadSize, bytes.Length);
        var typed = GameCharStatsPacket.DecodePayload(bytes);
        Assert.Equal(0x05C8FE1Fu, typed.EquipSlots[5].ItemUid);
        Assert.Equal(0x05C8FE20u, typed.EquipSlots[6].ItemUid);
        Assert.Equal(bytes, typed.EncodePayload());
    }
}
