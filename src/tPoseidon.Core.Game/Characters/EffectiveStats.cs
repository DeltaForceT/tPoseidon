using tPoseidon.Core.Data;

namespace tPoseidon.Core.Game.Characters;

public readonly record struct EffectiveStats(
    int Ap, int Ac, int Dx,
    int Mp, int Ma, int Md,
    int Wt, int Da, int Lk,
    int Hp, int Dp, int Hv);

public static class EffectiveStatsCalculator
{
    public const int BaseAp = 18;
    public const int BaseAc = 50;
    public const int BaseDx = 10;
    public const int BaseMp = 80;
    public const int BaseMa = 50;
    public const int BaseMd = 0;
    public const int BaseWt = 1200;
    public const int BaseDa = 0;
    public const int BaseLk = 0;
    public const int BaseHp = 120;
    public const int BaseDp = 0;
    public const int BaseHv = 0;

    private const double ApPerAxisPoint = 1.0;
    private const double AcPerAxisPoint = 1.0 / 16.0;
    private const double DxPerAxisPoint = -1.0 / 52.0;
    private const double MpPerAxisPoint = 7.5;
    private const double MaPerAxisPoint = 1.0 / 16.0;
    private const double MdPerAxisPoint = 1.0 / 6.0;
    private const double WtPerAxisPoint = 20.0;
    private const double DaPerAxisPoint = 1.0 / 16.0;
    private const double LkPerAxisPoint = 1.0 / 16.0;
    private const double HpPerAxisPoint = 7.5;
    private const double DpPerAxisPoint = 2.0 / 3.0;
    private const double HvPerAxisPoint = 1.0 / 16.0;

    public static EffectiveStats Compute(Character character)
    {
        ArgumentNullException.ThrowIfNull(character);

        var lvl = Math.Max(1, (int)character.Level);
        var pPoints = (lvl - 1) * character.PtsP;
        var mPoints = (lvl - 1) * character.PtsM;
        var sPoints = (lvl - 1) * character.PtsS;
        var cPoints = (lvl - 1) * character.PtsC;

        var ap = BaseAp + (int)Math.Floor(pPoints * ApPerAxisPoint) + character.ApPoints;
        var ac = BaseAc + (int)Math.Floor(pPoints * AcPerAxisPoint) + character.AcPoints;
        var dx = BaseDx + (int)Math.Truncate(pPoints * DxPerAxisPoint) + character.DxPoints;
        var mp = BaseMp + (int)Math.Floor(mPoints * MpPerAxisPoint) + character.MaxMpPoints;
        var ma = BaseMa + (int)Math.Floor(mPoints * MaPerAxisPoint) + character.MaPoints;
        var md = BaseMd + (int)Math.Floor(mPoints * MdPerAxisPoint) + character.MdPoints;
        var wt = BaseWt + (int)Math.Floor(sPoints * WtPerAxisPoint) + character.MaxWtPoints;
        var da = BaseDa + (int)Math.Floor(sPoints * DaPerAxisPoint) + character.DaPoints;
        var lk = BaseLk + (int)Math.Floor(sPoints * LkPerAxisPoint) + character.LkPoints;
        var hp = BaseHp + (int)Math.Floor(cPoints * HpPerAxisPoint) + character.MaxHpPoints;
        var dp = BaseDp + (int)Math.Floor(cPoints * DpPerAxisPoint) + character.DpPoints;
        var hv = BaseHv + (int)Math.Floor(cPoints * HvPerAxisPoint) + character.HvPoints;

        return new EffectiveStats(ap, ac, dx, mp, ma, md, wt, da, lk, hp, dp, hv);
    }
}
