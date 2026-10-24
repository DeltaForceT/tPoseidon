namespace tPoseidon.Core.LibConfig;

public interface ICharacterInfoLookup
{
        CharacterInfoEntry? GetById(int id);

        int Count { get; }
}
