using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Combat;

internal sealed class LibconfigSkill : ISkill
{
    private readonly SkillParam2Entry _entry;

    public LibconfigSkill(SkillParam2Entry entry)
    {
        _entry = entry;
    }

    public ushort SkillId => (ushort)_entry.Id;
    public string Name => _entry.Name;
    public SkillClass SkillClass => MapClass(_entry.SkillSymbolType);
    public bool IsActive => _entry.SpellType == 1;
    public bool IsSupport => _entry.Support;
    public ushort RendererFactoryId => (ushort)_entry.EsAction;
    public int MaxSkillLevel => _entry.MaxSkillLevel;
    public string EsaTableName => _entry.EsaTable;
    public string EsaiiTableName => _entry.EsaiiTable;

    private static SkillClass MapClass(int skillSymbolType) => skillSymbolType switch
    {
        0 => SkillClass.Magic,
        1 => SkillClass.Power,
        2 => SkillClass.Sense,
        3 => SkillClass.Charm,
        _ => SkillClass.Magic,
    };
}
