namespace tPoseidon.Core.LibConfig;

public sealed class InMemoryCharacterInfoLookup : ICharacterInfoLookup
{
    private readonly Dictionary<int, CharacterInfoEntry> _byId;

    public InMemoryCharacterInfoLookup(IEnumerable<CharacterInfoEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _byId = new Dictionary<int, CharacterInfoEntry>();
        foreach (var entry in entries)
        {
            _byId[entry.Id] = entry;
        }
    }

    public static InMemoryCharacterInfoLookup LoadFromXml(string xmlPath)
    {
        var reader = new LibConfigXmlReader(xmlPath);
        return new InMemoryCharacterInfoLookup(reader.LoadCharacterInfo());
    }

    public CharacterInfoEntry? GetById(int id)
        => _byId.TryGetValue(id, out var entry) ? entry : null;

    public int Count => _byId.Count;
}
