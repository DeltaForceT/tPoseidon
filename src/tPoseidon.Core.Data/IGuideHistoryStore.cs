namespace tPoseidon.Core.Data;

public interface IGuideHistoryStore
{
    GuideHistory? GetForCharacter(ulong characterId);

    void UpsertSystemGuide(ulong characterId, string systemGuide);

    void UpsertTutorialGuide(ulong characterId, ulong tutorialGuide);
}

public sealed record GuideHistory
{
    public required ulong CharacterId { get; init; }
    public string SystemGuide { get; init; } = string.Empty;
    public ulong TutorialGuide { get; init; }
}
