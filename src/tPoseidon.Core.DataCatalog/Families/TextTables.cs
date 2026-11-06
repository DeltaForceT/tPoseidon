using tPoseidon.Core.DataCatalog.Fast;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.DataCatalog.Families;

public sealed partial class TextTables
{
    private readonly FastLibConfigReader _reader;
    private readonly Lazy<IReadOnlyDictionary<string, string>> _wordStrings;
    private readonly Lazy<IReadOnlyDictionary<int, string>> _potionEffects;
    private readonly Lazy<IReadOnlyDictionary<string, string>> _stringTable;

    public TextTables(FastLibConfigReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _reader = reader;
        _wordStrings = new Lazy<IReadOnlyDictionary<string, string>>(reader.LoadWordStrings);
        _potionEffects = new Lazy<IReadOnlyDictionary<int, string>>(reader.LoadPotionEffects);
        _stringTable = new Lazy<IReadOnlyDictionary<string, string>>(reader.LoadStringTable);
    }

    public IReadOnlyDictionary<string, string> WordStrings => _wordStrings.Value;

    public IReadOnlyDictionary<int, string> PotionEffects => _potionEffects.Value;

    public IReadOnlyDictionary<string, string> NameValueTable(string tableName) => _reader.LoadNameValueTable(tableName);

    public IReadOnlyDictionary<string, string> NameStringTable(string tableName) => _reader.LoadNameStringTable(tableName);

    public IReadOnlyDictionary<string, string> StringTable => _stringTable.Value;
}
