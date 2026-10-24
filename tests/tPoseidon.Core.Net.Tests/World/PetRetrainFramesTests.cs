using tPoseidon.Core.Data;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class PetRetrainFramesTests
{
    [Fact]
    public void RequestIsThreeUidsAndTheLockedStat()
    {
        byte[] payload =
        [
            0x11, 0, 0, 0, 1, 0, 0, 0,
            0x22, 0, 0, 0, 1, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0,
            0x14, 0x27, 0, 0,
        ];
        Assert.True(PetRetrainFrames.TryReadRequest(payload, out var request));
        Assert.Equal(0x1_0000_0011UL, request.PetWireUid);
        Assert.Equal(0x1_0000_0022UL, request.MaterialWireUid);
        Assert.Equal(0UL, request.LockWireUid);
        Assert.Equal(10004, request.LockStatId);
        Assert.False(PetRetrainFrames.TryReadRequest(payload.AsSpan(0, 27), out _));
    }

    [Fact]
    public void ResultIsUidStatsThenRawRecords()
    {
        var frame = PetRetrainFrames.Result(0x1_0000_0011, [new ItemProperty(10, 10000, -3, 0)], [new BagOperation(3, 1, 19690, 0x1_0000_0022, 0)]);
        byte[] expected =
        [
            0x11, 0, 0, 0, 1, 0, 0, 0,
            1, 0x10, 0x27, 0xFD, 0xFF,
            1,
            3, 0, 0, 0, 1, 0, 0, 0, 0xEA, 0x4C, 0, 0, 0x22, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        ];
        Assert.Equal(expected, frame);
        Assert.Equal([7, 0], PetRetrainFrames.Refused(7));
    }
}
