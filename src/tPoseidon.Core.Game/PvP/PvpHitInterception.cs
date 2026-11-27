using tPoseidon.Core.Game.Combat;
using tPoseidon.Core.Game.Skills;
using tPoseidon.Core.Game.Skills.Effects;

namespace tPoseidon.Core.Game.PvP;

public sealed record PvpSkillInterception(SkillHit Hit, HitInterception Interception);

public sealed record PvpMeleeInterception(MeleeOutcome Outcome, ushort GuardLeft, HitInterception Interception);

public static class PvpHitInterception
{
    public static PvpSkillInterception Skill(
        SkillHit hit, IEnumerable<SkillEffect> effects, HitDefender defender, long victimHp, Func<ushort, long> casterHv)
    {
        var incoming = new IncomingHit(hit.Damage, hit.Type, hit.Kind & SkillDamage.KindMask, hit.Element, (uint)hit.Flags);
        var interception = IncomingHits.Intercept(incoming, effects, defender, casterHv);
        if (interception.Hit == incoming) return new PvpSkillInterception(hit, interception);
        var received = interception.Hit;
        var finishing = received.Damage >= victimHp ? SkillDamage.KindFinishingFlag : 0;
        var guardLeft = received.Kind == IncomingHits.KindBlockedByGuard ? received.Extra : (ushort)0;
        return new PvpSkillInterception(
            hit with { Damage = received.Damage, Kind = received.Kind | finishing, Element = received.Element, Flags = (int)received.Flags, GuardLeft = guardLeft },
            interception);
    }

    public static PvpMeleeInterception Melee(
        MeleeOutcome outcome, IEnumerable<SkillEffect> effects, HitDefender defender, long victimHp, Func<ushort, long> casterHv)
    {
        var incoming = new IncomingHit(
            outcome.Damage, IncomingHits.BasicAttackType, outcome.Result & MeleeResolution.ResultKindMask, SkillActions.NoElement, 0);
        var interception = IncomingHits.Intercept(incoming, effects, defender, casterHv);
        if (interception.Hit == incoming) return new PvpMeleeInterception(outcome, 0, interception);
        var received = interception.Hit;
        var finishing = received.Damage >= victimHp ? MeleeResolution.ResultFinishingFlag : 0;
        var guardLeft = received.Kind == IncomingHits.KindBlockedByGuard ? received.Extra : (ushort)0;
        return new PvpMeleeInterception(
            outcome with { Damage = received.Damage, Result = (byte)(received.Kind | finishing) }, guardLeft, interception);
    }
}
