using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class GhostExchangeFramesTests
{
    private static readonly byte[] StoneRequest = [0x11, 0, 0, 0, 1, 0, 0, 0, 3, 0, 0xEE, 0xEB, 0, 0];
    private static readonly byte[] NeoRequest = [0x22, 0, 0, 0, 1, 0, 0, 0, 0xF4, 0x9C, 0, 0];
    private static readonly byte[] RefusedSeven = [7];

    [Fact]
    public void RequestsAreRead()
    {
        Assert.True(GhostExchangeFrames.TryReadStone(StoneRequest, out var stone));
        Assert.Equal(new GhostStoneRequest(0x1_0000_0011, 3, 60398), stone);
        Assert.False(GhostExchangeFrames.TryReadStone(StoneRequest.AsSpan(0, 13), out _));
        Assert.True(GhostExchangeFrames.TryReadNeo(NeoRequest, out var neo));
        Assert.Equal(new GhostNeoRequest(0x1_0000_0022, 40180), neo);
        Assert.False(GhostExchangeFrames.TryReadNeo(NeoRequest.AsSpan(0, 11), out _));
    }

    [Fact]
    public void ExchangedIsCountThenOperations()
    {
        var removed = new BagOperation(QuestResultFrames.Removed, 2, 40001, 0x1_0000_0022, 0);
        var created = new BagOperation(QuestResultFrames.Created, 2, 40180, 0x1_0000_0033, 1, 2, 1, 4, 0);
        var frame = GhostExchangeFrames.Exchanged(new[] { removed, created });
        Assert.Equal(2, BitConverter.ToUInt16(frame, 0));
        Assert.Equal(QuestResultFrames.ItemOperation(removed).Concat(QuestResultFrames.ItemOperation(created)), frame[2..]);
        Assert.Equal(RefusedSeven, GhostExchangeFrames.Refused(7));
    }
}
