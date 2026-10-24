namespace tPoseidon.Core.Data;

public sealed record SafetyPassword
{
    public required uint AccountId { get; init; }

    public required string PasswordHash { get; init; }

    public byte LockFlag { get; init; }

    public byte LockLimitBitmask { get; init; }

    public string Email { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}
