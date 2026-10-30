namespace tPoseidon.Core.Data;

public static class MyCampStarterSet
{
    public const long SellCode = 200000001;
    public const int RandomPickCount = 3;

    public static IReadOnlyList<int> DefaultItems { get; } = new[]
    {
        2000, 3015, 3015, 5002, 8001, 10002, 10015, 11000, 15008, 16056, 16056, 16057, 16057,
    };

    public static IReadOnlyList<int> RandomPool { get; } = new[]
    {
        4013, 4013, 6010, 7004, 9007, 12020, 15038, 16000, 16041, 16045, 16045, 16045, 16045, 17006,
    };

    public static IReadOnlyList<int> Choose(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        var picks = Enumerable.Range(0, RandomPool.Count)
            .OrderBy(_ => random.Next())
            .Take(RandomPickCount)
            .Select(row => RandomPool[row]);
        return DefaultItems.Concat(picks).ToArray();
    }
}
