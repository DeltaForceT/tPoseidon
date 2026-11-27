namespace tPoseidon.Core.Game;

public static class KrRand
{
    public const int Max = 0x7FFF;
    public const int Range = Max + 1;

    public static int Next(Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        return rng.Next(Range);
    }

    public static int Scaled(Random rng, int minimum, int maximum)
    {
        var product = Next(rng) * (maximum - minimum);
        return ((product + ((product >> 31) & 0x7FFF)) >> 15) + minimum;
    }

    public static int Below(Random rng, int limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        return Next(rng) % limit;
    }
}
