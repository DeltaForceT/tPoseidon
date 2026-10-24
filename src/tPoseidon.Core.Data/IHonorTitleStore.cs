namespace tPoseidon.Core.Data;

public sealed record HonorTitleRecord(ushort TitleId, uint QuestId, byte QuestType, ushort ClearTime, ushort HunterCount);

public sealed record HonorTitleLogin(IReadOnlyList<ushort> Titles, ushort ProfileTitleType, uint ProfileTitleId);

public interface IHonorTitleStore
{
    HonorTitleLogin Load(ulong characterId);

    bool Insert(ulong characterId, IReadOnlyList<HonorTitleRecord> titles);

    void SetProfileTitle(ulong characterId, ushort titleType, uint titleId);
}
