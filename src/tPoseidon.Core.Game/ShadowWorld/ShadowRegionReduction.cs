using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.ShadowWorld;

public sealed class ShadowRegionReduction
{
    public const int FullPercent = MonsterEntity.FullStatPercent;
    public const int ExcludedMapClass = MapKindEntry.NoReturnClass;

    private readonly object _gate = new();
    private readonly Dictionary<int, Entry> _byGroup = new();
    private long _sequence;

    private sealed record Entry(int Region, int Percent, DateTimeOffset Expires, long Sequence);

    public static int ScaledStat(int stat, int percent) => (int)(stat * ((float)percent / FullPercent));

    public static int[] ScaledDeltas(IReadOnlyList<int> bases, IReadOnlyList<int> effectDeltas, int percent)
    {
        ArgumentNullException.ThrowIfNull(bases);
        ArgumentNullException.ThrowIfNull(effectDeltas);
        var deltas = new int[bases.Count];
        for (var stat = 0; stat < deltas.Length; stat++)
        {
            deltas[stat] = percent == FullPercent ? effectDeltas[stat] : ScaledStat(bases[stat] + effectDeltas[stat], percent) - bases[stat];
        }

        return deltas;
    }

    public bool Apply(ShadowRewardEntry area, int region, DateTimeOffset expires)
    {
        ArgumentNullException.ThrowIfNull(area);
        if (area.MonsterRespawnDecrease == 0) return false;
        lock (_gate) _byGroup[area.Group] = new Entry(region, FullPercent - area.MonsterRespawnDecrease, expires, ++_sequence);
        return true;
    }

    public int PercentFor(int region, int mapClass, DateTimeOffset now)
    {
        if (mapClass == ExcludedMapClass) return FullPercent;
        lock (_gate)
        {
            return _byGroup.Values
                .Where(entry => entry.Region == region && now < entry.Expires)
                .OrderByDescending(entry => entry.Sequence)
                .Select(entry => entry.Percent)
                .DefaultIfEmpty(FullPercent)
                .First();
        }
    }

    public bool AnyActive(DateTimeOffset now)
    {
        lock (_gate) return _byGroup.Values.Any(entry => now < entry.Expires);
    }
}
