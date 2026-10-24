namespace tPoseidon.Core.LibConfig;

public interface INpcMsgLookup
{
        NpcMsgEntry? GetRow(string tableName, int rowNo);

        int GetRowCount(string tableName);

        int Count { get; }
}
