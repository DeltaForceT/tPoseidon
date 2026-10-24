namespace tPoseidon.Core.Game.Combat;

public interface ISkill
{
    ushort SkillId { get; }
    string Name { get; }
    SkillClass SkillClass { get; }
    bool IsActive { get; }
    bool IsSupport { get; }
    ushort RendererFactoryId { get; }
    int MaxSkillLevel { get; }
    string EsaTableName { get; }
    string EsaiiTableName { get; }
}
