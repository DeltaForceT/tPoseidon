namespace tPoseidon.Core.LibConfig;

public interface IItemParamLookup
{
        ItemParamEntry? GetById(int itemId);

        int Count { get; }
}
