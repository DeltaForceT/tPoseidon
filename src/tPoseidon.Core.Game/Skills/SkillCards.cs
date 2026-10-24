using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Skills;

public static class SkillCardRules
{
    public const int CardType = 28;
    public const int CardSubType = 55;
    public const int NoJob = 0;

    public const byte LearnNotice = 5;
    public const ushort AlreadyOwnedNotice = 1;
    public const ushort TmPointsNotice = 2;
    public const ushort PreSkillNotice = 7;
    public const ushort JobNotice = 9;

    private static readonly Dictionary<int, int> JobTypeByCardType = new()
    {
        [2] = 1, [3] = 2, [4] = 10, [5] = 11, [6] = 19, [7] = 20, [8] = 22, [9] = 21,
        [12] = 3, [13] = 4, [14] = 12, [15] = 13, [16] = 23, [17] = 24, [18] = 27, [19] = 25, [20] = 26,
        [23] = 5, [24] = 6, [25] = 14, [26] = 15, [27] = 28, [28] = 29, [29] = 31, [30] = 30,
        [33] = 7, [34] = 8, [35] = 16, [36] = 17, [37] = 32, [38] = 33, [39] = 35, [40] = 34,
        [43] = 9, [44] = 18, [45] = 36,
    };

    public static int JobTypeOf(int cardType) => JobTypeByCardType.TryGetValue(cardType, out var job) ? job : NoJob;

    public static int ParentOf(int jobType) => jobType switch
    {
        >= 10 and <= 18 => jobType - 9,
        19 or 20 => 10,
        21 or 22 => 11,
        23 or 24 => 12,
        25 or 26 or 27 => 13,
        28 or 29 => 14,
        30 or 31 => 15,
        32 or 33 => 16,
        34 or 35 => 17,
        36 => 18,
        _ => NoJob,
    };

    public static bool PairPasses(int playerJobType, int tmLevel, int cardType, int requiredTmLevel)
    {
        var target = JobTypeOf(cardType);
        if (target == NoJob || tmLevel < requiredTmLevel) return false;
        for (var job = playerJobType; job != NoJob; job = ParentOf(job))
        {
            if (job == target) return true;
        }
        return false;
    }

    public static bool JobPasses(SkillCardEntry card, int playerJobType, int tmLevel)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (card.CardType == 0 && card.SubCardType == 0) return true;
        return PairPasses(playerJobType, tmLevel, card.CardType, card.TmLevel)
            || PairPasses(playerJobType, tmLevel, card.SubCardType, card.SubTmLevel);
    }

    public static ushort? Refusal(SkillCardEntry card, IReadOnlyDictionary<int, int> skills, int playerJobType, int tmLevel, int tmPoints)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(skills);
        if (skills.ContainsKey(card.AffectionSkill)) return AlreadyOwnedNotice;
        if (!JobPasses(card, playerJobType, tmLevel)) return JobNotice;
        if (tmPoints < card.AffectionPoint) return TmPointsNotice;
        foreach (var pre in card.PreSkills)
        {
            if (pre.SkillId == 0) continue;
            if (!skills.TryGetValue(pre.SkillId, out var level) || level < pre.Level) return PreSkillNotice;
        }
        return null;
    }
}
