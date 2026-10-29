namespace tPoseidon.Core.Data;

public interface ICharacterDirectory
{
    ulong? FindCharacterId(string characterName);
}
