using tPoseidon.Core.Game.Combat;

namespace tPoseidon.Core.Game.Skills;

public static class SkillLineOfFire
{
    public static int ModeOf(int actionId) => actionId == SkillHitShapes.BeastClaw ? LineOfFire.LevelGroundMode : LineOfFire.Unchecked;
}
