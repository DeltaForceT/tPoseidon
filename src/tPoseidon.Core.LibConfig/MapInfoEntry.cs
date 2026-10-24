namespace tPoseidon.Core.LibConfig;

public enum MapGroup
{
    World = 0,
    Tutorial = 0xC,
    GuildGame = 0xE,
    ParallelMonsterQuest = 0x10,
    ParallelParty = 0x14,
    WeddingHall = 0x16,
    Parallel = 0xFF,
}

public sealed record MapInfoEntry
{
    public required ushort Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Md3FileName { get; init; } = string.Empty;
    public string NpcTable { get; init; } = string.Empty;
    public string MonsterTable { get; init; } = string.Empty;
    public string BossMonsterTable { get; init; } = string.Empty;
    public string ItemTable { get; init; } = string.Empty;
    public string ShopTable { get; init; } = string.Empty;
    public string SkillShopTable { get; init; } = string.Empty;
    public int MonsterAvrNum { get; init; }
    public int MaxDepth { get; init; }
    public int BossMonsterCnt { get; init; }
    public int BossMonCheckTime { get; init; }
    public MapGroup Group { get; init; } = MapGroup.World;
    public bool IsDungeon { get; init; }
    public bool IsMyCamp { get; init; }
    public bool IsEvent { get; init; }
}
