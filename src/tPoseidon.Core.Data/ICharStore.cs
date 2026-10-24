namespace tPoseidon.Core.Data;

public interface ICharStore
{
    IReadOnlyList<Character> GetCharactersForAccount(uint accountId);
    Character? GetCharacterById(ulong characterId);
    void AddCharacter(Character character);
    bool DeleteCharacter(ulong characterId, uint ownerAccountId);
    bool SetCharacterMap(ulong characterId, ushort mapId);
    bool SetCharacterHome(ulong characterId, ushort homeMapId);
    bool SetCharacterPosition(ulong characterId, ushort mapId, ushort positionX, ushort positionY);
    bool SaveCharacterStats(ulong characterId, CharacterStatsSnapshot snapshot);
}
