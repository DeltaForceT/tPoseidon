using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Quests;

public static class NpcPreCheck
{
    public const int GreetingMultiCheck = 9;
    public const int AllClausesClass = 0;
    public const int AnyClauseClass = 1;
    public const int FirstClauseClass = 2;

    public static int Evaluate(PreCheckInfoEntry entry, Func<PreCheckClause, bool> holds)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(holds);
        if (entry.Clauses.Count == 0) return 0;
        var first = entry.Clauses[0];
        switch (entry.Class)
        {
            case AllClausesClass:
                return entry.Clauses.All(holds) ? first.Yes : first.No;
            case AnyClauseClass:
                return entry.Clauses.Any(holds) ? first.Yes : first.No;
            case FirstClauseClass:
                foreach (var clause in entry.Clauses)
                {
                    if (holds(clause)) return clause.Yes;
                }
                return first.No;
            default:
                return 0;
        }
    }
}
