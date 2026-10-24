using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Drilling;

public static class DrillResult
{
    public const byte Spot = 0;
    public const byte Stopped = 1;
    public const byte Nothing = 2;
    public const byte Found = 3;
    public const byte TooShallow = 4;
    public const byte WornOut = 6;
    public const byte NotDiggable = 7;
    public const byte Interrupted = 10;
}

public static class DrillRules
{
    public const int NotDiggableNature = 6;
    public const int DepthStep = 5;
    public const ushort TokenMask = 0xFFFE;
    public const int LuckBonus = 10;
    public const int PercentRoll = 100;
    public const int TmLevelCap = 500;
    public const int InvincibleDrillAbility = 6;
    public const int InvincibleDrillSkill = 3001;

    public static int Wear(int life, int presses, int drillNature, int cellNature)
    {
        var used = drillNature == 0 || drillNature == cellNature ? presses : 2 * presses;
        return Math.Max(0, life - used);
    }

    public static ushort Check(ushort reported, DrillParamEntry drill, int depth, int presses)
    {
        ArgumentNullException.ThrowIfNull(drill);
        var value = reported ^ (drill.BarSpeed << 1) ^ drill.BarEffArea ^ (drill.ScoopMass << 5) ^ (depth << 8) ^ (presses << 12);
        return (ushort)(value & TokenMask);
    }

    public static bool InRange(DrillParamEntry drill, int depth, int presses, int mapDepth)
    {
        ArgumentNullException.ThrowIfNull(drill);
        return presses > 0 && depth <= drill.DigDepth && depth <= mapDepth;
    }

    public static int MinimumMilliseconds(DrillParamEntry drill, int itemDepth, int mapDepth)
    {
        ArgumentNullException.ThrowIfNull(drill);
        var depth = itemDepth < 1 ? mapDepth : itemDepth;
        var scoop = drill.ScoopMass == 0 ? 1 : drill.ScoopMass;
        var dig = (float)(depth * 120 / scoop);
        var gauge = (float)((200 - drill.BarEffArea) * 100 / (drill.BarSpeed * 0.1666666716));
        var total = dig + gauge;
        return total < 0 ? 0 : (int)Math.Truncate(total * 0.699999988f);
    }

    public static int ItemDepth(int laidDepth) => laidDepth / DepthStep * DepthStep;

    public static int StartExp(DrillZoneEntry zone, int da)
    {
        ArgumentNullException.ThrowIfNull(zone);
        var basis = (zone.MinItemDepth + zone.MaxItemDepth) / 2;
        return Math.Max(1, (int)Math.Truncate(basis * (da + 200) * 0.01));
    }

    public static float GradeRate(IReadOnlyList<float> rates, int grade)
    {
        ArgumentNullException.ThrowIfNull(rates);
        if (rates.Count == 0 || grade < 0) return 0;
        if (grade < rates.Count) return rates[grade];
        return grade > rates.Count ? rates[^1] : 0;
    }

    public static int EndExp(IReadOnlyList<float> rates, int grade, int depth, int level) =>
        Math.Max(1, (int)Math.Truncate(GradeRate(rates, grade) * depth * (level + 400) * 0.0005));

    public static int FindTmExp(int level, int depth, int grade) => (level * 3 + depth) * 4 * grade;

    public static byte EndResult(int? laidDepth, int rolledItem, int depth, int mapDepth)
    {
        if (laidDepth is null) return DrillResult.Nothing;
        if (rolledItem == 0) return depth == mapDepth ? DrillResult.Nothing : DrillResult.TooShallow;
        var itemDepth = ItemDepth(laidDepth.Value);
        if (depth > itemDepth) return DrillResult.Nothing;
        return depth < itemDepth ? DrillResult.TooShallow : DrillResult.Found;
    }

    public static bool GivesEndExp(int? laidDepth, int rolledItem, int depth) =>
        laidDepth is null || rolledItem == 0 || depth <= ItemDepth(laidDepth.Value);
}
