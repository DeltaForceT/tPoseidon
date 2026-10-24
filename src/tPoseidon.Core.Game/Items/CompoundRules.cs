using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Items;

public readonly record struct CompoundMaterial(ulong ItemUid, int ItemId, int Count);

public readonly record struct CompoundRoll(int Percent, int Successes, int Failures, bool GivesResults);

public static class CompoundRules
{
    public const int Refused = -1;
    public const int Matches = 0;
    public const int MatchesWithExtra = 1;
    public const int MaxMaterials = 5;
    public const int MaxPercent = 100;
    public const int BonusStatOffset = 5;
    public const int SingleRollLimit = 20;
    public const int DiscountCount = 10;
    public const double DiscountRate = 0.9;
    public const byte HalvedMode = 6;
    public const byte NormalMode = 10;
    public const byte FirstStubMode = 5;
    public const byte SecondStubMode = 9;

    public static int CheckRecipe(CompoundRecipe? recipe, IReadOnlyList<CompoundMaterial> materials, int count)
    {
        ArgumentNullException.ThrowIfNull(materials);
        if (recipe is null) return Refused;
        if (recipe.Type == CompoundRecipe.RareType && count != 1) return Refused;
        if (materials.Count < recipe.Requirements.Count) return Refused;
        var matched = 0;
        for (var j = 0; j < materials.Count && j < recipe.Requirements.Count; j++)
        {
            var requirement = recipe.Requirements[j];
            foreach (var material in materials)
            {
                if (material.ItemId != requirement.ItemId) continue;
                if (requirement.Count * count > material.Count) return Refused;
                matched++;
                break;
            }
        }
        if (matched != recipe.Requirements.Count) return Refused;
        return recipe.Requirements.Count < materials.Count ? MatchesWithExtra : Matches;
    }

    public static int Bonus(int stat, Func<int> roll100)
    {
        ArgumentNullException.ThrowIfNull(roll100);
        var level = stat + BonusStatOffset;
        if (level <= 0) return 0;
        if (level <= 10) return roll100() < level ? 5 : 0;
        if (level <= 20) return roll100() < level ? 10 : 5;
        if (level <= 30) return roll100() < level ? 20 : 10;
        if (level <= 40) return roll100() < level ? 30 : roll100() < level ? 20 : 10;
        return roll100() < level ? 40 : roll100() < level ? 30 : roll100() < level ? 20 : 10;
    }

    public static int Percent(CompoundRecipe? recipe, int stat, Func<int> roll100) =>
        recipe is null ? 0 : Math.Min(MaxPercent, recipe.Probability + Bonus(stat, roll100));

    public static CompoundRoll? Roll(byte mode, int percent, int count, Func<int> roll100)
    {
        ArgumentNullException.ThrowIfNull(roll100);
        switch (mode)
        {
            case FirstStubMode:
            case SecondStubMode:
                return new CompoundRoll(0, 0, count, GivesResults: false);
            case HalvedMode:
                return Rolled(percent / 2, count, roll100);
            case NormalMode:
                return Rolled(percent, count, roll100);
            default:
                return null;
        }
    }

    public static long Fee(int recipeFee, int count)
    {
        if (count == 0 || recipeFee < 0) return Refused;
        if (count < DiscountCount) return (long)count * recipeFee;
        return (long)Math.Truncate(count * (double)recipeFee * DiscountRate);
    }

    private static CompoundRoll Rolled(int percent, int count, Func<int> roll100)
    {
        var successes = 0;
        var failures = 0;
        if (percent == 0) return new CompoundRoll(percent, successes, failures, GivesResults: true);
        var single = count;
        if (count > SingleRollLimit)
        {
            var step = count / SingleRollLimit + 1;
            var rolls = 0;
            for (; rolls < SingleRollLimit; rolls++)
            {
                if ((rolls + 1) * step >= count) break;
                if (roll100() < percent) successes += step;
                else failures += step;
            }
            single = count - step * rolls;
        }
        for (var i = 0; i < single; i++)
        {
            if (roll100() < percent) successes++;
            else failures++;
        }
        return new CompoundRoll(percent, successes, failures, GivesResults: true);
    }
}
