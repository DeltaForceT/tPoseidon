using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Quests;

public sealed class QuestManager
{
    private readonly IQuestStore _questStore;
    private readonly IQuestHistoryStore _historyStore;
    private readonly IItemStore _itemStore;
    private readonly IQuestInfoLookup _questCatalog;
    private readonly IQuestResultLookup? _questResults;
    private readonly IQuestTypeLookup _questTypes;

    public QuestManager(
        IQuestStore questStore,
        IQuestHistoryStore historyStore,
        IItemStore itemStore,
        IQuestInfoLookup questCatalog,
        IQuestResultLookup? questResults = null,
        IQuestTypeLookup? questTypes = null)
    {
        ArgumentNullException.ThrowIfNull(questStore);
        ArgumentNullException.ThrowIfNull(historyStore);
        ArgumentNullException.ThrowIfNull(itemStore);
        ArgumentNullException.ThrowIfNull(questCatalog);
        _questStore = questStore;
        _historyStore = historyStore;
        _itemStore = itemStore;
        _questCatalog = questCatalog;
        _questResults = questResults;
        _questTypes = questTypes ?? new ConstantQuestTypeLookup();
    }

    private sealed class ConstantQuestTypeLookup : IQuestTypeLookup
    {
        public byte GetQuestType(int questId) => 1;
    }

        public int? PickQuestForBindingCheck(NpcEntity npc, ulong characterId)
    {
        ArgumentNullException.ThrowIfNull(npc);
        var questIds = ExtractQuestIds(npc);
        if (questIds.Length == 0) return null;

        foreach (var qid in questIds)
        {
            if (qid == 0) continue;
            var active = _questStore.FindActive(characterId, _questTypes.GetQuestType(qid), qid, monsterId: 0);
            if (active is not null) return qid;

            var history = _historyStore.Find(characterId, _questTypes.GetQuestType(qid), qid, monsterId: 0);
            if (history is not null && !IsRepeatable(qid, history.CompletionCount))
            {
                continue;
            }
            return qid;
        }

        return questIds.LastOrDefault(q => q != 0);
    }

        public int? PickAcceptableQuest(NpcEntity npc, ulong characterId)
    {
        ArgumentNullException.ThrowIfNull(npc);
        var questIds = ExtractQuestIds(npc);
        if (questIds.Length == 0) return null;

        foreach (var qid in questIds)
        {
            if (qid == 0) continue;
            var alreadyActive = _questStore.FindActive(characterId, _questTypes.GetQuestType(qid), qid, monsterId: 0) is not null;
            if (alreadyActive) continue;

            var history = _historyStore.Find(characterId, _questTypes.GetQuestType(qid), qid, monsterId: 0);
            if (history is not null && !IsRepeatable(qid, history.CompletionCount))
            {
                continue;
            }
            return qid;
        }
        return null;
    }

        public int? PickActiveBoundQuestToComplete(NpcEntity npc, ulong characterId)
    {
        ArgumentNullException.ThrowIfNull(npc);
        var questIds = ExtractQuestIds(npc);
        if (questIds.Length == 0) return null;

        foreach (var qid in questIds)
        {
            if (qid == 0) continue;
            var active = _questStore.FindActive(characterId, _questTypes.GetQuestType(qid), qid, monsterId: 0);
            if (active is not null) return qid;
        }
        return null;
    }

        public (int QuestId, int ItemIndex)? PickActiveQuestStarterItem(NpcEntity npc, ulong characterId)
    {
        ArgumentNullException.ThrowIfNull(npc);
        var questIds = ExtractQuestIds(npc);
        if (questIds.Length == 0) return null;

        foreach (var qid in questIds)
        {
            if (qid == 0) continue;
            var active = _questStore.FindActive(characterId, _questTypes.GetQuestType(qid), qid, monsterId: 0);
            if (active is null) continue;
            var starter = GetStarterItemIndex(qid);
            if (starter == 0) continue;
            return (qid, starter);
        }
        return null;
    }

        private static int[] ExtractQuestIds(NpcEntity npc)
    {
        if (npc.Source is not NpcTalkEntry talk) return Array.Empty<int>();
        var slots = new[] { talk.QuestId0, talk.QuestId1, talk.QuestId2, talk.QuestId3, talk.QuestId4 };
        return slots.Where(q => q != 0).ToArray();
    }

