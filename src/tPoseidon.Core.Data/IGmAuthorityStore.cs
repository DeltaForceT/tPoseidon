namespace tPoseidon.Core.Data;

public interface IGmAuthorityStore
{
    bool IsGmClientLoginPermitted(string characterName, string password, string clientIp);

    GmCharacterRegistration? RegisterGmCharacter(string characterName, string permittedIp);

    ulong? GmCharacterId(string characterName);
}

public sealed record GmCharacterRegistration(uint AccountId, string CharacterName, string PermittedIp);

public sealed record GmCharacterEntry(uint AccountId, string AccountName, string CharacterName, IReadOnlyList<string> PermittedIps, DateTimeOffset CreatedAt);
