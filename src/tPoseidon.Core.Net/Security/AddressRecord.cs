namespace tPoseidon.Core.Net.Security;

internal sealed class AddressRecord
{
    private readonly Queue<(long At, int Weight)> _strikes = new();
    private int _strikeTotal;

    public AddressRecord(TokenBucket connectBucket, long now)
    {
        ConnectBucket = connectBucket;
        LastSeen = now;
    }

    public object Gate { get; } = new();

    public TokenBucket ConnectBucket { get; }

    public int Connections { get; set; }

    public long LastSeen { get; set; }

    public bool Retired { get; set; }

    public int BanLevel { get; private set; }

    public long BannedUntil { get; private set; }

    public DateTimeOffset BannedSince { get; private set; }

    public string BanReason { get; private set; } = "";

    public long LastBanEnded { get; private set; }

    public int StrikeTotal => _strikeTotal;

    public TokenBucket? RequestBucket { get; set; }

    public bool IsBanned(long now)
    {
        if (BannedUntil == 0)
        {
            return false;
        }

        if (BannedUntil > now)
        {
            return true;
        }

        LastBanEnded = BannedUntil;
        BannedUntil = 0;
        return false;
    }

    public (TimeSpan Length, int Level, int Strikes)? AddStrike(long now, int weight, BanRules rules, DateTimeOffset nowUtc, string reason)
    {
        while (_strikes.Count > 0 && now - _strikes.Peek().At > rules.WindowTicks)
        {
            _strikeTotal -= _strikes.Dequeue().Weight;
        }

        _strikes.Enqueue((now, weight));
        _strikeTotal += weight;
        if (IsBanned(now) || _strikeTotal < rules.StrikesBeforeBan)
        {
            return null;
        }

        if (LastBanEnded != 0 && now - LastBanEnded > rules.ForgetTicks)
        {
            BanLevel = 0;
        }

        var minutes = Math.Min(rules.MaxMinutes, rules.BaseMinutes * Math.Pow(2, Math.Min(BanLevel, 30)));
        var length = TimeSpan.FromMinutes(minutes);
        var strikes = _strikeTotal;
        BanLevel++;
        BannedUntil = now + (long)(length.TotalSeconds * rules.TicksPerSecond);
        BannedSince = nowUtc;
        BanReason = reason;
        ClearStrikes();
        return (length, BanLevel, strikes);
    }

    public bool Pardon(DateTimeOffset at)
    {
        var lifted = BannedUntil != 0 && BannedSince <= at;
        if (lifted)
        {
            BannedUntil = 0;
        }

        BanLevel = 0;
        LastBanEnded = 0;
        ClearStrikes();
        return lifted;
    }

    public bool IsIdle(long now, long forgetTicks, long banForgetTicks) =>
        Connections == 0
        && now - LastSeen >= forgetTicks
        && ConnectBucket.IsFull(now)
        && (RequestBucket is null || RequestBucket.IsFull(now))
        && !IsBanned(now)
        && (BanLevel == 0 || now - LastBanEnded > banForgetTicks);

    private void ClearStrikes()
    {
        _strikes.Clear();
        _strikeTotal = 0;
    }
}

internal sealed record BanRules(int StrikesBeforeBan, long WindowTicks, double BaseMinutes, double MaxMinutes, long ForgetTicks, long TicksPerSecond);
