using System.Net;

namespace tPoseidon.Core.Data;

public sealed record DeletedChar
{
    public required ulong CharacterId { get; init; }
    public required uint AccountId { get; init; }
    public required string CharacterName { get; init; }
    public IPAddress? UserIp { get; init; }
    public DateTimeOffset DeletedAt { get; init; }
}

public sealed record CharRestoreLogEntry
{
    public long Seq { get; init; }
    public required uint AccountId { get; init; }
    public required ulong CharacterId { get; init; }
    public required string OldName { get; init; }
    public required string NewName { get; init; }
    public required DateTimeOffset DeletedAt { get; init; }
    public DateTimeOffset RestoredAt { get; init; }
    public string? GmId { get; init; }
    public string? Reason { get; init; }
}
