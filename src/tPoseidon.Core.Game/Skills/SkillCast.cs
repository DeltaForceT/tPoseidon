namespace tPoseidon.Core.Game.Skills;

public enum SkillCastPhase
{
    AwaitingTarget,
    Chasing,
    Casting,
}

public sealed class SkillCast
{
    public SkillCast(SkillDefinition skill, int level)
    {
        ArgumentNullException.ThrowIfNull(skill);
        Skill = skill;
        Level = level;
    }

    public SkillDefinition Skill { get; }

    public int Level { get; }

    public SkillCastPhase Phase { get; private set; } = SkillCastPhase.AwaitingTarget;

    public ushort TargetEntityId { get; private set; }

    public int DelayLeftMs { get; private set; }

    public int ChasedMs { get; private set; }

    public (ushort X, ushort Y) ChaseEnd { get; private set; }

    public DateTimeOffset ChaseArrivesAt { get; private set; }

    public DateTimeOffset TickedAt { get; private set; }

    public ushort ResumeAttackOn { get; set; }

    public (ushort X, ushort Y)? TargetPoint { get; set; }

    public bool IsUnderWay => Phase != SkillCastPhase.AwaitingTarget;

    public void Chase(ushort targetEntityId, ushort toX, ushort toY, DateTimeOffset arrivesAt, DateTimeOffset now)
    {
        if (Phase == SkillCastPhase.AwaitingTarget) TickedAt = now;
        TargetEntityId = targetEntityId;
        ChaseEnd = (toX, toY);
        ChaseArrivesAt = arrivesAt;
        Phase = SkillCastPhase.Chasing;
    }

    public void StartCasting(ushort targetEntityId, DateTimeOffset now)
    {
        TargetEntityId = targetEntityId;
        DelayLeftMs = SkillChecks.DelayMsOf(Skill.ConditionsAt(Level));
        TickedAt = now;
        Phase = SkillCastPhase.Casting;
    }

    public int Elapse(DateTimeOffset now)
    {
        var elapsed = (int)Math.Max(0, (now - TickedAt).TotalMilliseconds);
        TickedAt = now;
        return elapsed;
    }

    public bool ChaseTimedOut(int elapsedMs)
    {
        ChasedMs += elapsedMs;
        return ChasedMs > SkillChecks.ChaseLimitMs;
    }

    public bool DelayIsOver(int elapsedMs)
    {
        DelayLeftMs -= elapsedMs;
        return DelayLeftMs < 1;
    }
}
