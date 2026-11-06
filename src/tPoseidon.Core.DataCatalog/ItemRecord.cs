using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.DataCatalog;

public sealed record ItemRecord(ItemTable Source, ItemParamEntry Entry)
{
    public int Id => Entry.Id;
}

public sealed record ItemClash(int Id, IReadOnlyList<ItemTable> Tables);
