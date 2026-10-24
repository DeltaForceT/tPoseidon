namespace tPoseidon.Core.Data;

public enum EquipUpgradeAction : byte
{
    Delete = 0,
    Insert = 1,
    Update = 2,
}

public sealed record EquipUpgradeItem(
    EquipUpgradeAction Action,
    ulong ItemUid,
    int ItemIndex,
    byte ItemClass,
    ushort Count,
    byte Slot,
    short RefineType,
    byte RefineLevel,
    byte RefineState);

public sealed record EquipStatusUpgrade(ulong CharacterId, uint Fee, int BagCapacity, IReadOnlyList<EquipUpgradeItem> Items);

public sealed record EquipStatusUpgradeResult(int Error, long RemainMoney, IReadOnlyList<EquipUpgradeItem> Items)
{
    public bool Succeeded => Error == EquipStatusUpgradeRules.Ok;

    public static EquipStatusUpgradeResult Refused(int error) => new(error, 0, Array.Empty<EquipUpgradeItem>());
}

public static class EquipStatusUpgradeRules
{
    public const int Ok = 0;
    public const int NotEnoughMoney = 1;
    public const int ItemMismatch = 2;
    public const int BagFull = 3;
    public const int StoreFailed = 4;
}

public interface IEquipStatusUpgradeStore
{
    EquipStatusUpgradeResult Apply(EquipStatusUpgrade upgrade);
}
