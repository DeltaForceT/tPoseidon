using tPoseidon.Core.Net.Security;

namespace tPoseidon.Core.Net.Tests.Security;

public sealed class TokenBucketTests
{
    private const long Second = TimeSpan.TicksPerSecond;

    [Fact]
    public void FullBucket_AllowsTheBurstThenRefuses()
    {
        var bucket = new TokenBucket(capacity: 5, perSecond: 1, now: 0, ticksPerSecond: Second);

        for (var i = 0; i < 5; i++)
        {
            Assert.True(bucket.TryTake(0));
        }

        Assert.False(bucket.TryTake(0));
    }

    [Fact]
    public void Bucket_RefillsAtTheConfiguredRate()
    {
        var bucket = new TokenBucket(capacity: 2, perSecond: 4, now: 0, ticksPerSecond: Second);
        Assert.True(bucket.TryTake(0));
        Assert.True(bucket.TryTake(0));
        Assert.False(bucket.TryTake(0));

        Assert.False(bucket.TryTake(Second / 8));
        Assert.True(bucket.TryTake(Second / 4));
        Assert.False(bucket.TryTake(Second / 4));
    }

    [Fact]
    public void Bucket_NeverHoldsMoreThanItsCapacity()
    {
        var bucket = new TokenBucket(capacity: 3, perSecond: 10, now: 0, ticksPerSecond: Second);

        Assert.Equal(3, bucket.Available(3600 * Second));
        Assert.True(bucket.IsFull(3600 * Second));
    }

    [Fact]
    public void Bucket_IgnoresTimeGoingBackwards()
    {
        var bucket = new TokenBucket(capacity: 1, perSecond: 1, now: 10 * Second, ticksPerSecond: Second);
        Assert.True(bucket.TryTake(10 * Second));

        Assert.False(bucket.TryTake(5 * Second));
        Assert.True(bucket.TryTake(11 * Second));
    }

    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(1, 0.0)]
    [InlineData(1, -1.0)]
    public void Bucket_RejectsNonPositiveLimits(int capacity, double perSecond)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TokenBucket(capacity, perSecond, 0, Second));
    }
}
