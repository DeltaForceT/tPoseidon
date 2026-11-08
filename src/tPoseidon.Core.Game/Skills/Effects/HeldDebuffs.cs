namespace tPoseidon.Core.Game.Skills.Effects;

public static class HeldDebuffs
{
    public static readonly IReadOnlyList<sbyte> ClassesClearedOnDeath = new sbyte[] { 0, 1, 7 };

    public static bool IsHeldDebuff(SkillEffect effect) => effect.Kind.HeldDebuff;

    public static bool ClearedOnDeath(SkillEffect effect) =>
        IsHeldDebuff(effect) && ClassesClearedOnDeath.Contains(effect.EsClass);

    public static int ConditionOf(IEnumerable<SkillEffect> held) =>
        held.Where(effect => effect.IsApplied && !effect.EndRequested && !effect.ConditionSuppressed && effect.ElapsedMs >= effect.Kind.HoldDelayMs)
            .Aggregate(0, (mask, effect) => mask | effect.Kind.PlayerCondition);

    public static int EndAll(IEnumerable<SkillEffect> held)
    {
        var ended = 0;
        foreach (var effect in held.Where(ClearedOnDeath).Where(effect => !effect.EndRequested))
        {
            effect.EndNow();
            ended++;
        }
        return ended;
    }
}
