namespace tPoseidon.Core.Data;

public interface ISkillStore
{
    IReadOnlyList<Skill> GetForCharacter(ulong characterId);

    Skill? Find(ulong characterId, int skillId);

    void Upsert(Skill skill);

    int DeleteAllForCharacter(ulong characterId);

    int DeleteAboveLevel(ulong characterId, int maxLevel);
}
