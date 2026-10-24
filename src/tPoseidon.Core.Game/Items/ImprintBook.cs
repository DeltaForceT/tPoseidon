using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Items;

public sealed record ActiveImprint(int ItemId, int Type, float Value, DateTimeOffset Until);

public sealed class ImprintBook
{
    public const int Effect = 52;
    public const int StatTypes = 12;
    public const int ExpType = 20;
    public const int TmType = 21;
    public const double PercentDivisor = 100.0;

    private readonly Dictionary<int, ActiveImprint> _active = new();
    private readonly object _gate = new();

    public ActiveImprint Add(ImprintEntry entry, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var imprint = new ActiveImprint(entry.ItemId, entry.Type, entry.Value, now.AddHours(entry.Hours));
        Restore(imprint);
        return imprint;
    }

    public void Restore(ActiveImprint imprint)
    {
        ArgumentNullException.ThrowIfNull(imprint);
        lock (_gate) _active[imprint.Type] = imprint;
    }

    public IReadOnlyList<ActiveImprint> Active(DateTimeOffset now)
    {
        lock (_gate) return _active.Values.Where(imprint => now < imprint.Until).OrderBy(imprint => imprint.Type).ToArray();
    }

    public int[] StatBonus(DateTimeOffset now)
    {
        var bonus = new int[StatTypes];
        foreach (var imprint in Active(now))
        {
            if (imprint.Type < StatTypes) bonus[imprint.Type] += (int)imprint.Value;
        }
        return bonus;
    }

    public float ExpPercent(DateTimeOffset now) => PercentOf(ExpType, now);

    public float TmPercent(DateTimeOffset now) => PercentOf(TmType, now);

    public static long Scale(long gain, float percent) =>
        gain <= 0 || percent <= 0 ? gain : gain + (long)Math.Truncate(gain * percent / PercentDivisor);

    private float PercentOf(int type, DateTimeOffset now)
    {
        lock (_gate) return _active.TryGetValue(type, out var imprint) && now < imprint.Until ? imprint.Value : 0f;
    }
}
