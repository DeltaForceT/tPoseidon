namespace tPoseidon.Core.LibConfig;

public sealed record ItemParamEntry
{
        public required int Id { get; init; }

        public int Class { get; init; }

        public int Type { get; init; }

        public int SubType { get; init; }

        public string Name { get; init; } = string.Empty;

        public string NameEng { get; init; } = string.Empty;

        public int MinLevel { get; init; }

        public int Weight { get; init; }

        public int Value { get; init; }

        public string FileName { get; init; } = string.Empty;

        public int BundleNum { get; init; }

        public string InvFileName { get; init; } = string.Empty;

        public int InvBundleNum { get; init; }

        public int Effect { get; init; }

    public int ExistType { get; init; }

        public const int RebirthEffect = 29;

    public int Options { get; init; }

    public int Life { get; init; }

    public int CardNum { get; init; }

    public int ItemFType { get; init; }

    public int Hp { get; init; }

    public int HpCon { get; init; }

    public int MpCon { get; init; }

    public int Ap { get; init; }

    public float HpRecoveryRate { get; init; }

    public float MpRecoveryRate { get; init; }

    public int RefineType { get; init; }

    public int RefineIndex { get; init; }

    public int CompoundSlot { get; init; }

    public int Mp { get; init; }

    public int ExtendedOptions { get; init; }

    public int CharacterTypeFlags { get; init; }

    public int CharacterGroupFlags { get; init; }

    public int CharacterGender { get; init; }

    public int MinStatType { get; init; }

    public int MinStatLevel { get; init; }

    public IReadOnlyList<int> StatBonuses { get; init; } = Array.Empty<int>();

    public int GroupId { get; init; }

    public int ReformCount { get; init; }

    public const int UsableOption = 0x2;
    public const int KeptWhenUsedExtendedOption = 0x40;
    public const int OnlyDropperTakesOption = 0x800;
    public const int KeptInBagOption = 0x1000;
    public const int StackableClass = 1;
    public const int RareClass = 2;
    public const int RareClassInTable = 3;
    public const int UseItemType = 15;
    public const int PotionType = 10;
    public const int MoneyType = 30;

    public bool IsUsable => (Options & UsableOption) != 0;

    public bool OnlyDropperTakes => (Options & OnlyDropperTakesOption) != 0;

    public bool IsKeptInBag => (Options & KeptInBagOption) != 0;

    public bool IsMoney => Type == MoneyType;

    public bool IsStackable => Class == StackableClass;

    public byte ObjectClass => Class == RareClassInTable ? (byte)RareClass : (byte)Class;

    public const int RandomStatsExtendedOption = 0x1;
    public const int GunSubType = 54;

    public bool HasRandomStats => (ExtendedOptions & RandomStatsExtendedOption) != 0;

    public bool IsKeptWhenUsed => (ExtendedOptions & KeptWhenUsedExtendedOption) != 0;
}
