using tPoseidon.Core.Game.Skills;

namespace tPoseidon.Core.Game.Monsters;

public static class BossElements
{
    public const int Physical = 9;
    public const int None = -1;

    private static readonly int[] CodeByIndex =
    {
        SkillActions.FireElement,
        SkillActions.WaterElement,
        SkillActions.WindElement,
        SkillActions.EarthElement,
        SkillActions.ElectricElement,
        SkillActions.LightElement,
        SkillActions.DarkElement,
        SkillActions.NoPropertyElement,
        SkillActions.GunElement,
        SkillActions.NoElement,
        SkillActions.ShadowElement,
    };

    public static int CodeOf(int index) => CodeByIndex[index];

    public static int IndexOf(int elementCode) => Array.IndexOf(CodeByIndex, elementCode) is var index && index >= 0 ? index : None;
}
