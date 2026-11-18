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

    public static bool IsActive(DateTimeOffset now, DateTimeOffset expiry) => now <= expiry;

    public static FortuneAward Award(IReadOnlyList<FortuneResult> results, FortuneTables tables, IItemParamLookup catalog)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(catalog);
        var text = new StringBuilder();
        long duration = 0;
        foreach (var result in results)
        {
            if (!tables.Cards.TryGetValue((int)result.CardId, out var card) || card.Attribute.Length == 0) continue;
            Append(text, card.Attribute, card.Value);
            duration += card.Duration;
        }
        var (titleId, determined) = Title(results, tables, catalog);
        var threeOfAKind = false;
        if (results.Count == ResultCount && results.All(result => result.CardId == results[0].CardId)
            && tables.Cards.TryGetValue((int)results[0].CardId, out var same) && same.Cross.Any(cross => cross.Attribute.Length > 0))
        {
            threeOfAKind = true;
            var item = catalog.GetById((int)results[0].CardId);
            var rankScale = item is not null && item.CardNum > 0 ? item.CardNum : 1;
            foreach (var cross in same.Cross)
            {
                if (cross.Attribute.Length > 0 && cross.Value != 0f)
                {
                    Append(text, cross.Attribute, cross.Value * rankScale);
                }
            }
        }
        return new FortuneAward(text.ToString(), duration, titleId, determined, threeOfAKind);
    }

    public static (int TitleId, bool Determined) Title(IReadOnlyList<FortuneResult> results, FortuneTables tables, IItemParamLookup catalog)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(catalog);
        foreach (var rule in tables.Titles.OrderBy(rule => rule.Index))
        {
            if (TryMatchRule(rule, results, tables, catalog, out var resolvedTitleId))
            {
                return (resolvedTitleId, true);
            }
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

    private static int EffectIndexFromAttribute(string attr) => attr switch
    {
        "AP" => 3,
        "AC" => 4,
        "DX" => 5,
        "MA" => 6,
        "MD" => 7,
        "DA" => 8,
        "LK" => 9,
        "DP" => 10,
        "HV" => 11,
        "RFIT" => 12,
        "DROP" => 13,
        "EXP" => 14,
        _ => -1,
    };

    private static bool TryMatchRule(FortuneTitleRule rule, IReadOnlyList<FortuneResult> results, FortuneTables tables, IItemParamLookup catalog, out int titleId)
    {
        titleId = rule.TitleId;
        var validConditions = rule.Conditions.Where(c => c.Category != TitleNoCategory).ToArray();
        if (validConditions.Length == 0) return false;

        var countCond = validConditions.FirstOrDefault(c => c.Category == TitleCountCategory);
        var requiredCount = countCond.Category == TitleCountCategory ? countCond.Value : 3;
        if (requiredCount <= 0) return false;

        var typeCond = validConditions.FirstOrDefault(c => c.Category == 1);
        var subCond = validConditions.FirstOrDefault(c => c.Category is >= 3 and <= 14);

        // 1) Cat=1:Val=1 -> 같은 카드 3장 일치 (등급 1~9 -> Cat=3~11 -> 실제 타이틀ID 3~11번)
        if (typeCond.Category == 1 && typeCond.Value == 1 && subCond.Category is >= 3 and <= 11 && requiredCount == 3)
        {
            var targetRank = subCond.Category - 2; // Cat=3 -> 1등급(머큐리), Cat=11 -> 9등급(아폴론)
            if (results.Count < 3) return false;
            if (results.Select(r => r.CardId).Distinct().Count() != 1) return false;
            var item = catalog.GetById((int)results[0].CardId);
            if (item is not null && item.CardNum == targetRank)
            {
                // 테이블에 TitleId가 3으로 고정된 버그 보정: Cat=3(3번) ~ Cat=11(11번)
                titleId = subCond.Category;
                return true;
            }
            return false;
        }

        // 2) Cat=1:Val=2 -> 같은 종류(옵션) 3장 일치 (카펠라 ~ 제련신)
        if (typeCond.Category == 1 && typeCond.Value == 2 && subCond.Category is >= 3 and <= 14 && requiredCount == 3)
        {
            var matched = results.Count(r =>
            {
                if (!tables.Cards.TryGetValue((int)r.CardId, out var card)) return false;
                return EffectIndexFromAttribute(card.Attribute) == subCond.Category;
            });
            return matched >= 3;
        }

        // 3) Cat=1:Val=0 -> 대흉의 (0등급 기본 카드 3장)
        if (typeCond.Category == 1 && typeCond.Value == 0 && !validConditions.Any(c => c.Category == TitleCardCategory))
        {
            var basicCount = results.Count(r =>
            {
                var item = catalog.GetById((int)r.CardId);
                return item is not null && item.CardNum == 0;
            });
            return basicCount >= requiredCount;
        }

        // 4) 특정 카드 번호(Cat=2) 2장 또는 3장 일치 (신 내린 제련사 등 - 다른 Cat 조건이 섞여 있어도 Cat=2 우선 검사)
        var cardConds = validConditions.Where(c => c.Category == TitleCardCategory).ToArray();
        if (cardConds.Length > 0)
        {
            foreach (var cond in cardConds)
            {
                if (results.Count(r => (int)r.CardId == cond.Value) < requiredCount) return false;
            }
            return true;
        }

        return false;
    }


    private static void Append(StringBuilder text, string attribute, float value)
    {
        if (text.Length > 0) text.Append(' ');
        text.Append(attribute).Append('|')
            .Append(Math.Round((double)value, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture));
    }
}


