namespace tPoseidon.Core.LibConfig;

public sealed class InMemoryNpcActionScriptLookup : INpcActionScriptLookup
{
    private readonly Dictionary<int, NpcActionScriptEntry> _byType;

    public InMemoryNpcActionScriptLookup(IEnumerable<NpcActionScriptEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _byType = new Dictionary<int, NpcActionScriptEntry>();
        foreach (var entry in entries)
        {
            _byType[entry.Type] = entry;
        }
    }

    public static InMemoryNpcActionScriptLookup LoadFromXml(string xmlPath)
    {
        var reader = new LibConfigXmlReader(xmlPath);
        return new InMemoryNpcActionScriptLookup(reader.LoadNpcActionScript());
    }

    public NpcActionScriptEntry? GetByType(int type)
        => _byType.TryGetValue(type, out var entry) ? entry : null;

    public int Count => _byType.Count;
}
