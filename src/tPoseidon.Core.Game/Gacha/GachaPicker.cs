using System.Security.Cryptography;
using tPoseidon.Core.Data;

namespace tPoseidon.Core.Game.Gacha;

public static class GachaPicker
{
    public static GachaPrize Pick(IReadOnlyList<GachaPrize> pool) => Pick(pool, RandomNumberGenerator.GetInt32);

    public static GachaPrize Pick(IReadOnlyList<GachaPrize> pool, Func<int, int> randomBelow)
    {
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentNullException.ThrowIfNull(randomBelow);
        if (pool.Count == 0) throw new ArgumentException("The prize pool is empty.", nameof(pool));
        var total = TotalWeight(pool);
        var roll = randomBelow(total);
        if (roll < 0 || roll >= total) throw new ArgumentOutOfRangeException(nameof(randomBelow), roll, "The roll must be below the pool weight.");
        foreach (var prize in pool)
        {
            if (roll < prize.Weight) return prize;
            roll -= prize.Weight;
        }
        throw new InvalidOperationException("The roll fell outside the pool.");
    }

    public static int TotalWeight(IReadOnlyList<GachaPrize> pool)
    {
        ArgumentNullException.ThrowIfNull(pool);
        long total = 0;
        foreach (var prize in pool)
        {
            if (prize.Weight <= 0) throw new ArgumentException($"Prize {prize.ItemIndex} has weight {prize.Weight}.", nameof(pool));
            total += prize.Weight;
        }
        return total <= int.MaxValue
            ? (int)total
            : throw new ArgumentException("The pool weight does not fit in 32 bits.", nameof(pool));
    }
}
