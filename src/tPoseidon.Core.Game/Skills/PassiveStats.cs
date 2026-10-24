using tPoseidon.Core.Game.Skills.Effects;

namespace tPoseidon.Core.Game.Skills;

public static class PassiveStats
{
    public const int ManaBottle = 1084;
    public const int BigBaggage = 1091;
    public const int Fitness = 1169;

    private static readonly Dictionary<int, (int Stat, string Column)> Flat = new()
    {
        [ManaBottle] = (SkillEffectKinds.StatMp, "MP"),
        [BigBaggage] = (SkillEffectKinds.StatWt, "WT"),
        [Fitness] = (SkillEffectKinds.StatHp, "HP"),
    };

    public static bool IsStatPassive(int actionId) => Flat.ContainsKey(actionId);

    public static int[] Bonuses(IReadOnlyDictionary<int, int> learned, SkillBook book)
    {
        ArgumentNullException.ThrowIfNull(learned);
        ArgumentNullException.ThrowIfNull(book);
        var bonuses = new int[SkillEffectKinds.StatCount];
        foreach (var (skillId, level) in learned)
        {
            if (level <= 0 || book.Find(skillId) is not { IsPassive: true } skill || !Flat.TryGetValue(skill.ActionId, out var flat)) continue;
            bonuses[flat.Stat] += skill.ActionAt(level)?.Amount(flat.Column) ?? 0;
        }
        return bonuses;
    }

    public static IReadOnlyList<int> StatsOf(IReadOnlyDictionary<int, int> learned, SkillBook book)
    {
        ArgumentNullException.ThrowIfNull(learned);
        ArgumentNullException.ThrowIfNull(book);
        return learned
            .Where(entry => entry.Value > 0 && book.Find(entry.Key) is { IsPassive: true } skill && Flat.ContainsKey(skill.ActionId))
            .Select(entry => Flat[book.Find(entry.Key)!.ActionId].Stat)
            .Distinct()
            .Order()
            .ToList();
    }
}
