namespace tPoseidon.Core.Game.Skills;

public static class ElementSlots
{
    public const int None = -1;
    public const int FirstResistSlot = 11;
    public const int ResistCap = 70;
    public const int Fire = 0;
    public const int Water = 1;
    public const int Wind = 2;
    public const int Earth = 3;
    public const int Electric = 4;
    public const int Dark = 6;

    public static readonly IReadOnlyList<int> ElementUpSlots = new[] { Fire, Water, Wind, Earth, Electric };
    public static readonly IReadOnlyList<int> DarkUpSlots = new[] { Dark };

    public static int AttackSlotOf(int elementCode) => elementCode switch
    {
        SkillActions.FireElement => 0,
        SkillActions.WaterElement => 1,
        SkillActions.WindElement => 2,
        SkillActions.EarthElement => 3,
        SkillActions.ElectricElement => 4,
        SkillActions.LightElement => 5,
        SkillActions.DarkElement => 6,
        SkillActions.NoPropertyElement => 7,
        _ => None,
    };

    public static int ResistSlotOf(int elementCode) => elementCode switch
    {
        SkillActions.NoElement => 0x14,
        SkillActions.FireElement => 0x0B,
        SkillActions.WaterElement => 0x0C,
        SkillActions.WindElement => 0x0D,
        SkillActions.EarthElement => 0x0E,
        SkillActions.ElectricElement => 0x0F,
        SkillActions.LightElement => 0x10,
        SkillActions.DarkElement => 0x11,
        SkillActions.NoPropertyElement => 0x12,
        SkillActions.GunElement => 0x13,
        SkillActions.ShadowElement => 0x15,
        _ => None,
    };

    public static int Total(int slot, int equipment, int bonus)
    {
        var total = equipment + bonus;
        return slot >= FirstResistSlot ? Math.Min(total, ResistCap) : total;
    }

    public static int Boost(int total, float ratio) => (int)(total * (double)ratio / 100.0);
}
