namespace tPoseidon.Core.Data;

public sealed record PvpMatch
{
    public long MatchSeq { get; init; }
    public required DateTimeOffset MatchBegin { get; init; }
    public required DateTimeOffset MatchEnd { get; init; }
    public byte MatchType { get; init; }
    public short MatchMap { get; init; }
    public byte MatchTimeout { get; init; }
    public byte JoinType { get; init; }
    public required ulong CharacterId { get; init; }
    public required ulong OpponentCharacterId { get; init; }
    public int GetLadder { get; init; }
    public int GetExp { get; init; }
    public byte MatchResult { get; init; }
}

public sealed record PvpLadder
{
    public required ulong CharacterId { get; init; }
    public int Ladder { get; init; } = 1000;
    public short MatchCount { get; init; }
    public short WinCount { get; init; }
    public short LoseCount { get; init; }
    public short DrawCount { get; init; }
    public short DiscCount { get; init; }
}

public sealed record FriendGroup(byte GroupNum, string GroupName);

public sealed record FriendEntry(ulong FriendCharacterId, byte GroupNum, string Name, ushort CharacterType);

public sealed record FriendMessage(ulong SenderCharacterId, string SenderName, DateTimeOffset SentAt, string Content, byte Kind);

public enum FriendAddResult
{
    Added = 0,
    ReverseInsertFailed = 101,
    InsertFailed = 102,
    AlreadyFriends = 103,
}

public enum FriendRemoveResult
{
    Removed = 0,
    NotFriends = 101,
    ReverseMissing = 102,
}

public static class FriendRules
{
    public const int MostFriends = 60;
    public const int GroupCount = 5;
    public const int MostStoredMessages = 100;
    public const int OfflineMessageLimit = 5;
    public const int MessageContentLength = 50;
    public const int GroupNameLength = 20;
    public const ulong GmSenderCharacterId = 0;
    public const byte GmMessageKind = 2;
    public const byte GuildMessageKind = 3;
    public const string GmSenderName = "GM";

    public static readonly IReadOnlyList<string> DefaultGroupNames = new[]
    {
        "Best Friends", "Soulmates", "Companions", "Relatives", "Good Fellas",
    };

    public static string Truncate(string value, int byteLimit, System.Text.Encoding text)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(text);
        if (text.GetByteCount(value) <= byteLimit) return value;
        var length = value.Length;
        while (length > 0 && text.GetByteCount(value.AsSpan(0, length)) > byteLimit) length--;
        return value[..length];
    }

    public static ushort CharacterTypeOf(Character character)
    {
        ArgumentNullException.ThrowIfNull(character);
        return character.JobState switch
        {
            2 => character.Type2,
            3 => character.Type3,
            _ => (ushort)character.Species,
        };
    }
}

public sealed record AccountBlock
{
    public long BlockSeq { get; init; }
    public required uint TargetAccountId { get; init; }
    public required byte BlockType { get; init; }
    public required byte BlockReason { get; init; }
    public uint? GmAccountId { get; init; }
    public required DateTimeOffset BlockFrom { get; init; }
    public required DateTimeOffset BlockTo { get; init; }
    public string Description { get; init; } = "";
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed record AccountLogoutEntry
{
    public long LogSeq { get; init; }
    public required uint AccountId { get; init; }
    public required DateTimeOffset LoginAt { get; init; }
    public required DateTimeOffset LogoutAt { get; init; }
    public byte LogoutWorld { get; init; }
    public int? PlayTime { get; init; }
}

public sealed record FortuneInfo
{
    public required ulong CharacterId { get; init; }
    public int ViewCount { get; init; }
    public int TitleId { get; init; }
    public string ItemAttrString { get; init; } = "";
    public DateTimeOffset UpdatedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
}

public sealed record FortuneInfoLogEntry
{
    public long LogSeq { get; init; }
    public required ulong CharacterId { get; init; }
    public int ViewCount { get; init; }
    public int TitleId { get; init; }
    public string ItemAttrString { get; init; } = "";
    public DateTimeOffset LoggedAt { get; init; }
}

public sealed record BroadcastMessage
{
    public long BroadcastCode { get; init; }
    public required ulong CharacterId { get; init; }
    public byte IslandCode { get; init; }
    public string Message { get; init; } = "";
    public byte MessageFlag { get; init; }
    public byte BroadcastType { get; init; }
    public byte ColorType { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed record BroadcastLogEntry
{
    public long LogSeq { get; init; }
    public required ulong CharacterId { get; init; }
    public byte IslandCode { get; init; }
    public string Message { get; init; } = "";
    public byte BroadcastType { get; init; }
    public byte ColorType { get; init; }
    public DateTimeOffset LoggedAt { get; init; }
}
