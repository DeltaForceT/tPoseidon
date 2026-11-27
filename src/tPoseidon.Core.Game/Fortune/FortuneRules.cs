using System.Globalization;
using System.Text;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Fortune;

public sealed record FortuneBonus(IReadOnlyList<int> Stats, int Refine, int Drop, int Exp)
{
    public static FortuneBonus None { get; } = new(new int[FortuneRules.StatSlots], 0, 0, 0);
}

public sealed record FortuneAward(string Text, long DurationSeconds, int TitleId, bool ThreeOfAKind);

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
    public const int MostDropRolls = 9;
    public const double ExpRateFactor = 0.009999999776482582;
    public const float DrawnWeightFactor = 0.3f;
    public const float MinimumWeight = 0.01f;
    public const int StarTearsEffect = 37;
    public const int SecondsPerHour = 3600;
    public const int UsesResetLength = 100;
    public const int TitleCountCategory = 15;
    public const int TitleNoCategory = 0;
    public const int TitleRankCategory = 1;
    public const int TitleCardCategory = 2;
    public const int TitleFirstAttributeCategory = 3;
    public const int TitleLastAttributeCategory = 14;
    public const int LevelStep = 40;
    public const string RefineName = "RFIT";
    public const string DropName = "DROP";
    public const string ExpName = "EXP";

    private static readonly string[] TitleAttributes = ["AP", "AC", "DX", "MA", "MD", "DA", "LK", "DP", "HV", RefineName, DropName, ExpName];

    private static readonly HashSet<string> ScaledAttributes = new(["AP", "AC", "DX", "MA", "MD", "DA", "LK", "DP", "HV"], StringComparer.OrdinalIgnoreCase);

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

    public static bool TryGetStatSlot(string name, out int slot) => StatSlotByName.TryGetValue(name, out slot);

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

    public static bool StoredAwardUsable(string award)
    {
        ArgumentNullException.ThrowIfNull(award);
        return award.Length < UsesResetLength;
    }

    public static float ExpRate(int percent) =>
        percent > 0 ? (float)(percent * ExpRateFactor) : 0f;

    public static int DropExtra(int rolls, int percent, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        var extra = percent > 0 && KrRand.Below(rng, 100) < percent ? 1 : 0;
        return rolls + extra > MostDropRolls ? 0 : extra;
    }

    public static long ExpExtra(long baseGain, int percent)
    {
        var rate = ExpRate(percent);
        if (baseGain <= 0 || rate <= 0f) return 0;
        return (long)Math.Truncate(baseGain * (double)rate);
    }

    public static bool IsActive(DateTimeOffset now, DateTimeOffset expiry) => now <= expiry;

    public static FortuneAward Award(IReadOnlyList<FortuneResult> results, FortuneTables tables, IItemParamLookup catalog, int level)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(catalog);
        var text = new StringBuilder();
        long duration = 0;
        foreach (var result in results)
        {
            if (!tables.Cards.TryGetValue((int)result.CardId, out var card) || card.Attribute.Length == 0) continue;
            Append(text, card.Attribute, Scaled(card.Attribute, card.Value, level));
            duration += card.Duration;
        }
        var titleId = Title(results, tables, catalog);
        var threeOfAKind = false;
        if (results.Count == ResultCount && results.All(result => result.CardId == results[0].CardId)
            && tables.Cards.TryGetValue((int)results[0].CardId, out var same))
        {
            threeOfAKind = true;
            foreach (var cross in same.Cross)
            {
                if (cross.Attribute.Length > 0 && cross.Value != 0f) Append(text, cross.Attribute, Scaled(cross.Attribute, cross.Value, level));
            }
        }
        return new FortuneAward(text.ToString(), duration, titleId, threeOfAKind);
    }

    public static double Scaled(string attribute, float value, int level)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        return ScaledAttributes.Contains(attribute) ? (double)value * (level / LevelStep + 1) : value;
    }

    public static int Title(IReadOnlyList<FortuneResult> results, FortuneTables tables, IItemParamLookup catalog)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(catalog);
        var ids = results.Select(result => (int)result.CardId).ToArray();
        foreach (var rule in tables.Titles.OrderBy(rule => rule.Index))
        {
            if (Matches(rule, ids, tables, catalog)) return rule.TitleId;
        }
        return 0;
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
        var lift = (float)((double)power / MaxPower) * needleSeed;
        foreach (var pick in picks.Take(ResultCount))
        {
            var index = pick is >= 0 and <= JokerSlot ? pick : JokerSlot;
            if (index < weights.Length) weights[index] += lift;
        }
        for (var i = 0; i < cards.Count; i++)
        {
            if (cards[i].JokerSet != 0) weights[i] = (float)((double)power * myCardSeed / MaxPower + jokerWeight(cards[i]));
        }
        Normalize(weights);
        var upper = Bounds(weights);
        var results = new List<FortuneResult>(ResultCount);
        var jokerHit = false;
        for (var draw = 0; draw < ResultCount; draw++)
        {
            var roll = rand(RangeScale);
            var chosen = Array.FindIndex(upper, high => roll < high);
            if (chosen < 0) chosen = upper.Length - 1;
            var card = cards[chosen];
            var joker = card.IsBasic == 0;
            jokerHit |= joker;
            results.Add(new FortuneResult(chosen, card.CardId, joker));
            if (joker) continue;
            weights[chosen] *= DrawnWeightFactor;
            if (weights[chosen] < MinimumWeight) weights[chosen] = MinimumWeight;
            Normalize(weights);
            upper = Bounds(weights);
        }
        return new FortuneDraw(results, jokerHit);
    }

    public static int[] Ranges(IReadOnlyList<float> weights)
    {
        ArgumentNullException.ThrowIfNull(weights);
        var normalized = weights.ToArray();
        Normalize(normalized);
        return Bounds(normalized);
    }

    public static void Normalize(float[] weights)
    {
        ArgumentNullException.ThrowIfNull(weights);
        var total = 0f;
        foreach (var weight in weights) total += weight;
        if (!(total > 0f)) return;
        for (var i = 0; i < weights.Length; i++) weights[i] /= total;
    }

    public static int[] Bounds(IReadOnlyList<float> weights)
    {
        ArgumentNullException.ThrowIfNull(weights);
        var upper = new int[weights.Count];
        var lower = 0;
        for (var i = 0; i < weights.Count; i++)
        {
            lower += (int)Math.Truncate((double)weights[i] * RangeScale);
            upper[i] = lower;
        }
        return upper;
    }

    private static bool Matches(FortuneTitleRule rule, IReadOnlyList<int> ids, FortuneTables tables, IItemParamLookup catalog)
    {
        var count = rule.Conditions.FirstOrDefault(condition => condition.Category == TitleCountCategory);
        if (count.Category == TitleCountCategory)
        {
            var remaining = ids.ToList();
            foreach (var condition in rule.Conditions)
            {
                if (remaining.Count == 0) break;
                switch (condition.Category)
                {
                    case TitleRankCategory:
                        remaining.RemoveAll(id => (catalog.GetById(id)?.CardNum ?? int.MinValue) != condition.Value);
                        break;
                    case TitleCardCategory:
                        remaining.RemoveAll(id => id != condition.Value);
                        break;
                    case >= TitleFirstAttributeCategory and <= TitleLastAttributeCategory:
                        var name = TitleAttributes[condition.Category - TitleFirstAttributeCategory];
                        remaining.RemoveAll(id => !tables.Cards.TryGetValue(id, out var card) || !string.Equals(card.Attribute, name, StringComparison.OrdinalIgnoreCase));
                        break;
                }
            }
            return remaining.Count >= count.Value;
        }
        var wanted = rule.Conditions.Where(condition => condition.Category == TitleCardCategory).ToArray();
        return wanted.Sum(condition => ids.Count(id => id == condition.Value)) >= wanted.Length;
    }

    private static void Append(StringBuilder text, string attribute, double value)
    {
        if (text.Length > 0) text.Append(' ');
        text.Append(attribute).Append('|')
            .Append(Math.Round(value, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture));
    }
}
