namespace tPoseidon.Core.Data;

public static class PostRules
{
    public const byte MailBox = 0;
    public const byte GiftBox = 1;

    public const byte NormalLetter = 0;
    public const byte GiftLetter = 1;
    public const byte PaymentRequest = 2;
    public const byte Payment = 3;
    public const byte PaidGift = 4;
    public const byte GmGift = 5;

    public const byte Unread = 0;
    public const byte Read = 1;
    public const byte Returned = 2;
    public const byte Deleted = 3;

    public const byte SenderSide = 0;
    public const byte ReceiverSide = 1;

    public const byte ReceivedList = 0;
    public const byte SentList = 1;
    public const byte GiftList = 2;

    public const int DailySendLimit = 25;
    public const int InboxLimit = 100;
    public const int MaxMoney = 500_000_000;
    public const int ListRows = 100;
    public const int TitleLength = 50;
    public const int ContentLength = 300;

    public const short KeepPermanent = 0;
    public const short KeepWeek = 1;
    public const short KeepDay = 2;

    public const int Ok = 0;
    public const int Failed = 50000;
}

public readonly record struct PostResult<T>(int Error, T? Value)
{
    public bool Succeeded => Error == PostRules.Ok;
}

public static class PostResult
{
    public static PostResult<T> Ok<T>(T value) => new(PostRules.Ok, value);

    public static PostResult<T> Fail<T>(int error) => new(error, default);
}

public sealed record PostListRow(
    long DateSeq,
    uint PostUid,
    byte PostType,
    byte PostSubType,
    byte PostState,
    byte UserType,
    string CharName,
    string Title,
    DateTime OccurDate,
    DateTime ReturnDate);

public sealed record PostCounts(int NewPosts, int ReturnedPosts);

public sealed record PostSyncResult(PostCounts Counts, IReadOnlyList<PostListRow> Mail, IReadOnlyList<PostListRow> Gifts);

public sealed record PostItemRow(
    long ItemSeq,
    ulong ItemUid,
    int ItemIndex,
    byte ItemClass,
    short ItemAttr,
    byte RefineType,
    byte RefineLevel,
    byte RefineState,
    byte ItemSlot);

public sealed record PostItemOption(
    long ItemSeq,
    ulong ItemUid,
    byte OptionType,
    ushort OptionIndex,
    short OptionValue,
    uint MixedItem);

public sealed record PostDetail(
    uint PostUid,
    string Content,
    int Money,
    IReadOnlyList<PostItemRow> Items,
    IReadOnlyList<PostItemOption> Options);

public sealed record PostSendItem(ulong ItemUid, ushort Count);

public sealed record PostSendRequest(
    byte PostSubType,
    string Title,
    string Content,
    int MoneyTax,
    int Money,
    uint FromAccountId,
    ulong FromCharUid,
    string FromCharName,
    int FromCharLevel,
    string ToCharName,
    IReadOnlyList<PostSendItem> Items);

public sealed record PostSent(long DateSeq, uint PostUid, DateTime OccurDate, DateTime ReturnDate, uint ToAccountId, ulong ToCharUid);

public sealed record PostReceiveRequest(
    uint PostUid,
    byte PostState,
    uint ToAccountId,
    ulong ToCharUid,
    int ToCharLevel,
    string ToCharName,
    string SysTitle,
    string SysContent);

public static class PostItemChange
{
    public const byte Created = 1;
    public const byte Stacked = 2;
}

public sealed record PostReceivedItem(
    byte Change,
    long ItemSeq,
    ulong ItemUid,
    int ItemIndex,
    byte ItemClass,
    ushort Count,
    ushort Total,
    byte RefineType,
    byte RefineLevel,
    byte RefineState,
    byte ItemSlot);

public sealed record PostReceived(
    byte PostSubType,
    long MoneyChange,
    IReadOnlyList<PostReceivedItem> Items,
    IReadOnlyList<PostItemOption> Options,
    uint? PaymentToAccountId,
    ulong? PaymentToCharUid);

public sealed record GiftItem(int ItemIndex, byte ItemClass, short ItemAttr, int AddMinute);

public sealed record GmGift(string Title, string Content, ulong ToCharUid, string GmName, IReadOnlyList<GiftItem> Items);

public sealed record GiftDetailItem(long ItemSeq, int ItemIndex, byte ItemClass, short ItemAttr, int AddMinute, byte ItemFlag);

public sealed record GiftDetail(uint PostUid, string Content, IReadOnlyList<GiftDetailItem> Items);

public sealed record GiftItemOption(long ItemSeq, ushort OptionIndex, int OptionValue, int MixedItem);

public sealed record GiftReceiveRequest(uint PostUid, uint ToAccountId, ulong ToCharUid, int ToCharLevel, IReadOnlyList<GiftItemOption> Options);

public sealed record GiftReceivedItem(long ItemSeq, ulong ItemUid, int ItemIndex, byte ItemClass, ushort Count, DateTime BreaksAt);

public static class GiftRules
{
    public const byte StatOptionType = 10;
    public const int TimedFlag = 1;
}

public interface IPostStore
{
    PostResult<PostCounts> Count(ulong charUid);

    PostResult<PostSyncResult> Sync(uint accountId, ulong charUid, long afterDateSeq);

    PostResult<IReadOnlyList<PostListRow>> List(byte listType, uint accountId, ulong charUid, long beforeDateSeq, int rows);

    PostResult<PostDetail> Detail(uint postUid, uint accountId, ulong charUid);

    PostResult<PostSent> Send(PostSendRequest request);

    PostResult<PostReceived> Receive(PostReceiveRequest request);

    PostResult<PostListRow?> Delete(uint postUid, uint accountId, ulong charUid, long lastDateSeq);

    PostResult<PostSent> SendGmGift(GmGift gift);

    PostResult<GiftDetail> GiftDetail(uint postUid, uint accountId, ulong charUid);

    PostResult<IReadOnlyList<GiftReceivedItem>> ReceiveGift(GiftReceiveRequest request);
}
