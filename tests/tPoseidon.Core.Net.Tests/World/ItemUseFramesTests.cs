using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class ItemUseFramesTests
{
    [Fact]
    public void AUseRequestIsTheItemUid()
    {
        var payload = new byte[] { 0x05, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00 };

        Assert.True(ItemUseFrames.TryReadUse(payload, out var uid));
        Assert.Equal(0x0000_0001_0000_0005UL, uid);
        Assert.False(ItemUseFrames.TryReadUse(payload.AsSpan(0, 7), out _));
    }

    [Fact]
    public void AUseOnTargetRequestIsTheItemUidThenTheTargetId()
    {
        var payload = new byte[] { 0x05, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x34, 0x12 };

        Assert.True(ItemUseFrames.TryReadUseOnTarget(payload, out var uid, out var target));
        Assert.Equal(0x0000_0001_0000_0005UL, uid);
        Assert.Equal((ushort)0x1234, target);
        Assert.False(ItemUseFrames.TryReadUseOnTarget(payload.AsSpan(0, 9), out _, out _));
    }

    [Fact]
    public void TheEffectAnswerIsUidEffectAndPlayer()
    {
        var reply = ItemUseFrames.Reply(11, 0x1234);

        Assert.Equal(new byte[] { 0x05, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x0B, 0x00, 0x34, 0x12 },
            ItemUseFrames.Used(0x0000_0001_0000_0005UL, reply));
    }

    [Fact]
    public void TheSeenAnswerIsPlayerItemEffectAndPlayer()
    {
        var reply = ItemUseFrames.Reply(11, 0x1234);

        Assert.Equal(new byte[] { 0x34, 0x12, 0xD1, 0x07, 0x00, 0x00, 0x0B, 0x00, 0x34, 0x12 },
            ItemUseFrames.Seen(0x1234, 2001, reply));
    }

    [Fact]
    public void BlockIdsCountFromOneAndWrapBeforeTheHighBit()
    {
        var ids = new CommandBlockIds();

        Assert.Equal(1, ids.Next());
        Assert.Equal(2, ids.Next());
        for (var i = 2; i < MeleeSwingFrames.HighestBlockId; i++) ids.Next();
        Assert.Equal(1, ids.Next());
    }
}
