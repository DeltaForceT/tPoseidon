using tPoseidon.Core.Game.Combat;

namespace tPoseidon.Core.Game.Characters;

public static class RunRules
{
    public const int ForcedWalkPercent = 90;

    public const int WeightPercentWithoutCapacity = 100;

    public static bool CanRun(long carriedWeight, long maxWeight)
    {
        var percent = maxWeight < 1 ? WeightPercentWithoutCapacity : carriedWeight * 100 / maxWeight;
        return percent >= 0 && percent < ForcedWalkPercent;
    }

    public static byte Allowed(byte requested, long carriedWeight, long maxWeight) =>
        requested == MeleeApproach.RunMoveType && !CanRun(carriedWeight, maxWeight)
            ? MeleeApproach.WalkMoveType
            : requested;
}
