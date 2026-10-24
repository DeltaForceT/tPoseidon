namespace tPoseidon.Core.Data;

public sealed record TradeSlot(ulong ItemUid, int ItemIndex, ushort Count);

public sealed record TradeOffer(ulong CharacterId, uint Money, IReadOnlyList<TradeSlot> Slots);

public enum TradeChangeKind : byte
{
    StackChanged = 1,
    Removed = 3,
    Created = 4,
}

public sealed record TradeItemChange(TradeChangeKind Kind, byte ItemClass, int ItemIndex, ulong ItemUid, ushort Count, byte MixSlot);

public sealed record TradeSideResult(ulong CharacterId, uint Money, IReadOnlyList<TradeItemChange> Changes);

public sealed record TradeResult(int Error, TradeSideResult? First, TradeSideResult? Second)
{
    public bool Succeeded => Error == TradeRules.Ok;
}

public static class TradeRules
{
    public const int Ok = 0;
    public const int ItemMismatch = 1;
    public const int BagFull = 3;
    public const int MoneyOutOfRange = 4;
    public const int DuplicateItem = 0x0B;
    public const int StoreFailed = 0x0C;
    public const int SlotsPerSide = 6;
    public const uint MaxOfferedMoney = 1_000_000_000;
    public const uint OppositeMoneyCap = 2_100_000_000;
    public const uint MaxSettledMoney = 0x7FFFFFFE;
    public const byte CommonClass = 1;

    public static bool MoneySettles(uint own, uint given, uint received) =>
        (long)own - given + received is >= 0 and <= MaxSettledMoney;
}

public interface ITradeStore
{
    TradeResult Trade(TradeOffer first, TradeOffer second, int bagCapacity);
}
