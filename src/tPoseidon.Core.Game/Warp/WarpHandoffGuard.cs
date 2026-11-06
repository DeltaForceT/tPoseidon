namespace tPoseidon.Core.Game.Warp;

public sealed class WarpHandoffGuard
{
    private readonly object _gate = new();
    private readonly TimeSpan _holdFor;
    private DateTime _heldUntil = DateTime.MinValue;

    public WarpHandoffGuard(TimeSpan? holdFor = null)
    {
        _holdFor = holdFor ?? WarpManager.DefaultTimeToLive;
    }

    public bool TryClaim(DateTime nowUtc)
    {
        lock (_gate)
        {
            if (nowUtc < _heldUntil) return false;
            _heldUntil = nowUtc + _holdFor;
            return true;
        }
    }

    public void Release()
    {
        lock (_gate) _heldUntil = DateTime.MinValue;
    }
}
