using tPoseidon.Core.Data;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.Tests.Packets;

public sealed class GameItemListRefineTests
{
    [Fact]
    public void ADefaultEquipmentRecordKeepsTheOldBytes()
    {
        var payload = new GameItemListPacket
        {
            Records = new[] { new GameItemListPacket.ItemRecord { Class = 2, ItemIndex = 500014, ItemUid = 5 } },
        }.EncodePayload();

        Assert.Equal(new byte[] { 0x01, 0x00, 0x02, 0x2E, 0xA1, 0x07, 0x00, 0x05, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, payload);
    }

    [Fact]
    public void RefineBytesAndOptionsRoundTrip()
    {
        var record = new GameItemListPacket.ItemRecord
        {
            Class = 2,
            ItemIndex = 500014,
            ItemUid = 5,
            RefineType = 0,
            RefineLevel = 3,
            RefineState = 1,
            MixSlot = 2,
            Options = new[] { new ItemProperty(ItemPropertyTypes.RefineBonus, 10000, 30, 0) },
        };
        var payload = new GameItemListPacket { Records = new[] { record } }.EncodePayload();

        Assert.Equal(2 + 20 + GameItemListPacket.OptionSize, payload.Length);
        var decoded = Assert.Single(GameItemListPacket.DecodePayload(payload).Records);
        Assert.Equal((3, 1, 2), (decoded.RefineLevel, decoded.RefineState, decoded.MixSlot));
        Assert.Equal(record.Options, decoded.Options);
    }
}
