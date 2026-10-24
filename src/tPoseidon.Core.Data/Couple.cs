namespace tPoseidon.Core.Data;

public sealed record Couple
{
    public required ulong CoupleCode { get; init; }
    public bool CoupleAuthority { get; init; }
    public short TitleIndex { get; init; }
    public int RingItemIndex { get; init; }
    public int DressItemIndex { get; init; }
    public byte AdviceIndex1 { get; init; }
    public byte AdviceIndex2 { get; init; }
    public byte CoupleState { get; init; }
    public byte RelationPoint { get; init; }
    public byte LovePoint { get; init; }
    public byte FriendPoint { get; init; }
    public DateTimeOffset RecentUpdateAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed record CoupleChar
{
    public required ulong CharacterId { get; init; }
    public required ulong CoupleCode { get; init; }
    public short CoupleCount { get; init; }
    public short CoupleDeleteCount { get; init; }
    public short MarriageCount { get; init; }
    public short DivorceCount { get; init; }
    public short WeddingInCount { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed record CoupleJoin
{
    public required ulong CoupleCode { get; init; }
    public required ulong CharacterId { get; init; }
    public bool CharSex { get; init; }
    public long EventBitmask { get; init; }
    public byte DivorceState { get; init; }
}
