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

    [Fact]
    public void AReleaseListIsACountThenClassUidAndSlot()
    {
        var payload = new byte[]
        {
            2, 0,
            2, 0x12, 0xFE, 0xC8, 0x05, 0x01, 0x00, 0x00, 0x00, 4,
            1, 0x34, 0x12, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 20,
        };

        Assert.True(EquipmentFrames.TryReadReleaseList(payload, out var entries));
        Assert.Equal(
            new[] { new ReleaseEntry(2, 0x0000_0001_05C8_FE12UL, 4), new ReleaseEntry(1, 0x0000_0001_0000_1234UL, 20) },
            entries);
        Assert.Equal(payload, EquipmentFrames.ReleaseList(entries));
    }

    [Fact]
    public void AnEmptyReleaseListIsAccepted()
    {
        Assert.True(EquipmentFrames.TryReadReleaseList(new byte[] { 0, 0 }, out var entries));
        Assert.Empty(entries);
    }

    private static readonly byte[][] MalformedReleaseLists =
    {
        Array.Empty<byte>(),
        new byte[] { 1 },
        new byte[] { 1, 0, 2, 1, 0, 0, 0, 0, 0, 0, 0 },
        new byte[] { 31, 0 },
    };

    [Fact]
    public void AMalformedReleaseListIsRejected()
    {
        foreach (var payload in MalformedReleaseLists) Assert.False(EquipmentFrames.TryReadReleaseList(payload, out _));
    }
}
