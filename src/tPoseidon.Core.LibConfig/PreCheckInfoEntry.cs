namespace tPoseidon.Core.LibConfig;

public readonly record struct PreCheckClause(int Type, int Argument, int Yes, int No);

public sealed record PreCheckInfoEntry(int Id, int Class, IReadOnlyList<PreCheckClause> Clauses)
{
    public const int Slots = 16;
}
