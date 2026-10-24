using tPoseidon.Core.Game.Combat;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Skills;

public readonly record struct CrazyGaugeFill(bool Changed, bool Burst, int Current);

public sealed class CrazyHammerGauge
{
    private readonly object _gate = new();
    private ushort _objectId;
    private ushort _announcedOnMap;
    private int _current;

    public ushort ObjectId
    {
        get { lock (_gate) return _objectId; }
    }

    public int Current
    {
        get { lock (_gate) return _current; }
    }

    public bool NeedsAnnounce(ushort mapId)
    {
        lock (_gate) return _objectId == 0 || _announcedOnMap != mapId;
    }

    public ushort Announce(ushort objectId, ushort mapId)
    {
        lock (_gate)
        {
            var previous = _objectId;
            _objectId = objectId;
            _announcedOnMap = mapId;
            return previous;
        }
    }

    public CrazyGaugeFill Fill(int points, int max)
    {
        lock (_gate)
        {
            if (points <= 0 || max <= 0) return new CrazyGaugeFill(false, false, _current);
            _current = Math.Min(max, _current + points);
            if (_current < max) return new CrazyGaugeFill(true, false, _current);
            _current = 0;
            return new CrazyGaugeFill(true, true, 0);
        }
    }
}

public static class CrazyHammer
{
    public const int CrazyHammerSkill = 4801;
    public const int DoubleCrazySkill = 4804;
    public const int TripleCrazySkill = 4806;
    public const int QuadrupleCrazySkill = 4908;
    public const int QuintupleCrazySkill = 4910;
    public const int GaugeAction = 1532;
    public const int ModeAction = 1533;

    public const string GaugeColumn = "GaugeP";
    public const string AttackPointColumn = "AtPoint";
    public const string SkillPointColumn = "SkillPoint";
    public const string RatioColumn = "Ratio";
    public const string CriticalRatioColumn = "CriticalRatio";

    private const double PercentPerRatio = 100.0;

    public static readonly AreaShape Shockwave = new(AreaCentre.Target, SkillAreaShapes.PolarBearAreaLimit, AreaTail.None, AreaTm.None)
    {
        LimitColumn = SkillAreaShapes.KillNumColumn, Target = AreaTarget.AlwaysFirst,
    };

    public static int NormalAttackAp(int ap, int baseAp, SkillTableRow crazy, SkillTableRow? triple)
    {
        ArgumentNullException.ThrowIfNull(crazy);
        var bonus = baseAp * (crazy.Value(RatioColumn) ?? 0f) + baseAp * (triple?.Value(RatioColumn) ?? 0f);
        return ap + (int)bonus;
    }

    public static int CriticalBonus(SkillTableRow? quadruple) =>
        (int)Math.Round((quadruple?.Value(CriticalRatioColumn) ?? 0f) * PercentPerRatio);

    public static int ShockwaveDamage(int ap, int targetDp) => MeleeResolution.Normal(ap, targetDp);
}
