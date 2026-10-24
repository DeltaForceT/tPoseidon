namespace tPoseidon.Core.Data;

public static class GiftAcceptErrors
{
    public const int None = 0;
    public const int GrantFailed = 101;
    public const int NotFound = 102;
    public const int AlreadyTaken = 103;
}

public static class GiftItemRules
{
    public const int MessageLength = 100;
    public const byte ChargedBoxCategory = 0;
    public const int ListRowLimit = 100;
}

public sealed record GiftReceivedRow(uint GoodsCode, DateTimeOffset TakeDate, string SenderName, string GiftMessage, short SenderType);

public sealed record GiftPendingRow(uint GiftCode, uint GoodsCode, string SenderName, DateTimeOffset GiftDate, string GiftMessage, short SenderType);

public sealed record GiftSentRow(uint GoodsCode, DateTimeOffset GiftDate, string ReceiverName, string ReceiveMessage, bool Taken, short ReceiverType);

public sealed record GiftAcceptResult(int Error)
{
    public bool Succeeded => Error == GiftAcceptErrors.None;

    public uint Success => Succeeded ? 1u : 0u;
}

public interface IGiftStore
{
    GiftAcceptResult Accept(uint giftCode, string receiveMessage, DateTimeOffset at);

    IReadOnlyList<GiftReceivedRow> Received(uint accountId, int limit);

    IReadOnlyList<GiftPendingRow> Pending(uint accountId, int limit);

    IReadOnlyList<GiftSentRow> Sent(uint accountId, int limit);

    int RemainCount(uint accountId);
}
