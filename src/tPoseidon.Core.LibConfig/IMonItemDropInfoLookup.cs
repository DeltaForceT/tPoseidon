namespace tPoseidon.Core.LibConfig;

public interface IMonItemDropInfoLookup
{
        MonItemDropInfoEntry? GetById(int dropTableId);

        int Count { get; }
}
