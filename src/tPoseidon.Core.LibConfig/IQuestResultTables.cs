namespace tPoseidon.Core.LibConfig;

public interface IQuestResultTables
{
    IReadOnlyList<QuestResultEntry> RowsOf(string tableName);
}
