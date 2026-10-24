namespace tPoseidon.Core.Data;

public enum CharCreationError : ushort
{
    Ok = 0,
    OverflowSlotCount = 1020,
    SlotOccupied = 1021,
    NameInvalid = 1019,
    NameTaken = 1023,
    CharAttrInsertFailed = 9001,
    CharStatusInsertFailed = 9002,
    CharStateInsertFailed = 9003,
    SideEffectInsertFailed = 105,
}

public sealed record CharCreationOutcome
{
    public required CharCreationError Error { get; init; }
    public CharacterSnapshot? Snapshot { get; init; }

    public bool IsSuccess => Error == CharCreationError.Ok;

    public static CharCreationOutcome Fail(CharCreationError error) =>
        new() { Error = error };

    public static CharCreationOutcome Success(CharacterSnapshot snapshot) =>
        new() { Error = CharCreationError.Ok, Snapshot = snapshot };
}

public sealed record CharDeletionOutcome
{
    public required CharDeletionError Error { get; init; }

    public bool IsSuccess => Error == CharDeletionError.Ok;

    public static CharDeletionOutcome Fail(CharDeletionError error) =>
        new() { Error = error };

    public static CharDeletionOutcome Success() =>
        new() { Error = CharDeletionError.Ok };
}

public enum CharDeletionError : ushort
{
    Ok = 0,
    NotFound = 8001,
    AuditInsertFailed = 8002,
    StateUpdateFailed = 8003,
    GuildMember = 8004,
    Married = 8005,
}
