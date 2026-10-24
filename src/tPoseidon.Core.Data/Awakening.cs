namespace tPoseidon.Core.Data;

public sealed record CharAwakening
{
    public required ulong CharacterId { get; init; }
    public byte SoulSeedFlag { get; init; }
    public byte AwakeningFlag { get; init; }
    public short SlotMaxCount { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed record CharAwakeningLevelUp
{
    public required ulong CharacterId { get; init; }
    public required int CharLevel { get; init; }
    public DateTimeOffset LevelUpAt { get; init; }
}

public sealed record CharSoulGuardian
{
    public required ulong GuardianUid { get; init; }
    public required ulong CharacterId { get; init; }
    public required short GuardianType { get; init; }
    public byte SlotNum { get; init; }
    public byte EquipFlag { get; init; }
    public string GuardianName { get; init; } = "";
    public byte FType { get; init; }
    public byte PtsP { get; init; }
    public byte PtsM { get; init; }
    public byte PtsS { get; init; }
    public byte PtsC { get; init; }
    public int SkillPoint { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed record CharSoulGuardianSlot
{
    public required ulong CharacterId { get; init; }
    public required byte SlotNum { get; init; }
    public ulong? GuardianUid { get; init; }
}

public sealed record CharSoulSeed
{
    public required ulong CharacterId { get; init; }
    public int SeedItemIndex { get; init; }
    public short PowerPoint { get; init; }
    public short MagicPoint { get; init; }
    public short SensePoint { get; init; }
    public short CharmPoint { get; init; }
    public short SpecialPowerPoint { get; init; }
    public short SpecialMagicPoint { get; init; }
    public short SpecialSensePoint { get; init; }
    public short SpecialCharmPoint { get; init; }
    public DateTimeOffset StartAt { get; init; }
    public DateTimeOffset LastUpdateAt { get; init; }
}
