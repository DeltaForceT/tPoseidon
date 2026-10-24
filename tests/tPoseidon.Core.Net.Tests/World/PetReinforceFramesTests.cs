using tPoseidon.Core.Data;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class PetReinforceFramesTests
{
    [Fact]
    public void RequestIsFourUids()
    {
        var body = new byte[32];
        body[0] = 1;
        body[8] = 2;
        body[16] = 3;
        body[24] = 4;

        Assert.True(PetReinforceFrames.TryReadRequest(body, out var request));
        Assert.Equal(new PetReinforceRequest(1, 2, 3, 4), request);
        Assert.False(PetReinforceFrames.TryReadRequest(body.AsSpan(0, 31), out _));
    }

    [Fact]
    public void ResultIsMaterialPetPropertyThenTwoAids()
    {
        var material = new BagOperation(QuestResultFrames.Removed, 1, 63000, 0x11, 0);
        var empty = new BagOperation(0, 1, 0, 0, 0);
        var frame = PetReinforceFrames.Result(material, 0x22, new ItemProperty(3, 10005, 12, 63000), new[] { empty, empty });

        var op = QuestResultFrames.ItemOperation(material);
        Assert.Equal(op, frame[..op.Length]);
        Assert.Equal(0x22ul, BitConverter.ToUInt64(frame, op.Length));
        Assert.Equal(new byte[] { 3, 0x15, 0x27, 12, 0, 0x18, 0xF6, 0, 0 }, frame[(op.Length + 8)..(op.Length + 17)]);
        Assert.Equal(op.Length + 17 + 2 * QuestResultFrames.ItemOperation(empty).Length, frame.Length);
    }

    [Fact]
    public void ResetRequestIsPetMaterialSlot()
    {
        var body = new byte[20];
        body[0] = 5;
        body[8] = 6;
        body[16] = 3;

        Assert.True(PetReinforceFrames.TryReadResetRequest(body, out var request));
        Assert.Equal(new PetReinforceResetRequest(5, 6, 3), request);
        Assert.False(PetReinforceFrames.TryReadResetRequest(body.AsSpan(0, 19), out _));
    }

    [Fact]
    public void ResetResultIsPetSlotThenMaterial()
    {
        var material = new BagOperation(QuestResultFrames.Removed, 1, 0x4D0F, 0x11, 0);
        var frame = PetReinforceFrames.ResetResult(0x22, 4, material);

        Assert.Equal(0x22ul, BitConverter.ToUInt64(frame, 0));
        Assert.Equal(4u, BitConverter.ToUInt32(frame, 8));
        Assert.Equal(QuestResultFrames.ItemOperation(material), frame[12..]);
    }
}