        public int ResolveBranchRow(IReadOnlyList<ushort> args, ulong characterId, int boundQuestId, int boundItemIndex)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Count == 0) return 0;

        var noItemRow = args[0];
        var hasItemRow = args.Count >= 2 ? args[1] : noItemRow;
        var inProgressRow = args.Count >= 3 ? args[2] : noItemRow;
        var completeRow = args.Count >= 4 ? args[3] : hasItemRow;

        const byte invSlotKind = 1;
        var history = _historyStore.Find(characterId, _questTypes.GetQuestType(boundQuestId), boundQuestId, monsterId: 0);
        var active = _questStore.FindActive(characterId, _questTypes.GetQuestType(boundQuestId), boundQuestId, monsterId: 0);
        var hasItem = boundItemIndex != 0 && _itemStore
            .GetForCharacterBySlotKind(characterId, invSlotKind)
            .Any(i => i.ItemIndex == boundItemIndex);
        var isComplete = active is null && history is { CompletionCount: > 0 };

        if (isComplete)              return completeRow;
        if (hasItem)                 return hasItemRow;
        if (active is not null)      return inProgressRow;
        return noItemRow;
    }

    public bool IsQuestActive(ulong characterId, int questId) =>
        _questStore.FindActive(characterId, _questTypes.GetQuestType(questId), questId, monsterId: 0) is not null;

        public Quest AddActive(ulong characterId, int questId)
    {
        var prior = _historyStore.Find(characterId, _questTypes.GetQuestType(questId), questId, monsterId: 0);
        var nextCount = (byte)Math.Min((prior?.CompletionCount ?? 0) + 1, 99);
        var quest = new Quest
        {
            CharacterId = characterId,
            QuestType = _questTypes.GetQuestType(questId),
            QuestId = questId,
            CompletionCount = nextCount,
            MonsterId = 0,
            KillCount = 0,
            RecordTime = 0,
        };
        _questStore.AddActive(quest);
        return quest;
    }

    public bool IsQuestComplete(ulong characterId, int questId)
    {
        var history = _historyStore.Find(characterId, _questTypes.GetQuestType(questId), questId, monsterId: 0);
        if (history is null || history.CompletionCount == 0) return false;
        return _questStore.FindActive(characterId, _questTypes.GetQuestType(questId), questId, monsterId: 0) is null;
    }

        public bool IsRepeatable(int questId, byte historyCount)
    {
        if (historyCount >= 99) return false;
        var def = _questCatalog.GetById(questId);
        var maxCount = def?.MaxCount ?? 1;
        if (maxCount <= 0) maxCount = 1;
        return historyCount < maxCount;
    }

        public string? GetPostAcceptMessage(int questId, int grade = 1, int msgIndex = 0)
    {
        if (_questResults is null) return null;
        var entry = _questResults.GetByQuestId(questId, grade);
        return entry?.MessageForGrade(msgIndex);
    }

        public int GetStarterItemIndex(int questId)
    {
        var def = _questCatalog.GetById(questId);
        return def?.SrcItem0 ?? 0;
    }

        public LibConfig.QuestInfoEntry? GetQuestInfo(int questId) => _questCatalog.GetById(questId);

        public QuestCompletionEvent? CompleteQuest(
        ulong characterId,
        int questId,
        ItemManager ItemManager)
    {
        ArgumentNullException.ThrowIfNull(ItemManager);

        var active = _questStore.FindActive(characterId, _questTypes.GetQuestType(questId), questId, monsterId: 0);
        if (active is null) return null;

        Item? revokedItem = null;
        var starterItemIndex = GetStarterItemIndex(questId);
        if (starterItemIndex != 0)
        {
            revokedItem = ItemManager.RevokeQuestItem(characterId, starterItemIndex, questId);
        }

        var grantedRewards = new List<Item>();
        if (_questResults is not null)
        {
            var rewards = _questResults.GetByQuestId(questId, count: 1);
            if (rewards is not null)
            {
                foreach (var (itemIndex, count) in rewards.NonEmptyItems())
                {
                    var stack = (ushort)Math.Clamp(count, 1, ushort.MaxValue);
                    var minted = ItemManager.GrantRewardItem(
                        characterId, itemIndex, stack);
                    grantedRewards.Add(minted);
                }
            }
        }

        
        var nowUtc = DateTimeOffset.UtcNow;
        var historyRow = new QuestHistory
        {
            CharacterId = characterId,
            QuestType = _questTypes.GetQuestType(questId),
            QuestId = questId,
            CompletionCount = active.CompletionCount,
            MonsterId = 0,
            KillCount = active.KillCount,
            RecordTime = active.RecordTime,
            CompletedAt = nowUtc,
        };
        if (active.CompletionCount == 1)
        {
            try { _historyStore.Insert(historyRow); }
            catch (InvalidOperationException) { _historyStore.Update(historyRow); }
        }
        else
        {
            if (!_historyStore.Update(historyRow))
            {
                _historyStore.Insert(historyRow);
            }
        }
        _historyStore.AppendLog(historyRow);

        _questStore.RemoveActive(characterId, _questTypes.GetQuestType(questId), questId, monsterId: 0);

        return new QuestCompletionEvent
        {
            CharacterId = characterId,
            QuestId = questId,
            CompletionCount = active.CompletionCount,
            RevokedItem = revokedItem,
            GrantedRewards = grantedRewards,
        };
    }
}

public sealed record QuestCompletionEvent
{
    public required ulong CharacterId { get; init; }
    public required int QuestId { get; init; }
    public required byte CompletionCount { get; init; }
        public Item? RevokedItem { get; init; }
        public IReadOnlyList<Item> GrantedRewards { get; init; } = Array.Empty<Item>();
}
