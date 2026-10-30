using tPoseidon.Core.Data;

namespace tPoseidon.Core.Game.Skills;

public sealed record HeldSkill(int SkillId, int Level);

public sealed record SkillListChange(IReadOnlyList<int> Removed, IReadOnlyList<HeldSkill> Learned, IReadOnlyList<HeldSkill> Levelled)
{
    public bool IsEmpty => Removed.Count == 0 && Learned.Count == 0 && Levelled.Count == 0;

    public static SkillListChange Between(IReadOnlyDictionary<int, int> before, IReadOnlyDictionary<int, int> after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        var removed = before.Keys.Where(id => !after.ContainsKey(id)).Order().ToList();
        var learned = after.Where(pair => !before.ContainsKey(pair.Key)).OrderBy(pair => pair.Key).Select(pair => new HeldSkill(pair.Key, pair.Value)).ToList();
        var levelled = after.Where(pair => before.TryGetValue(pair.Key, out var level) && level != pair.Value).OrderBy(pair => pair.Key).Select(pair => new HeldSkill(pair.Key, pair.Value)).ToList();
        return new SkillListChange(removed, learned, levelled);
    }
}

public static class GmSkillSet
{
    public const int FirstSkillId = 20000;
    public const int LastSkillId = 20003;

    public static bool Contains(int skillId) => skillId is >= FirstSkillId and <= LastSkillId;

    public static IReadOnlySet<int> Ids { get; } = Enumerable.Range(FirstSkillId, LastSkillId - FirstSkillId + 1).ToHashSet();

    public static IReadOnlyList<Skill> Of(SkillBook book, ulong characterId)
    {
        ArgumentNullException.ThrowIfNull(book);
        return Ids.Order()
            .Select(id => book.Find(id))
            .OfType<SkillDefinition>()
            .Select(definition => new Skill
            {
                CharacterId = characterId,
                SkillId = definition.Id,
                SkillLevel = SkillGrantRules.HighestLevelOf(definition),
            })
            .ToList();
    }
}
