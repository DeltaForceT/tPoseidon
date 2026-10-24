namespace tPoseidon.Core.Data;

public sealed record CharacterStatsSnapshot
{
    public required long CurrentHp { get; init; }
    public required long CurrentMp { get; init; }
    public required ushort Level { get; init; }
    public required ulong Exp { get; init; }
    public required long Money { get; init; }
    public required long TmExp { get; init; }
    public required ushort TmLevel { get; init; }
    public required ushort TmPoint { get; init; }

    public ushort ApPoints { get; init; }
    public ushort AcPoints { get; init; }
    public ushort DxPoints { get; init; }
    public ushort MaPoints { get; init; }
    public ushort MdPoints { get; init; }
    public ushort DaPoints { get; init; }
    public ushort LkPoints { get; init; }
    public ushort DpPoints { get; init; }
    public ushort HvPoints { get; init; }
    public ushort MaxHpPoints { get; init; }
    public ushort MaxMpPoints { get; init; }
    public ushort MaxWtPoints { get; init; }
    public ushort AttrPoints { get; init; }
    public ushort TmLevelPower { get; init; }
    public ushort TmLevelMagic { get; init; }
    public ushort TmLevelSense { get; init; }
    public ushort TmLevelCharm { get; init; }
}
