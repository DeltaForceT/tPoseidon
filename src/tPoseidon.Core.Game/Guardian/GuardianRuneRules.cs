using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Guardian;

public static class GuardianRuneRules
{
    public const short DefaultLevel = 1;

    private static readonly (int First, int Last, byte Type)[] TypeRanges =
    [
        (9000, 9004, 1),
        (10000, 10004, 2),
        (11000, 11000, 6),
        (12000, 12000, 7),
        (13000, 13001, 3),
        (14000, 14000, 4),
        (15000, 15003, 5),
        (16000, 16000, 4),
        (17000, 17000, 9),
        (18000, 18000, 10),
    ];

    public static byte TypeOf(int runeSkillId)
    {
        foreach (var (first, last, type) in TypeRanges)
        {
            if (runeSkillId >= first && runeSkillId <= last) return type;
        }
        return 0;
    }

    public static IReadOnlyList<GuardianSkill> Defaults(GuardianParamEntry? param)
    {
        if (param is null) return [];
        var runes = new List<GuardianSkill>();
        foreach (var skillId in param.DefaultRuneSkills)
        {
            if (skillId == 0) continue;
            runes.Add(new GuardianSkill(skillId, GuardianSkillType.Special, TypeOf(skillId), DefaultLevel, (byte)runes.Count));
        }
        return runes;
    }
}
