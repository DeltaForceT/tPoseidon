namespace tPoseidon.Core.Data;

public sealed class Account
{
    public required uint AccountId { get; init; }

        public required string Username { get; init; }

        public required string PasswordPlaceholder { get; init; }

        public required IReadOnlyList<ulong> CharacterIds { get; init; }
}
