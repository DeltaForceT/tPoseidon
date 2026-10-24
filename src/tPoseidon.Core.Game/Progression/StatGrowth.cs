using tPoseidon.Core.Data;

namespace tPoseidon.Core.Game.Progression;

public static class StatGrowth
{
    public const int GroupCount = 4;
    public const int StatsPerGroup = 3;
    public const int WeightTotal = 10;
    public const int BonusPointsPerLevel = 4;

    private const int GrowthScale = 10;

    private static readonly short[][] WeightsByType =
    {
        new short[] { 4, 2, 1, 3 },
        new short[] { 1, 4, 3, 2 },
        new short[] { 2, 3, 4, 1 },
        new short[] { 3, 1, 2, 4 },
    };

    private static readonly short[] WeightsOfOtherTypes = { 3, 2, 3, 2 };

    public static int TypeIndexOf(FType type) => (byte)type - 1;

    public static IReadOnlyList<short> WeightsOfType(int type) =>
        type >= 0 && type < WeightsByType.Length ? WeightsByType[type] : WeightsOfOtherTypes;

    public static IReadOnlyList<short> WeightsInUse(IReadOnlyList<short> own, int type)
    {
        ArgumentNullException.ThrowIfNull(own);
        if (own.Count != GroupCount)
        {
            throw new ArgumentException($"A character has {GroupCount} growth weights, got {own.Count}.", nameof(own));
        }
        var total = 0;
        foreach (var weight in own) total += weight;
        return total == WeightTotal ? own : WeightsOfType(type);
    }

    public static void Raise(short[] statPoints, IReadOnlyList<short> weights, int levels)
    {
        ArgumentNullException.ThrowIfNull(statPoints);
        ArgumentNullException.ThrowIfNull(weights);
        if (statPoints.Length != GroupCount * StatsPerGroup)
        {
            throw new ArgumentException(
                $"A character has {GroupCount * StatsPerGroup} stats, got {statPoints.Length}.", nameof(statPoints));
        }
        var scaledLevels = levels * GrowthScale;
        for (var group = 0; group < GroupCount; group++)
        {
            var step = (short)(weights[group] * scaledLevels / GrowthScale);
            for (var stat = 0; stat < StatsPerGroup; stat++)
            {
                var at = group * StatsPerGroup + stat;
                statPoints[at] = (short)(statPoints[at] + step);
            }
        }
    }
}
