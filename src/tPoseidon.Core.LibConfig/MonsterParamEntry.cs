namespace tPoseidon.Core.LibConfig;

public sealed record MonsterParamEntry
{
    public required int Id { get; init; }
    public int Type { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Level { get; init; }
    public int MaxHpLv { get; init; }
    public int MaxMpLv { get; init; }
    public int ApLv { get; init; }
    public int AcLv { get; init; }
    public int DxLv { get; init; }
    public int DpLv { get; init; }
    public int MaLv { get; init; }
    public int MdLv { get; init; }
    public int MaxWtLv { get; init; }
    public int DaLv { get; init; }
    public int LkLv { get; init; }
    public int HvLv { get; init; }
    public int Exp { get; init; }
    public int Tm { get; init; }
    public int GMin { get; init; }
    public int GMax { get; init; }
    public double GelderRate { get; init; }
    public int ItemDropCnt { get; init; }
    public int ItemDropIdx { get; init; }
    public int HpRegen { get; init; }
    public int MpRegen { get; init; }
    public bool IsBoss { get; init; }
    public float WalkSpeed { get; init; }
    public float RunSpeed { get; init; }
    public float MaxStay { get; init; }
    public float MinStay { get; init; }
    public float ChaseTime { get; init; }
    public int AiAttackDispos { get; init; }
    public int AiTargetSetting1 { get; init; }
    public int AiTargetSetting2 { get; init; }
    public int AiDamageResponse { get; init; }
    public int AiDamageSpecial { get; init; }
    public int AiAttackLater { get; init; }
    public int AiGroupPolicy { get; init; }
    public int AiHelpResponse { get; init; }
    public int AiRetreatKind { get; init; }
    public int AiMoveArea { get; init; }
    public int AiMoveKind { get; init; }
    public int AiRetreatArea { get; init; }
    public int AiRetreatHp { get; init; }
    public int AiViewRange { get; init; }
    public string Speech { get; init; } = string.Empty;
    public bool DoesCntMonRetr { get; init; }
    public int RetrMonNum { get; init; }
    public string TacticsTable { get; init; } = string.Empty;
    public int FireR { get; init; }
    public int WaterR { get; init; }
    public int WindR { get; init; }
    public int EarthR { get; init; }
    public int ElecR { get; init; }
    public int LightR { get; init; }
    public int DarkR { get; init; }
    public int NoPropR { get; init; }
    public int PhysicalR { get; init; }
    public int GunR { get; init; }
    public int ShadowR { get; init; }
    public int MonFtype { get; init; }
    public string PropertyInfo { get; init; } = string.Empty;
    public int PropertyType { get; init; }
    public int PropertyRatio { get; init; }
    public string ProtectTable { get; init; } = string.Empty;
    public bool AccFormula { get; init; }
    public int DebuffSet { get; init; }
    public int DamageSkill { get; init; }
    public int DamageSkillEffect { get; init; }
}
