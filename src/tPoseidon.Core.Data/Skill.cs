namespace tPoseidon.Core.Data;

public sealed record Skill
{
    public required ulong CharacterId { get; init; }

    public required int SkillId { get; init; }

    public required int SkillLevel { get; init; }

    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}
