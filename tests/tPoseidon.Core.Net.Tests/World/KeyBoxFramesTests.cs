using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class KeyBoxFramesTests
{
    [Fact]
    public void TheOpenRequestIsOneUid()
    {
        Assert.True(KeyBoxFrames.TryReadOpen([1, 0, 0, 0, 1, 0, 0, 0], out var uid));
        Assert.Equal(0x1_0000_0001UL, uid);
        Assert.False(KeyBoxFrames.TryReadOpen([1, 0, 0, 0, 1, 0, 0], out _));
    }

    [Fact]
    public void TheConfirmRequestIsTheBoxUidThenTheKeyUid()
    {
        byte[] bytes = [1, 0, 0, 0, 1, 0, 0, 0, 2, 0, 0, 0, 1, 0, 0, 0];
        Assert.True(KeyBoxFrames.TryReadConfirm(bytes, out var box, out var key));
        Assert.Equal(0x1_0000_0001UL, box);
        Assert.Equal(0x1_0000_0002UL, key);
        Assert.False(KeyBoxFrames.TryReadConfirm(bytes.AsSpan(0, 15), out _, out _));
    }

    [Fact]
    public void TheOpenRefusalIsTheByteOne()
    {
        Assert.Equal(new byte[] { 1 }, KeyBoxFrames.OpenRefused());
    }

    [Theory]
    [InlineData(KeyBoxRefusal.NotUsable, 1)]
    [InlineData(KeyBoxRefusal.KeyRequired, 2)]
    [InlineData(KeyBoxRefusal.KeyRejected, 3)]
    [InlineData(KeyBoxRefusal.TooHeavy, 4)]
    [InlineData(KeyBoxRefusal.BagFull, 5)]
    [InlineData(KeyBoxRefusal.Failed, 6)]
    public void TheConfirmRefusalIsAWordWithTheCode(KeyBoxRefusal refusal, byte code)
    {
        Assert.Equal(new byte[] { code, 0 }, KeyBoxFrames.ConfirmRefused(refusal));
    }

    [Fact]
    public void TheResultIsACountThenTheItemRecords()
    {
        var created = new BagOperation(QuestResultFrames.Created, 1, 600, 0x1_0000_0005UL, 2);
        var removed = new BagOperation(QuestResultFrames.Removed, 1, 100, 0x1_0000_0003UL, 0);
        var frame = KeyBoxFrames.Results([created, removed]);
        Assert.Equal(new byte[] { 2, 0 }, frame[..2]);
        Assert.Equal(QuestResultFrames.ItemOperation(created), frame[2..(2 + QuestResultFrames.ItemOperation(created).Length)]);
        Assert.Equal([created, removed], QuestResultFrames.ReadItemOperations(frame));
    }
}
