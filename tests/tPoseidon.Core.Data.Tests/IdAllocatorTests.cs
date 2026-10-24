namespace tPoseidon.Core.Data.Tests;

public class IdAllocatorTests
{
    [Fact]
    public void NewId_AssignsMonotonicallyFromFirstId()
    {
        var alloc = new IdAllocator(firstId: 0x0100, lastId: 0xFFFE);
        Assert.Equal((ushort)0x0100, alloc.NewId());
        Assert.Equal((ushort)0x0101, alloc.NewId());
        Assert.Equal((ushort)0x0102, alloc.NewId());
    }

    [Fact]
    public void Release_ReturnsIdToFreeList_ThenReused()
    {
        var alloc = new IdAllocator(firstId: 0x0100, lastId: 0xFFFE);
        var a = alloc.NewId();
        var b = alloc.NewId();
        alloc.Release(a);
        Assert.Equal(a, alloc.NewId());
        Assert.Equal((ushort)(b + 1), alloc.NewId());
    }

    [Fact]
    public void Release_IdempotentDoubleRelease()
    {
        var alloc = new IdAllocator(firstId: 0x0100, lastId: 0xFFFE);
        var a = alloc.NewId();
        alloc.Release(a);
        alloc.Release(a);
        Assert.Equal(a, alloc.NewId());
        Assert.NotEqual(a, alloc.NewId());
    }

    [Fact]
    public void Release_OutOfRangeId_NoOps()
    {
        var alloc = new IdAllocator(firstId: 0x0100, lastId: 0x0200);
        alloc.Release(0x0050);
        alloc.Release(0x0500);
        Assert.Equal((ushort)0x0100, alloc.NewId());
    }

    [Fact]
    public void NewId_Exhausted_Throws()
    {
        var alloc = new IdAllocator(firstId: 1, lastId: 3);
        Assert.Equal((ushort)1, alloc.NewId());
        Assert.Equal((ushort)2, alloc.NewId());
        Assert.Equal((ushort)3, alloc.NewId());
        Assert.Throws<InvalidOperationException>(() => alloc.NewId());
    }

    [Fact]
    public void InvalidRange_Throws()
    {
        Assert.Throws<ArgumentException>(() => new IdAllocator(firstId: 100, lastId: 50));
    }

    [Fact]
    public void InFlightCount_TracksAllocatedMinusReleased()
    {
        var alloc = new IdAllocator(firstId: 1, lastId: 100);
        Assert.Equal(0, alloc.InFlightCount);
        alloc.NewId();
        var b = alloc.NewId();
        alloc.NewId();
        Assert.Equal(3, alloc.InFlightCount);
        alloc.Release(b);
        Assert.Equal(2, alloc.InFlightCount);
    }
}
