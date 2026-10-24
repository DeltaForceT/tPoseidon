namespace tPoseidon.Core.LibConfig;

public sealed record EsaiiEntry
{
    public required string SkillName { get; init; }
    public required int Level { get; init; }
    public int MpCon { get; init; }
    public float Delay { get; init; }
    public int Range { get; init; }
    public float CoolTime { get; init; }
    public float MonDelay { get; init; }
}
