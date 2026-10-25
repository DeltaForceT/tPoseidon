using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Guardian;

public static class GuardianStats
{
    public const float LevelFactor = 0.01f;
    public const int ApIndex = 0;
    public const int AcIndex = 1;
    public const int DxIndex = 2;
    public const int MaIndex = 4;
    public const int DaIndex = 7;
    public const int LkIndex = 8;
    public const int HpIndex = 9;
    public const int DpIndex = 10;
    public const int HvIndex = 11;
    public const int AccuracyMagicBonus = 50;

    public static int[] Compute(short level, IReadOnlyList<short> points, GuardianGrowthEntry growth)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(growth);
        var result = new int[GuardianTables.GrowthColumns.Length];
        for (var i = 0; i < result.Length; i++)
        {
            var basis = (int)Math.Round(points[i] * level * LevelFactor, MidpointRounding.ToEven);
            result[i] = (int)Math.Round(basis * growth.Ratios[i], MidpointRounding.ToEven);
        }
        result[AcIndex] += AccuracyMagicBonus;
        result[MaIndex] += AccuracyMagicBonus;
        return result;
    }

    public static Skills.SkillAttacker Attacker(short level, IReadOnlyList<int> stats) => new(
        Ap: stats[ApIndex],
        Ac: stats[AcIndex],
        Ma: stats[MaIndex],
        Lk: stats[LkIndex],
        Level: level,
        Da: stats[DaIndex],
        Hp: stats[HpIndex],
        MaxHp: stats[HpIndex],
        Hv: stats[HvIndex],
        Dx: stats[DxIndex],
        Dp: stats[DpIndex],
        AlwaysHits: true);
}
