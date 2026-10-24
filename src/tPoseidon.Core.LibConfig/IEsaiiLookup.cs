namespace tPoseidon.Core.LibConfig;

public interface IEsaiiLookup
{
    EsaiiEntry? GetBySkillAndLevel(string skillName, int level);
    IReadOnlyList<EsaiiEntry> GetBySkill(string skillName);
    IReadOnlyCollection<EsaiiEntry> All { get; }
    int Count { get; }
}
