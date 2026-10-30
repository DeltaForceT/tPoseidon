namespace tPoseidon.Core.LibConfig;

public sealed record SkillParam2Entry
{
    public required int Id { get; init; }
    public int Type { get; init; }
    public string Name { get; init; } = string.Empty;
    public string IconFile { get; init; } = string.Empty;
    public int IconBundle { get; init; }
    public int SkillOption { get; init; }
    public bool LevelUp { get; init; }
    public int MaxSkillLevel { get; init; }
    public int MasterTmPoint { get; init; }
    public int EsAction { get; init; }
    public string EsaTable { get; init; } = string.Empty;
    public string EsaiiTable { get; init; } = string.Empty;
    public int SpellType { get; init; }
    public int SkillSymbolType { get; init; }
    public int Element { get; init; }
    public bool Support { get; init; }
    public int Grade { get; init; }
    public int UseStyle { get; init; }
    public int StateType { get; init; }
    public int PSAbility { get; init; }
    public int ItemInvokeType { get; init; }
    public int AffectionRel { get; init; }
    public int InvocationCondition { get; init; }
    public int TargetInput { get; init; }
    public int DebuffBitCode { get; init; }
    public int EState { get; init; }
    public int EsClass { get; init; }
    public int SkillExp { get; init; }
    public int SkillCardId { get; init; }
}
