using System.Collections.Concurrent;

namespace tPoseidon.Core.Data;

internal static class InMemoryCharacterEdits
{
    public static uint Apply(
        ConcurrentDictionary<ulong, Character> characters, ulong characterId, CharacterEdit edit, IReadOnlyCollection<string> forbiddenNames)
    {
        ArgumentNullException.ThrowIfNull(characters);
        ArgumentNullException.ThrowIfNull(edit);
        ArgumentNullException.ThrowIfNull(forbiddenNames);
        while (characters.TryGetValue(characterId, out var existing))
        {
            if (edit.Kind == CharacterEditKind.Rename)
            {
                if (characters.Values.Any(other => string.Equals(other.Name, edit.NewName, StringComparison.OrdinalIgnoreCase)))
                {
                    return CharacterEditCodes.NameTaken;
                }
                if (!CharacterNameValidity.IsValid(edit.NewName, forbiddenNames)) return CharacterEditCodes.InvalidName;
            }
            if (characters.TryUpdate(characterId, InMemoryCharStore.CloneWith(existing, edit: edit), existing)) return CharacterEditCodes.Done;
        }
        return CharacterEditCodes.Failed;
    }
}
