using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Skills;

public sealed record PolarBearOutcome(IReadOnlyList<SkillHit> Hits)
{
    public int Sum => Hits.Where(Counts).Sum(hit => hit.Damage);

    public int Landed => Hits.Count(Counts);

    private static bool Counts(SkillHit hit) => hit.Damage > 0 && hit.Kind != SkillDamage.KindMiss;
}

public static class PolarBearHits
{
    public const int DoubleHammer = 1523;
    public const int OverSwing = 1535;

    public const int DoubleHammerHits = 2;
    public const int OverSwingHits = 3;

    public const string RatioColumn = "Ratio";
    public const string ApplyRatioColumn = "ApplyRatio";
    public const string DaRatioColumn = "AdRatio";

    public static int CountOf(int actionId) => actionId switch
    {
        DoubleHammer => DoubleHammerHits,
        OverSwing => OverSwingHits,
        _ => 0,
    };

    public static int PowerOf(int actionId, SkillTableRow action, SkillAttacker attacker, int hitIndex)
    {
        ArgumentNullException.ThrowIfNull(action);
        var attack = attacker.Ap * (double)(action.Value(RatioColumn) ?? 0f);
        var dig = actionId == OverSwing ? attacker.Da * (double)(action.Value(DaRatioColumn) ?? 0f) * hitIndex : 0.0;
        return (int)Math.Clamp(Math.Truncate(attack + dig), int.MinValue, int.MaxValue);
    }

    public static PolarBearOutcome Resolve(int actionId, SkillTableRow action, SkillAttacker attacker, SkillTarget target, Random rng)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(rng);
        var coefficient = action.Value(ApplyRatioColumn) ?? 0f;
        var hits = new List<SkillHit>(CountOf(actionId));
        for (var hitIndex = 0; hitIndex < CountOf(actionId); hitIndex++)
        {
            hits.Add(SkillDamage.Physical(attacker, PowerOf(actionId, action, attacker, hitIndex), target, coefficient, SkillActions.NoElement, rng));
        }
        return new PolarBearOutcome(hits);
    }
}
