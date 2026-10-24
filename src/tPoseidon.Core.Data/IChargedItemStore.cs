namespace tPoseidon.Core.Data;

public sealed record ChargedItem(
    ulong ItemUid,
    byte ItemClass,
    int ItemIndex,
    short ItemAttr,
    short UseTimeHours,
    byte ItemFlag);

public sealed record ChargedItemGrant(
    uint AccountId,
    byte ItemClass,
    int ItemIndex,
    short ItemAttr,
    short UseTimeHours,
    byte ItemFlag,
    int SellCode,
    byte SellType);

public sealed record ChargedItemMove(uint AccountId, ulong ItemUid, ulong CharacterId, bool JoinStack);

public sealed record ChargedItemMoved(
    ulong ItemUid,
    bool JoinedStack,
    byte ItemClass,
    int ItemIndex,
    ushort Count,
    ushort Total,
    DateTimeOffset BreakedAt);

public sealed record ChargedItemMoveResult(ChargedItemMoved? Moved, int Error)
{
    public bool Succeeded => Moved is not null;
}

public static class ChargedItemRules
{
    public const byte CommonClass = 1;
    public const byte RareClass = 2;
    public const byte TimedFlag = 1;
    public const int NotWaiting = 101;
    public const int StampFailed = 102;
    public const int MoveFailed = 103;
    public const byte PurchaseSellType = 0;
    public const byte OtherSellType = 1;
    public const int GachaSellCodeBase = 300_000_000;
}

public interface IChargedItemStore
{
    IReadOnlyList<ChargedItem> ListWaiting(uint accountId);

    ulong Grant(ChargedItemGrant grant);

    ChargedItemMoveResult MoveToInventory(ChargedItemMove move);
}
