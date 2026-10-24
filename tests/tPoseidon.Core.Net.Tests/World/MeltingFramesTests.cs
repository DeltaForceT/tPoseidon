using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class MeltingFramesTests
{
    private static readonly byte[] TwoItems =
    [
        2,
        0x11, 0, 0, 0, 1, 0, 0, 0,
        0x22, 0, 0, 0, 1, 0, 0, 0,
    ];

    private static readonly ulong[] TwoUids = [0x1_0000_0011UL, 0x1_0000_0022UL];
    private static readonly byte[] RefusedEight = [8];

    [Fact]
    public void RequestIsCountThenUids()
    {
        Assert.True(MeltingFrames.TryReadRequest(TwoItems, out var uids));
        Assert.Equal(TwoUids, uids);
        Assert.False(MeltingFrames.TryReadRequest(TwoItems.AsSpan(0, 16), out _));
        Assert.False(MeltingFrames.TryReadRequest(ReadOnlySpan<byte>.Empty, out _));
    }

    [Fact]
    public void ResultIsMoneyThenOperations()
    {
        var removed = new BagOperation(QuestResultFrames.Removed, 2, 40001, 0x1_0000_0011, 0);
        var box = new BagOperation(QuestResultFrames.Created, 1, 433073, 0x1_0000_0033, 1);
        var frame = MeltingFrames.Result(40000, new[] { removed, box });
        Assert.Equal(SlotExtendFrames.MoneyAndOperations(40000, new[] { removed, box }), frame);
        Assert.Equal(40000u, BitConverter.ToUInt32(frame, 0));
        Assert.Equal(2, BitConverter.ToUInt16(frame, 4));
        Assert.Equal(RefusedEight, MeltingFrames.Refused(8));
    }
}
