using System.Globalization;
using System.Text;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Fortune;

public sealed record FortuneBonus(IReadOnlyList<int> Stats, int Refine, int Drop, int Exp)
{
    public static FortuneBonus None { get; } = new(new int[FortuneRules.StatSlots], 0, 0, 0);
}

public sealed record FortuneAward(string Text, long DurationSeconds, int TitleId, bool TitleDetermined, bool ThreeOfAKind);

public readonly record struct FortuneCard(ushort JokerSet, ushort IsBasic, uint CardId, ulong ItemUid, float Weight);

public readonly record struct FortuneResult(int CardIndex, uint CardId, bool Joker);

public sealed record FortuneDraw(IReadOnlyList<FortuneResult> Results, bool JokerHit);

public static class FortuneRules
{
    public const int StatSlots = 12;
    public const int CardCount = 11;
    public const int JokerSlot = 10;
    public const int ResultCount = 3;
    public const int MaxPower = 5000;
    public const int RangeScale = 10000;
    public const float DrawnWeightFactor = 0.3f;
    public const float MinimumWeight = 0.01f;
    public const int StarTearsEffect = 37;
    public const int SecondsPerHour = 3600;
    public const int UsesResetLength = 100;
    public const int TitleCountCategory = 15;
    public const int TitleNoCategory = 0;
    public const int TitleCardCategory = 2;
    public const string RefineName = "RFIT";
    public const string DropName = "DROP";
    public const string ExpName = "EXP";

    private static readonly char[] Delimiters = ['\r', '\n', '\t', ' ', '|'];

    private static readonly Dictionary<string, int> StatSlotByName = new(StringComparer.Ordinal)
    {
        ["AP"] = 0,
        ["AC"] = 1,
        ["DX"] = 2,
        ["MA"] = 4,
        ["MD"] = 5,
        ["DA"] = 7,
        ["LK"] = 8,
        ["DP"] = 10,
        ["HV"] = 11,
    };

    public static FortuneBonus Parse(string award)
    {
        ArgumentNullException.ThrowIfNull(award);
        var stats = new int[StatSlots];
        int refine = 0, drop = 0, exp = 0;
        var tokens = award.Split(Delimiters, StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i + 1 < tokens.Length; i += 2)
        {
            if (!int.TryParse(tokens[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) continue;
            var name = tokens[i];
            if (StatSlotByName.TryGetValue(name, out var slot)) stats[slot] += value;
            else if (name == RefineName) refine += value;
            else if (name == DropName) drop += value;
            else if (name == ExpName) exp += value;
        }
        return new FortuneBonus(stats, refine, drop, exp);
    }

    public static bool IsActive(DateTimeOffset now, DateTimeOffset expiry) => now <= expiry;

    public static FortuneAward Award(IReadOnlyList<FortuneResult> results, FortuneTables tables)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(tables);
        var text = new StringBuilder();
        long duration = 0;
        foreach (var result in results)
        {
            if (!tables.Cards.TryGetValue((int)result.CardId, out var card) || card.Attribute.Length == 0) continue;
            Append(text, card.Attribute, card.Value);
            duration += card.Duration;
        }
        var (titleId, determined) = Title(results, tables);
        var threeOfAKind = false;
        if (results.Count == ResultCount && results.All(result => result.CardId == results[0].CardId)
            && tables.Cards.TryGetValue((int)results[0].CardId, out var same) && same.Cross.Any(cross => cross.Attribute.Length > 0))
        {
            threeOfAKind = true;
            foreach (var cross in same.Cross)
            {
                if (cross.Attribute.Length > 0 && cross.Value != 0f) Append(text, cross.Attribute, cross.Value);
            }
        }
        return new FortuneAward(text.ToString(), duration, titleId, determined, threeOfAKind);
    }

    public static (int TitleId, bool Determined) Title(IReadOnlyList<FortuneResult> results, FortuneTables tables)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(tables);
        var ids = results.Select(result => (int)result.CardId).ToArray();
        foreach (var rule in tables.Titles.OrderBy(rule => rule.Index))
        {
            if (rule.Conditions.Any(condition => condition.Category is not (TitleNoCategory or TitleCardCategory or TitleCountCategory))) return (0, false);
            if (Matches(rule, ids)) return (rule.TitleId, true);
        }
        return (0, true);
    }

    public static FortuneDraw Draw(int power, IReadOnlyList<int> picks, IReadOnlyList<FortuneCard> cards, float needleSeed, float myCardSeed,
        Func<FortuneCard, float> jokerWeight, Func<int, int> rand)
    {
        ArgumentNullException.ThrowIfNull(picks);
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(jokerWeight);
        ArgumentNullException.ThrowIfNull(rand);
        power = Math.Clamp(power, 0, MaxPower);
        var weights = cards.Select(card => card.Weight).ToArray();
        foreach (var pick in picks.Take(ResultCount))
        {
            var index = pick is >= 0 and <= JokerSlot ? pick : JokerSlot;
            if (index < weights.Length) weights[index] += power / (float)MaxPower * needleSeed;
        }
        for (var i = 0; i < cards.Count; i++)
        {
            if (cards[i].JokerSet != 0) weights[i] = jokerWeight(cards[i]) + power * myCardSeed / MaxPower;
        }
        var results = new List<FortuneResult>(ResultCount);
        var jokerHit = false;
        for (var draw = 0; draw < ResultCount; draw++)
        {
            var upper = Ranges(weights);
            var roll = rand(RangeScale);
            var chosen = Array.FindIndex(upper, high => roll < high);
            if (chosen < 0) chosen = upper.Length - 1;
            var card = cards[chosen];
            var joker = card.IsBasic == 0;
            jokerHit |= joker;
            results.Add(new FortuneResult(chosen, card.CardId, joker));
            if (!joker) weights[chosen] = Math.Max(MinimumWeight, weights[chosen] * DrawnWeightFactor);
        }
        return new FortuneDraw(results, jokerHit);
    }

    public static int[] Ranges(IReadOnlyList<float> weights)
    {
        ArgumentNullException.ThrowIfNull(weights);
        var total = weights.Sum(weight => (double)weight);
        var upper = new int[weights.Count];
        double cumulative = 0;
        for (var i = 0; i < weights.Count; i++)
        {
            cumulative += total > 0 ? weights[i] / total : 0;
            upper[i] = (int)Math.Round(cumulative * RangeScale, MidpointRounding.AwayFromZero);
        }
        return upper;
    }

    private static bool Matches(FortuneTitleRule rule, IReadOnlyList<int> ids)
    {
        var count = rule.Conditions.FirstOrDefault(condition => condition.Category == TitleCountCategory);
        if (count.Category == TitleCountCategory)
        {
            IEnumerable<int> remaining = ids;
            foreach (var condition in rule.Conditions)
            {
                if (condition.Category == TitleCardCategory) remaining = remaining.Where(id => id == condition.Value).ToArray();
            }
            return remaining.Count() >= count.Value;
        }
        return rule.Conditions.Where(condition => condition.Category == TitleCardCategory).All(condition => ids.Contains(condition.Value));
    }

    private static void Append(StringBuilder text, string attribute, float value)
    {
        if (text.Length > 0) text.Append(' ');
        text.Append(attribute).Append('|')
            .Append(Math.Round((double)value, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture));
    }
}
