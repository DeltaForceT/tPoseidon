using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class PetLevelUpFramesTests
{
    [Fact]
    public void RequestIsTwoUidsAndTheResultCountsOperationsInOneByte()
    {
        var payload = new byte[16];
        BitConverter.GetBytes(0x11UL).CopyTo(payload, 0);
        BitConverter.GetBytes(0x22UL).CopyTo(payload, 8);
        Assert.True(PetLevelUpFrames.TryReadRequest(payload, out var request));
        Assert.Equal((0x11UL, 0x22UL), (request.PetWireUid, request.CatalystWireUid));
        Assert.False(PetLevelUpFrames.TryReadRequest(payload.AsSpan(0, 15), out _));

        var removed = new BagOperation(QuestResultFrames.Removed, 1, 19719, 0x22, 0);
        var bytes = PetLevelUpFrames.Result([removed]);
        Assert.Equal(1, bytes[0]);
        Assert.Equal(QuestResultFrames.ItemOperation(removed), bytes[1..]);
        Assert.Equal(new byte[] { 9, 0 }, PetLevelUpFrames.Refused(9));
    }
}
