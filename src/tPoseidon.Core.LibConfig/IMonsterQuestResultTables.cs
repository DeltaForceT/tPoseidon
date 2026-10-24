namespace tPoseidon.Core.LibConfig;

public interface IMonsterQuestResultTables
{
    IReadOnlyList<MonsterQuestResultEntry?> RowsOf(string tableName);
}
