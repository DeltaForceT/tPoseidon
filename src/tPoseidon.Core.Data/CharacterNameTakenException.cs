namespace tPoseidon.Core.Data;

public sealed class CharacterNameTakenException : InvalidOperationException
{
    public CharacterNameTakenException(string name, Exception? inner = null)
        : base($"The character name '{name}' is already used by another character.", inner)
    {
        Name = name;
    }

    public string Name { get; }
}
