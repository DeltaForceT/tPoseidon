namespace tPoseidon.Core.Data;

public sealed record MyCampSlot
{
    public required ulong SlotCode { get; init; }
    public required ulong CharacterId { get; init; }
    public string SlotName { get; init; } = "";
    public DateTimeOffset SlotUpToDate { get; init; }
}

public sealed record MyCampItem
{
    public required ulong ItemUid { get; init; }
    public required uint AccountId { get; init; }
    public required int ItemIndex { get; init; }
    public byte ItemWhere { get; init; }
    public DateTimeOffset ItemBuyDate { get; init; }
    public DateTimeOffset? ItemUseDate { get; init; }
    public short ItemUseTime { get; init; }
    public byte ItemFlag { get; init; }
    public long SellCode { get; init; }
    public byte SellType { get; init; }
}

public sealed record MyCampDeco
{
    public required ulong SlotCode { get; init; }
    public required ulong ItemUid { get; init; }
    public short DecoX { get; init; }
    public short DecoY { get; init; }
    public byte DecoZ { get; init; }
    public byte DecoDirection { get; init; }
    public ulong ParentsUid { get; init; }
}

public static class MyCampItemWhere
{
    public const byte New = 0;
    public const byte Inventory = 1;
    public const byte Deleted = 2;
}
