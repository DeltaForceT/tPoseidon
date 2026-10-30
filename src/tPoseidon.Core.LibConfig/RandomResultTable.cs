namespace tPoseidon.Core.LibConfig;

public readonly record struct RandomResultEntry(int ItemIndex, int Count, int Low, int High);

public sealed class RandomResultTable
{
    public const int Slots = 10;
    public const double RateScale = 10000.0;

    private readonly RandomResultEntry[] _entries;

    public RandomResultTable(int id, IEnumerable<RandomResultEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        Id = id;
        _entries = entries.ToArray();
        Total = _entries.Length == 0 ? 0 : _entries[^1].High;
    }

    public int Id { get; }

    public int Total { get; }

    public IReadOnlyList<RandomResultEntry> Entries => _entries;

    public static int WeightOf(float rate) => (int)((double)rate * RateScale);

    public RandomResultEntry? Pick(int roll)
    {
        foreach (var entry in _entries)
        {
            if (entry.Low <= roll && roll <= entry.High) return entry;
        }
        return null;
    }
}
