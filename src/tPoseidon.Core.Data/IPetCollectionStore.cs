namespace tPoseidon.Core.Data;

public sealed record PetCollectedItem(ulong CollectedUid, int ItemId, byte ItemClass, ushort Count);

public interface IPetCollectionStore
{
    IReadOnlyList<PetCollectedItem> Load(ulong characterId);

    PetCollectedItem Add(ulong characterId, int itemId, byte itemClass, ushort count);

    bool Remove(ulong characterId, ulong collectedUid);
}
