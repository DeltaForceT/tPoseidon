namespace tPoseidon.Core.Data;

public sealed class SessionToken
{
    public required uint Value { get; init; }
    public required uint AccountId { get; init; }
    public required DateTimeOffset IssuedAt { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;
}
