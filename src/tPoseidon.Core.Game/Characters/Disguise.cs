namespace tPoseidon.Core.Game.Characters;

public enum DisguiseUse
{
    Disguised,
    NoKitRow,
    Refused,
}

public sealed class DisguiseState
{
    public int ShownType { get; private set; }
    public bool LooksDifferent { get; private set; }
    public bool Disguised { get; private set; }

    public void Reset(int realType)
    {
        ShownType = realType;
        LooksDifferent = false;
        Disguised = false;
    }

    public void Show(int type, int realType)
    {
        if (DisguiseRules.Storable(type)) ShownType = type;
        LooksDifferent = ShownType != realType;
    }

    public DisguiseUse UseKit(int? kitType, int realType, bool normalForm) =>
        kitType is { } type ? Wear(type, realType, normalForm) : DisguiseUse.NoKitRow;

    public DisguiseUse Wear(int type, int realType, bool normalForm)
    {
        Show(type, realType);
        if (!LooksDifferent || !normalForm) return DisguiseUse.Refused;
        Disguised = true;
        return DisguiseUse.Disguised;
    }

    public bool WearsDress => LooksDifferent && DisguiseRules.IsDress(ShownType);

    public bool Remove(int realType)
    {
        if (!LooksDifferent || !Disguised || ShownType == realType) return false;
        Show(realType, realType);
        Disguised = false;
        return true;
    }
}

public static class DisguiseRules
{
    public const int KitSubType = 71;
    public const int HighestShownType = 0x4671;
    public const byte DigRefusedDisguised = 0x10;
    public const byte DigRefusedOtherForm = 0x16;
    public const byte RemovedByRequest = 0;
    public const byte RemovedByCostumeEnd = 1;
    public const int FirstDressType = 18000;
    public const int LastDressType = 0x4670;
    public const int DressTypesPerIndex = 8;

    public static bool IsDress(int type) => type >= FirstDressType && type <= LastDressType;

    public static bool IsFemale(int characterType) => (characterType & 1) != 0;

    public static int DressType(int dressIndex, int characterType) =>
        FirstDressType + Math.Max(dressIndex, 1) * DressTypesPerIndex + characterType;

    public static bool Storable(int type) => type > 0 && type <= HighestShownType;

    public static byte? DigRefusal(bool disguised, bool otherForm) =>
        disguised ? DigRefusedDisguised : otherForm ? DigRefusedOtherForm : null;
}
