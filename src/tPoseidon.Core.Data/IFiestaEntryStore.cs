namespace tPoseidon.Core.Data;

public interface IFiestaEntryStore
{
    (byte Count, DateTime? Slot) Load(ulong characterId);

    void Save(ulong characterId, byte count, DateTime? slot);
}
