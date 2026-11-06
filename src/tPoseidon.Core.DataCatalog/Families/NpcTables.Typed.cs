using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.DataCatalog.Families;

public sealed partial class NpcTables
{
    private CatalogTable<int, CharacterInfoEntry>? _characters;

    public CatalogTable<int, CharacterInfoEntry> Characters => _characters ??= new CatalogTable<int, CharacterInfoEntry>("CharacterInfo", CharacterInfo, row => row.Id);
}
