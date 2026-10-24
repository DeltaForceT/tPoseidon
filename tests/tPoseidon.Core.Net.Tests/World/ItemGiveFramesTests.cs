using System.Text;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class ItemGiveFramesTests
{
    private static readonly BagOperation Taken = new(QuestResultFrames.StackChanged, 1, 1001, 0x10, 3);

    [Fact]
    public void GivenIsOperationCountThenReceiverName()
    {
        var op = QuestResultFrames.ItemOperation(Taken);
        var frame = ItemGiveFrames.Given(Taken, 2, "Ab", Encoding.ASCII);
        Assert.Equal(op, frame[..op.Length]);
        Assert.Equal(new byte[] { 2, 0, (byte)'A', (byte)'b', 0 }, frame[op.Length..]);
    }

    [Fact]
    public void ReceivedIsOperationCountGiverIdThenGiverName()
    {
        var op = QuestResultFrames.ItemOperation(Taken);
        var frame = ItemGiveFrames.Received(Taken, 2, 0x1234, "G", Encoding.ASCII);
        Assert.Equal(op, frame[..op.Length]);
        Assert.Equal(new byte[] { 2, 0, 0x34, 0x12, (byte)'G', 0 }, frame[op.Length..]);
    }

    [Fact]
    public void RefusedIsItemUidCountNameThenCode()
    {
        var frame = ItemGiveFrames.Refused(1001, 0x0102030405060708, 2, "R", ItemGiveFrames.RefusedNoRoom, Encoding.ASCII);
        Assert.Equal(new byte[] { 0xE9, 0x03, 0, 0, 8, 7, 6, 5, 4, 3, 2, 1, 2, 0, (byte)'R', 0, 7 }, frame);
    }
}
