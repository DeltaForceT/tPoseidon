using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Skills;

public static class SkillFailure
{
    public const ushort General = 0;
    public const ushort BelowMinLevel = 1;
    public const ushort AboveMaxLevel = 2;
    public const ushort NotEnoughHp = 5;
    public const ushort NotEnoughMp = 6;
    public const ushort OnCooldown = 0xC;
    public const ushort LineBlocked = 0xD;
    public const ushort RefusedWithNotice = 0xF;

    public const byte WrongPlaceNotice = 0xF;
}

public static class SkillChecks
{
    public const int ProbabilityRange = 100;
    public const int DefaultRange = 200;
    public const int ChaseLimitMs = 5000;
    public const double ServerTickSeconds = 0.1;

    private static readonly int[] TownMapTypes = { 1, 5, 6 };

    public static ushort? Conditions(SkillTableRow? row, int level, long hp, long mp, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        if (row is null) return null;
        if (row.Amount(SkillDefinition.MinLevelColumn) is { } minLevel && level < minLevel) return SkillFailure.BelowMinLevel;
        if (row.Amount(SkillDefinition.MaxLevelColumn) is { } maxLevel && level > maxLevel) return SkillFailure.AboveMaxLevel;
        if (row.Amount(SkillDefinition.HpCostColumn) is { } hpCost && hp < hpCost) return SkillFailure.NotEnoughHp;
        if (row.Amount(SkillDefinition.MpCostColumn) is { } mpCost && mp < mpCost) return SkillFailure.NotEnoughMp;
        if (row.Amount(SkillDefinition.ProbabilityColumn) is { } probability
            && rng.Next(ProbabilityRange) > probability)
        {
            return SkillFailure.General;
        }
        return null;
    }

    public static int RangeOf(SkillTableRow? row) => row?.Amount(SkillDefinition.RangeColumn) ?? DefaultRange;

    public static int DelayMsOf(SkillTableRow? row) =>
        row?.Value(SkillDefinition.DelayColumn) is { } delay ? (int)((double)delay * 1000.0) : 0;

    public static bool IsWithinRange(int range, int fromX, int fromY, int toX, int toY)
    {
        long dx = fromX - toX;
        long dy = fromY - toY;
        return dx * dx + dy * dy < (long)range * range;
    }

    public static (int X, int Y) ChaseEnd(int range, float speed, int fromX, int fromY, int toX, int toY)
    {
        double dx = fromX - toX;
        double dy = fromY - toY;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        if (distance == 0) return (toX, toY);
        var stopAt = Math.Max(0.0, range - speed * ServerTickSeconds);
        return ((int)Math.Round(toX + dx / distance * stopAt), (int)Math.Round(toY + dy / distance * stopAt));
    }

    public static bool IsTownMap(int mapType) => Array.IndexOf(TownMapTypes, mapType) >= 0;

    public static (int Hp, int Mp) Costs(SkillTableRow? row, long hp, long mp)
    {
        var hpCost = row?.Amount(SkillDefinition.HpCostColumn) ?? 0;
        var mpCost = row?.Amount(SkillDefinition.MpCostColumn) ?? 0;
        return (
            hpCost > 0 && hpCost <= hp ? hpCost : 0,
            mpCost > 0 && mpCost <= mp ? mpCost : 0);
    }
}
