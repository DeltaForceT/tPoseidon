namespace tPoseidon.Core.LibConfig;

public sealed record NpcTalkEntry
{
        public required int Type { get; init; }

        public required int PosId { get; init; }

        public required string MapName { get; init; }

        public string MsgTable { get; init; } = string.Empty;

    public int TeleportId { get; init; }

    public int BossPartyZone { get; init; }

    public int NpcMoveId { get; init; }

    public IReadOnlyList<int> ItemStatIds { get; init; } = Array.Empty<int>();

    public int JobTypeFlag1 { get; init; }

    public int JobTypeFlag2 { get; init; }

    public IReadOnlyList<int> SkillCheckIds { get; init; } = Array.Empty<int>();

    public string CompounderTable { get; init; } = string.Empty;

    public string ExchangeShopTable { get; init; } = string.Empty;

    public string VipShopTable { get; init; } = string.Empty;

        public int QuestId0 { get; init; }
    public int QuestId1 { get; init; }
    public int QuestId2 { get; init; }
    public int QuestId3 { get; init; }
    public int QuestId4 { get; init; }

        public int HeadMarkType { get; init; }

    public int PreCheckType { get; init; }
    public int PreCheckArg { get; init; }
    public int PreCheckYes { get; init; }
    public int PreCheckNo { get; init; }

    public int CheckQuestId0 { get; init; }
    public int CheckQuestId1 { get; init; }
    public int CheckQuestId2 { get; init; }
    public int CheckQuestNum0 { get; init; }
    public int CheckQuestNum1 { get; init; }
    public int CheckQuestNum2 { get; init; }
    public int CheckMonQuestId { get; init; }
    public int CheckMonQuestNum { get; init; }

    public int CheckItemId0 { get; init; }
    public int CheckItemCnt0 { get; init; }
    public int CheckItemId1 { get; init; }
    public int CheckItemCnt1 { get; init; }

    public int ActiveQuestId0 { get; init; }
    public int ActiveQuestId1 { get; init; }
    public int ActiveQuestId2 { get; init; }

    public int GiveItemId { get; init; }
    public int TakeItemId { get; init; }
    public int GiveGelder { get; init; }
    public int TakeGelder { get; init; }
}
