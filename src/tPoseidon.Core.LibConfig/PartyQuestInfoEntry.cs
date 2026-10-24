namespace tPoseidon.Core.LibConfig;

public sealed record PartyQuestTarget(int Monster, int Count);

public sealed record PartyQuestInfoEntry
{
    public const string TableName = "PartyMonQuestInfo";
    public const string ExpTableName = "PartyQuestExpTable";
    public const string ResultTablePrefix = "PMonQuestResult_";
    public const int TargetSlots = 5;
    public const int DefaultMaxCount = 25;

    public required int Id { get; init; }
    public required string Name { get; init; }
    public string Client { get; init; } = string.Empty;
    public string Pos { get; init; } = string.Empty;
    public string Comment { get; init; } = string.Empty;
    public int MinLevel { get; init; }
    public int MaxLevel { get; init; }
    public int TimeMinutes { get; init; }
    public IReadOnlyList<PartyQuestTarget> Targets { get; init; } = Array.Empty<PartyQuestTarget>();
    public string ResultTable { get; init; } = string.Empty;
    public int Exp { get; init; }
    public int Tmxp { get; init; }
    public int MaxCount { get; init; }
    public int ReturnMapId { get; init; }

    public int EffectiveMaxCount => MaxCount < 1 ? DefaultMaxCount : MaxCount;
}

public sealed class PartyQuestCatalog
{
    private const double MemberShareBase = 0.2;
    private const double MemberShareStep = 0.05;

    private readonly Dictionary<int, PartyQuestInfoEntry> _byId;
    private readonly IReadOnlyList<float> _ratios;

    public PartyQuestCatalog(IEnumerable<PartyQuestInfoEntry> quests, IReadOnlyList<float> expRatios)
    {
        ArgumentNullException.ThrowIfNull(quests);
        _byId = quests.ToDictionary(q => q.Id);
        _ratios = expRatios ?? throw new ArgumentNullException(nameof(expRatios));
    }

    public static PartyQuestCatalog Empty { get; } = new(Array.Empty<PartyQuestInfoEntry>(), Array.Empty<float>());

    public int Count => _byId.Count;

    public PartyQuestInfoEntry? GetById(int id) => _byId.GetValueOrDefault(id);

    public float ExpRatio(int completionCount)
    {
        if (completionCount < 1 || _ratios.Count == 0) return 0f;
        return _ratios[Math.Min(completionCount, _ratios.Count) - 1];
    }

    public int RewardFor(int baseAmount, int memberCount, int ownKills, int targetCount, int completionCount)
    {
        if (targetCount <= 0 || memberCount <= 0) return 0;
        var share = (memberCount - 1) * (double)baseAmount * ownKills / targetCount
            + (MemberShareStep * memberCount + MemberShareBase) * baseAmount;
        return (int)(share * ExpRatio(completionCount));
    }
}
