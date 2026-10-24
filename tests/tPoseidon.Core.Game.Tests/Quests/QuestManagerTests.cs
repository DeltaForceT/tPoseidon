using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Quests;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.GameTests.Quests;

public class QuestManagerTests
{
    private const ulong CharId = 0x10000001;

    private static readonly NpcPoint StubPoint = NpcPoint.Xy(0, 0);

    private static NpcEntity MakeBunnyMaid() => new()
    {
        EntityId = 0x014C,
        ChrType = 1011,
        MapId = 148,
        PosId = 3,
        Spawn = StubPoint,
        Source = new NpcTalkEntry
        {
            Type = 1011,
            PosId = 3,
            MapName = "Coral_Town",
            MsgTable = "NpcMsg_EP0_Npc12_1",
            QuestId0 = 1233,
            QuestId1 = 1234,
            QuestId2 = 1235,
        },
    };

    private static QuestManager MakeManager(
        out InMemoryQuestStore questStore,
        out InMemoryQuestHistoryStore historyStore,
        out InMemoryItemStore itemStore,
        IEnumerable<QuestInfoEntry>? quests = null)
    {
        questStore = new InMemoryQuestStore();
        historyStore = new InMemoryQuestHistoryStore();
        itemStore = new InMemoryItemStore();
        var catalog = new InMemoryQuestInfoLookup(quests ?? new[]
        {
            new QuestInfoEntry { Id = 1233, Name = "Trickster Registration", MaxCount = 1, SrcItem0 = 11805 },
            new QuestInfoEntry { Id = 1234, Name = "Prove Your Spirit!", MaxCount = 1 },
            new QuestInfoEntry { Id = 1235, Name = "Next quest", MaxCount = 1 },
        });
        return new QuestManager(questStore, historyStore, itemStore, catalog);
    }

    [Fact]
    public void PickQuestForBindingCheck_FreshChar_PicksFirstNonZero()
    {
        var mgr = MakeManager(out _, out _, out _);
        var qid = mgr.PickQuestForBindingCheck(MakeBunnyMaid(), CharId);
        Assert.Equal(1233, qid);
    }

    [Fact]
    public void PickQuestForBindingCheck_AfterCompleting1233_Picks1234()
    {
        var mgr = MakeManager(out _, out var history, out _);
        history.Insert(new QuestHistory
        {
            CharacterId = CharId,
            QuestType = 1,
            QuestId = 1233,
            CompletionCount = 1,
        });
        var qid = mgr.PickQuestForBindingCheck(MakeBunnyMaid(), CharId);
        Assert.Equal(1234, qid);
    }

    [Fact]
    public void PickAcceptableQuest_AfterCompleting1233_Picks1234()
    {
        var mgr = MakeManager(out _, out var history, out _);
        history.Insert(new QuestHistory
        {
            CharacterId = CharId,
            QuestType = 1,
            QuestId = 1233,
            CompletionCount = 1,
        });
        var qid = mgr.PickAcceptableQuest(MakeBunnyMaid(), CharId);
        Assert.Equal(1234, qid);
    }

    [Fact]
    public void PickAcceptableQuest_AllCompleted_ReturnsNull()
    {
        var mgr = MakeManager(out _, out var history, out _);
        foreach (var q in new[] { 1233, 1234, 1235 })
        {
            history.Insert(new QuestHistory
            {
                CharacterId = CharId,
                QuestType = 1,
                QuestId = q,
                CompletionCount = 1,
            });
        }
        var qid = mgr.PickAcceptableQuest(MakeBunnyMaid(), CharId);
        Assert.Null(qid);
    }

    [Fact]
    public void PickActiveBoundQuestToComplete_PicksActiveQuest()
    {
        var mgr = MakeManager(out var quests, out _, out _);
        quests.AddActive(new Quest
        {
            CharacterId = CharId,
            QuestType = 1,
            QuestId = 1234,
            CompletionCount = 1,
        });
        var qid = mgr.PickActiveBoundQuestToComplete(MakeBunnyMaid(), CharId);
        Assert.Equal(1234, qid);
    }

    [Fact]
    public void IsRepeatable_OneShotAlreadyDone_BlocksReaccept()
    {
        var mgr = MakeManager(out _, out _, out _);
        Assert.False(mgr.IsRepeatable(1233, historyCount: 1));
    }

    [Fact]
    public void GetStarterItemIndex_Heidi1233_ReturnsRegistrationForm()
    {
        var mgr = MakeManager(out _, out _, out _);
        Assert.Equal(11805, mgr.GetStarterItemIndex(1233));
    }
}
