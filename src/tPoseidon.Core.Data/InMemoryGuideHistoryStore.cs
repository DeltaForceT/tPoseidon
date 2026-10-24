using System.Collections.Concurrent;

namespace tPoseidon.Core.Data;

public sealed class InMemoryGuideHistoryStore : IGuideHistoryStore
{
    private readonly ConcurrentDictionary<ulong, GuideHistory> _rows = new();

    public GuideHistory? GetForCharacter(ulong characterId) =>
        _rows.TryGetValue(characterId, out var row) ? row : null;

    public void UpsertSystemGuide(ulong characterId, string systemGuide)
    {
        ArgumentNullException.ThrowIfNull(systemGuide);
        if (systemGuide.Length > 1000)
        {
            throw new ArgumentException(
                $"system_guide column is varchar(1000); got {systemGuide.Length} chars.",
                nameof(systemGuide));
        }
        _rows.AddOrUpdate(
            characterId,
            _ => new GuideHistory { CharacterId = characterId, SystemGuide = systemGuide, TutorialGuide = 0 },
            (_, existing) => existing with { SystemGuide = systemGuide });
    }

    public void UpsertTutorialGuide(ulong characterId, ulong tutorialGuide)
    {
        _rows.AddOrUpdate(
            characterId,
            _ => new GuideHistory { CharacterId = characterId, SystemGuide = string.Empty, TutorialGuide = tutorialGuide },
            (_, existing) => existing with { TutorialGuide = tutorialGuide });
    }
}
