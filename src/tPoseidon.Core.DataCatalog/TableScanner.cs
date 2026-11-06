using tPoseidon.Core.DataCatalog.FastTable;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.DataCatalog;

internal static class TableScanner
{
    public static (int RowCount, IReadOnlyList<string> Columns) Scan(byte[] xml, LibConfigTableSpan table)
    {
        var rows = 0;
        IReadOnlyList<string> columns = Array.Empty<string>();
        FastTableReader.Read(xml, table, row =>
        {
            rows++;
            columns = row.SlotNames;
        });
        return (rows, columns.ToArray());
    }
}
