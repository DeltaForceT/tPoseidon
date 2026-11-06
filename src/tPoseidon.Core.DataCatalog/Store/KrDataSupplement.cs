using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.DataCatalog.Store;

public static class KrDataSupplement
{
    private const string NamePrefix = "shadow-sanctuary-rows-referenced-";

    public static CatalogSupplement Create() => new(
        $"{NamePrefix}{ReferencedTables.Fingerprint}-{ReferencedRows.Fingerprint}-{RosemaryExchangeRows.Fingerprint}",
        xml => RosemaryExchangeRows.Merge(ShadowSanctuaryRows.Merge(ReferencedRows.Merge(ReferencedTables.Merge(xml)))));
}
