namespace tPoseidon.Core.Game.Drilling;

public sealed class SurpriseSpotHandoff
{
    private readonly object _gate = new();
    private readonly Dictionary<ulong, (DateTimeOffset At, SurpriseSpot Spot)> _held = new();

    public void Store(ulong characterId, SurpriseSpot spot, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(spot);
        lock (_gate) _held[characterId] = (now, spot);
    }

    public SurpriseSpot? Take(ulong characterId, DateTimeOffset now, TimeSpan timeToLive)
    {
        (DateTimeOffset At, SurpriseSpot Spot) held;
        lock (_gate)
        {
            if (!_held.Remove(characterId, out held)) return null;
        }
        if (now - held.At > timeToLive || SurpriseSpotRules.HasExpired(held.Spot, now)) return null;
        return held.Spot;
    }
}
