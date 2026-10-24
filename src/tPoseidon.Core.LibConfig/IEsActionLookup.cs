namespace tPoseidon.Core.LibConfig;

public interface IEsActionLookup
{
    EsActionEntry? GetBySkillAndLevel(string skillName, int level);
    IReadOnlyList<EsActionEntry> GetBySkill(string skillName);
    IReadOnlyCollection<EsActionEntry> All { get; }
    int Count { get; }
}
