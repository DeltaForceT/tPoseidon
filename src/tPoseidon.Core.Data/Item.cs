namespace tPoseidon.Core.Data;

public sealed record Item
{
        public required ulong ItemUid { get; init; }

        public required int ItemIndex { get; init; }

        public required byte ItemClass { get; init; }

        public ushort Stack { get; init; } = 1;

        public short ItemAttr { get; init; }

        public byte ItemCharged { get; init; }

        public short RefineType { get; init; }

        public byte RefineLevel { get; init; }

        public byte RefineState { get; init; }

        public byte ItemSlot { get; init; }

        public DateTimeOffset BreakedAt { get; init; } = NeverBreaks;

        public required ulong CharacterId { get; init; }

        public required byte SlotKind { get; init; }

        public required byte SlotPosition { get; init; }

    public const byte RareClass = 2;

    public static DateTimeOffset BreakedAfter(byte itemClass, int periodMinutes, DateTimeOffset now) =>
        periodMinutes > 0 && itemClass == RareClass ? now.AddMinutes(periodMinutes) : NeverBreaks;

    public static readonly DateTimeOffset NeverBreaks = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public int WeightUnits => ItemClass == RareClass ? 1 : Stack;
}

public static class EquipmentSlot
{
        public const byte Hat = 0;

        public const byte Weapon = 1;

        public const byte Shield = 2;

        public const byte Ear = 5;

        public const byte Tail = 6;

        public const byte Belt = 3;

        public const byte Accessory = 4;

        public const byte Drill = 7;

        public const byte Pet = 8;

        public const byte SecondAccessory = 9;

        public const byte FirstCostume = 10;

        public const byte Shoes = 18;

        public const byte Socks = 19;

        public const byte Common = 20;

        public const byte Cape = 21;

        public const byte SpeedAccessory = 22;

        public const int Count = 23;
}

public static class SlotKind
{
    public const byte Equipment = 0;
    public const byte Inventory = 1;
    public const byte Warehouse = 3;
}
