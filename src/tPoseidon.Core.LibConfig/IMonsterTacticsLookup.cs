namespace tPoseidon.Core.LibConfig;

public interface IMonsterTacticsLookup
{
    IReadOnlyList<TacticsMonEntry> GetTactics(int monsterId);
    IReadOnlyList<MonAITacticsEntry> GetAI(int monsterId);
    IReadOnlyCollection<TacticsMonEntry> AllTactics { get; }
    IReadOnlyCollection<MonAITacticsEntry> AllAI { get; }
    int TacticsCount { get; }
    int AICount { get; }
}
