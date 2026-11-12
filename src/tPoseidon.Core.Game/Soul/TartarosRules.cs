using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Soul;

public readonly record struct TartarosBuff(int Category, int SkillId, int Level, float Ratio, bool Good);

public readonly record struct TartarosSoulItem(int ItemType, int SoulLevel, int Remaining);

public static class TartarosRules
{
    public const int FirstGoodSkill = 5047;
    public const int FirstBadSkill = 5051;
    public const int Categories = 4;
    public const int NoCategory = -1;

    public static int CategoryOf(int itemType) => itemType switch
    {
        2 => 0,
        3 => 1,
        8 => 2,
        7 => 3,
        _ => NoCategory,
    };

    public static int CategoryOfSkill(int skillId) =>
        skillId >= FirstBadSkill && skillId < FirstBadSkill + Categories ? skillId - FirstBadSkill
        : skillId >= FirstGoodSkill && skillId < FirstGoodSkill + Categories ? skillId - FirstGoodSkill
        : NoCategory;

    public static IReadOnlyList<TartarosBuff> BuffsOn(
        SoulSystemTables tables, int mapId, IEnumerable<TartarosSoulItem> wornSoulItems)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(wornSoulItems);
        if (!tables.TartarosMapSkills.TryGetValue(mapId, out var mapSkills)) return Array.Empty<TartarosBuff>();
        var good = new Dictionary<int, int>();
        foreach (var soul in wornSoulItems)
        {
            var category = CategoryOf(soul.ItemType);
            if (category == NoCategory || soul.Remaining <= 0) continue;
            var level = Math.Max(1, soul.SoulLevel);
            good[category] = good.TryGetValue(category, out var known) ? Math.Max(known, level) : level;
        }
        var buffs = new List<TartarosBuff>();
        foreach (var skill in mapSkills)
        {
            var category = CategoryOfSkill(skill.SkillId);
            if (category == NoCategory) continue;
            if (good.TryGetValue(category, out var soulLevel))
            {
                buffs.Add(new TartarosBuff(category, FirstGoodSkill + category, soulLevel, RatioAt(tables.GoodBuffRatios, soulLevel), true));
            }
            else
            {
                buffs.Add(new TartarosBuff(category, skill.SkillId, skill.Level, RatioAt(tables.BadBuffRatios, skill.Level), false));
            }
        }
        return buffs;
    }

    public static float RatioAt(IReadOnlyDictionary<int, float> ratios, int level)
    {
        ArgumentNullException.ThrowIfNull(ratios);
        if (ratios.TryGetValue(level, out var ratio)) return ratio;
        return ratios.Count == 0 ? 1f : ratios[ratios.Keys.Where(known => known <= level).DefaultIfEmpty(ratios.Keys.Min()).Max()];
    }

    public static IReadOnlyList<int> Scaled(IReadOnlyList<int> stats, float ratio)
    {
        ArgumentNullException.ThrowIfNull(stats);
        if (ratio == 1f) return stats;
        return stats.Select(value => (int)Math.Truncate(value * (double)(decimal)ratio)).ToArray();
    }
}
