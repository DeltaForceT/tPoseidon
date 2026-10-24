using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class EquipmentFramesTests
{
    [Fact]
    public void ARequestIsAWholeUidAndASlot()
    {
        var payload = new byte[] { 0x12, 0xFE, 0xC8, 0x05, 0x01, 0x00, 0x00, 0x00, 0x01 };

        Assert.True(EquipmentFrames.TryReadRequest(payload, out var uid, out var slot));
        Assert.Equal(0x0000_0001_05C8_FE12UL, uid);
        Assert.Equal((byte)1, slot);
    }

    [Fact]
    public void AShortRequestIsRejected()
    {
        Assert.False(EquipmentFrames.TryReadRequest(new byte[8], out _, out _));
    }

    [Fact]
    public void TheItemInSlotAnswerEchoesTheRequestLayout()
    {
        Assert.Equal(
            new byte[] { 0x12, 0xFE, 0xC8, 0x05, 0x01, 0x00, 0x00, 0x00, 0x01 },
            EquipmentFrames.ItemInSlot(0x0000_0001_05C8_FE12UL, 1));
    }

    [Fact]
    public void ARefusalCarriesTheUidOnly()
    {
        Assert.Equal(new byte[] { 0x12, 0xFE, 0xC8, 0x05, 0x01, 0x00, 0x00, 0x00 }, EquipmentFrames.Refused(0x0000_0001_05C8_FE12UL));
    }

    [Fact]
    public void TheLookFramesNameThePlayerAndTheItem()
    {
        Assert.Equal(
            new byte[] { 0xF9, 0x21, 0xD6, 0x0B, 0x00, 0x00, 0xD6, 0x0B, 0x00, 0x00 },
            EquipmentFrames.LookAdded(0x21F9, 3030, 3030));
        Assert.Equal(new byte[] { 0xF9, 0x21, 0xD6, 0x0B, 0x00, 0x00 }, EquipmentFrames.LookRemoved(0x21F9, 3030));
    }
}
