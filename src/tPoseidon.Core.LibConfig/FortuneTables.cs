namespace tPoseidon.Core.LibConfig;

public readonly record struct FortuneCross(string Attribute, float Value);

public sealed record FortuneCardEntry(int Id, string Attribute, float Value, float Prob, float JokerProb, int Duration, IReadOnlyList<FortuneCross> Cross);

public readonly record struct FortuneTitleCondition(int Category, int Value);

public sealed record FortuneTitleRule(int Index, IReadOnlyList<FortuneTitleCondition> Conditions, int TitleId);

public sealed record FortuneTables(
    IReadOnlyDictionary<int, FortuneCardEntry> Cards,
    IReadOnlyDictionary<int, int> FeeByUse,
    IReadOnlyDictionary<string, float> Rules,
    IReadOnlyList<FortuneTitleRule> Titles)
{
    public const string CardTable = "FortuneCardParam";
    public const string FeeTable = "FortuneGalder";
    public const string RuleTable = "FortuneRuleParam";
    public const string TitleTable = "FortuneRuleTitle";
    public const string NeedleSelectCardSeed = "NeedleSelectCardSeed";
    public const string MyCardSelectSeed = "MyCardSelectSeed";
    public const int CrossColumns = 9;
    public const int TitleConditions = 3;

    public IReadOnlyList<int> CardOrder { get; init; } = Array.Empty<int>();

    public IEnumerable<FortuneCardEntry> CardsInRowOrder => CardOrder.Where(Cards.ContainsKey).Select(id => Cards[id]);

    public static FortuneTables Empty { get; } = new(
        new Dictionary<int, FortuneCardEntry>(),
        new Dictionary<int, int>(),
        new Dictionary<string, float>(StringComparer.Ordinal),
        Array.Empty<FortuneTitleRule>());
}
