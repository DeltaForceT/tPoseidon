namespace tPoseidon.Core.Data;

public interface ISafetyPasswordStore
{
    SafetyPassword? Find(uint accountId);

    void Upsert(SafetyPassword record);

    bool SetLockFlag(uint accountId, byte lockFlag);

    bool Delete(uint accountId);

    void AppendLog(SafetyPasswordLogEntry entry);

    IReadOnlyList<SafetyPasswordLogEntry> GetLog(uint accountId, int limit = 50);
}

public enum SafetyPasswordLogType : byte
{
    Created = 1,
    Updated = 2,
    LockFlagChanged = 3,
    Deleted = 4,
    GmOverride = 5,
}

public sealed record SafetyPasswordLogEntry
{
    public long LogSeq { get; init; }
    public required SafetyPasswordLogType LogType { get; init; }
    public required uint AccountId { get; init; }
    public string PasswordHash { get; init; } = "";
    public byte? LockFlag { get; init; }
    public byte? LockLimitBitmask { get; init; }
    public string Email { get; init; } = "";
    public DateTimeOffset CreatedAt { get; init; }
    public string? GmId { get; init; }
    public string? Reason { get; init; }
}
