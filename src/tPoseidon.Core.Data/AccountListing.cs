namespace tPoseidon.Core.Data;

public sealed record AccountListing(string Username, DateTimeOffset CreatedAt, bool IsMember, bool HasGmCharacter);
