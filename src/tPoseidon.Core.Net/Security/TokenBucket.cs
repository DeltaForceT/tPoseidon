namespace tPoseidon.Core.Net.Security;

public sealed class TokenBucket
{
    private readonly double _capacity;
    private readonly double _tokensPerTick;
    private double _tokens;
    private long _last;

    public TokenBucket(int capacity, double perSecond, long now, long ticksPerSecond)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        if (!(perSecond > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(perSecond));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(ticksPerSecond, 1);
        _capacity = capacity;
        _tokensPerTick = perSecond / ticksPerSecond;
        _tokens = capacity;
        _last = now;
    }

    public double Capacity => _capacity;

    public bool TryTake(long now)
    {
        Refill(now);
        if (_tokens < 1)
        {
            return false;
        }

        _tokens -= 1;
        return true;
    }

    public double Available(long now)
    {
        Refill(now);
        return _tokens;
    }

    public bool IsFull(long now) => Available(now) >= _capacity;

    private void Refill(long now)
    {
        if (now <= _last)
        {
            return;
        }

        _tokens = Math.Min(_capacity, _tokens + ((now - _last) * _tokensPerTick));
        _last = now;
    }
}
