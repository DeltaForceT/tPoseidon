namespace tPoseidon.Core.Data;

public readonly record struct ItemProperty(int Type, int Id, short Value, uint Extra);

public static class ItemPropertyTypes
{
    public const int LastMixSlot = 9;
    public const int Stat = 10;
    public const int RefineBonus = 0xB;
    public const int Options = 0xD;
    public const int MaxPerItem = 20;
    public const int MaxStored = 19;
}

public interface IItemPropertyStore
{
    IReadOnlyList<ItemProperty> ForItem(ulong itemUid);

    void Replace(ulong itemUid, IReadOnlyList<ItemProperty> properties);

    void Delete(ulong itemUid);
}

public interface IItemRefineStore
{
    void SetRefine(ulong itemUid, byte level, byte state);

    void SetMixSlot(ulong itemUid, byte slots);
}
