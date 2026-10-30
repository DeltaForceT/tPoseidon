namespace tPoseidon.Core.Data;

public interface IMembershipStore
{
    bool SetMember(string accountName, bool member);

    bool SetMemberOfCharacter(string characterName, bool member);

    IReadOnlyList<string> Members();
}
