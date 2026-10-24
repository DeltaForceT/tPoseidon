namespace tPoseidon.Core.Data;

public sealed class Character
{
        public required ulong CharacterId { get; init; }

        public uint CharacterIdLow => (uint)(CharacterId & 0xFFFFFFFFu);

    public required uint OwnerAccountId { get; init; }

        public required byte Slot { get; init; }

        public required string Name { get; init; }

        public string AccountLabel { get; init; } = string.Empty;

        public required Species Species { get; init; }

        public required FType FType { get; init; }

    public byte PtsP { get; init; }
    public byte PtsM { get; init; }
    public byte PtsS { get; init; }
    public byte PtsC { get; init; }

        public byte JobTier { get; init; } = 0x01;

        public ushort Hair { get; init; }

        public ushort Hair2 { get; init; }

        public ushort Hair3 { get; init; }

        public byte JobState { get; init; }

        public byte CharState { get; init; }

        public ushort Home { get; init; }

        public byte CharAuthority { get; init; }

        public short GuildState { get; init; }

        public bool IsGuildChief { get; init; }

        public DateTimeOffset GenTime { get; init; } = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public long Money { get; init; }

        public ushort Level { get; init; } = 1;

        public ushort StarterEarItemIndex { get; init; }

        public ushort StarterTailItemIndex { get; init; }

        public ushort PositionX { get; init; }

        public ushort PositionY { get; init; }

        public ushort MapId { get; init; }

    public long CurrentHp { get; set; }

    public long CurrentMp { get; init; }

    

    

    

    

        public ulong Exp { get; init; }

        public ulong TmExpPower { get; init; }
    public ulong TmExpMagic { get; init; }
    public ulong TmExpSense { get; init; }
    public ulong TmExpCharm { get; init; }

        public ushort WarehouseSlots { get; init; } = 80;

        public DateTimeOffset LoginTime { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LogoutTime { get; init; } = DateTimeOffset.UtcNow;

        public uint TotalConnectedSeconds { get; init; }

    

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
    public ushort TmPoint { get; init; }
    public ushort TmLevel { get; init; }
    public long TmExp { get; init; }

    

    public ushort Type2 { get; init; }
    public byte Job2 { get; init; }
    public DateTimeOffset Job2ChangedAt { get; init; } = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    
    public ushort Type3 { get; init; }
    public byte Job3 { get; init; }
    public DateTimeOffset Job3ChangedAt { get; init; } = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public byte ChangeJobQuestState { get; init; }

    public ulong DepthCount { get; init; }
    public uint DigCount { get; init; }
}
