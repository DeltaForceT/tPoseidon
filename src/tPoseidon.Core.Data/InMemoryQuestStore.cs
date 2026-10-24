using System.Collections.Concurrent;

namespace tPoseidon.Core.Data;

public sealed class InMemoryQuestStore : IQuestStore
{
    private readonly record struct QuestKey(ulong CharacterId, byte QuestType, int QuestId, int MonsterId);

    private readonly ConcurrentDictionary<QuestKey, Quest> _active = new();

    public void AddActive(Quest quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        var key = new QuestKey(quest.CharacterId, quest.QuestType, quest.QuestId, quest.MonsterId);
        if (!_active.TryAdd(key, quest))
        {
            throw new InvalidOperationException(
                $"Quest already active for character {quest.CharacterId}: " +
                $"type={quest.QuestType}, id={quest.QuestId}, monster={quest.MonsterId}");
        }
    }

    public IReadOnlyList<Quest> GetActiveForCharacter(ulong characterId)
    {
        return _active.Values
            .Where(q => q.CharacterId == characterId)
            .ToList();
    }

    public Quest? FindActive(ulong characterId, byte questType, int questId, int monsterId)
    {
        var key = new QuestKey(characterId, questType, questId, monsterId);
        return _active.TryGetValue(key, out var q) ? q : null;
    }

    public int CountActiveOfType(ulong characterId, byte questType)
    {
        return _active.Values.Count(q => q.CharacterId == characterId && q.QuestType == questType);
    }

    public bool UpdateMonsterProgress(ulong characterId, int questId, int monsterId, short killCount, int recordTime)
    {


        foreach (var qt in new byte[] { 2, 5 })
        {
            var key = new QuestKey(characterId, qt, questId, monsterId);
            if (_active.TryGetValue(key, out var existing))
            {
                var updated = existing with { KillCount = killCount, RecordTime = recordTime };
                if (_active.TryUpdate(key, updated, existing)) return true;



                return false;
            }
        }
        return false;
    }

    public bool RemoveActive(ulong characterId, byte questType, int questId, int monsterId)
    {
        var key = new QuestKey(characterId, questType, questId, monsterId);
        return _active.TryRemove(key, out _);
    }
}
