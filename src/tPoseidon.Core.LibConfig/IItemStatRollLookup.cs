namespace tPoseidon.Core.LibConfig;

public interface IItemStatRollLookup
{
    IReadOnlyList<RandomStatValueEntry> GetById(int id);
    IReadOnlyCollection<RandomStatValueEntry> All { get; }
    int Count { get; }
}
