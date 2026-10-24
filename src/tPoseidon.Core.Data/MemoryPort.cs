namespace tPoseidon.Core.Data;

public sealed record MemoryPort
{
    public required ulong CharacterId { get; init; }
    public required byte PortType { get; init; }
    public required byte SlotNum { get; init; }
    public required short Zone { get; init; }
    public required int PositionX { get; init; }
    public required int PositionY { get; init; }
    public string Description { get; init; } = "";
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed record PkInfo
{
    public required ulong CharacterId { get; init; }
    public int KillTodayCount { get; init; }
    public int KillTotalCount { get; init; }
    public int DeathTodayCount { get; init; }
    public int DeathTotalCount { get; init; }
    public DateTimeOffset LatestUpdateAt { get; init; }
}

public sealed record UniqueCharDutyInfo
{
    public required ulong CharacterId { get; init; }
    public short BossMonMedalCount { get; init; }
    public short UniqueItemCount { get; init; }
    public DateTimeOffset? DutyEndAt { get; init; }
}

public sealed record CharProfile
{
    public required ulong CharacterId { get; init; }
    public int PublicFlagBitmask { get; init; }
    public string SelfIntro { get; init; } = "";
    public int TitleType { get; init; }
    public int TitleId { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
