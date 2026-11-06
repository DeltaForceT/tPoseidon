namespace tPoseidon.Core.Game.Skills;

public static class CastInterruption
{
    public static bool Interrupts(bool fatal, bool skillCancelledByDamage, bool holdsInvincibleCasting) =>
        fatal || (skillCancelledByDamage && !holdsInvincibleCasting);

    public static bool Interrupts(SkillDefinition skill, bool fatal, bool holdsInvincibleCasting)
    {
        ArgumentNullException.ThrowIfNull(skill);
        return Interrupts(fatal, skill.Has(SkillDefinition.CancelledByDamageOption), holdsInvincibleCasting);
    }
}
