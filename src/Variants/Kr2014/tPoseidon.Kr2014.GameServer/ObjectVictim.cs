using tPoseidon.Core.Game.Skills;

namespace tPoseidon.Kr2014.GameServer;

public sealed record ObjectVictim(ushort EntityId, int X, int Y, bool IsPlayer, Func<int, SkillTarget> TargetFor)
{
    public SkillAttacker AttackerFor(SkillAttacker attacker) => IsPlayer ? attacker with { AlwaysHits = true } : attacker;
}
