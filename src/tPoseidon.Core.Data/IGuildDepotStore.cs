namespace tPoseidon.Core.Data;

public static class GuildDepotRules
{
    public const int MaxTabs = 10;
    public const int TabSlots = 80;
    public const int HistoryShown = 20;
    public const int HistoryKept = 200;
    public const int StackLimit = 32000;
    public const int BagCapacity = 300;
    public const long NotStorableOption = 0x10000;

    public const ushort DepotRight = 0x40;
    public const ushort MasterRights = 0xFFFF;
    public const byte DepositAction = 0;
    public const byte WithdrawAction = 1;

    public static int Admit(GuildDepotAccess? access, byte? tab)
    {
        if (access is null || !access.MayUse) return GuildDepotErrors.NoPermission;
        if (access.Tabs == 0) return GuildDepotErrors.NotPurchased;
        if (tab is null) return GuildDepotErrors.None;
        if (tab < 1 || tab > MaxTabs) return GuildDepotErrors.BadItem;
        return tab > access.Tabs ? GuildDepotErrors.NotExpanded : GuildDepotErrors.None;
    }
}

public static class GuildDepotErrors
{
    public const int None = 0;
    public const int Unknown = 1;
    public const int AreaUnavailable = 2;
    public const int RequestPending = 3;
    public const int RequestFailed = 4;
    public const int NoPermission = 5;
    public const int NotPurchased = 6;
    public const int Busy = 7;
    public const int ItemMissing = 8;
    public const int NoSlot = 9;
    public const int CannotAdd = 10;
    public const int TooManyCarried = 11;
    public const int ItemsPresent = 12;
    public const int BadItem = 13;
    public const int NotExpanded = 14;
    public const int NotStorable = 15;
    public const int Unusable = 16;
}

public enum GuildDepotChange : byte
{
    Created,
    Grown,
    Shrunk,
    Removed,
}

public sealed record GuildDepotItem(
    ulong ItemUid,
    int ItemIndex,
    byte ItemClass,
    ushort Stack,
    short RefineType,
    byte RefineLevel,
    byte RefineState,
    byte MixSlot,
    byte Tab);

public sealed record GuildDepotSide(GuildDepotItem Item, GuildDepotChange Change, ushort Moved);

public sealed record GuildDepotHistoryEntry(
    long Sequence,
    string CharacterName,
    int ItemIndex,
    ushort Count,
    byte Action,
    DateTimeOffset At);

public sealed record GuildDepotTransfer(
    int Error,
    GuildDepotSide? Depot,
    GuildDepotSide? Bag,
    GuildDepotHistoryEntry? History)
{
    public static GuildDepotTransfer Fail(int error) => new(error, null, null, null);
}

public sealed record GuildDepotDeposit(
    ulong GuildCode,
    ulong CharacterId,
    string CharacterName,
    ulong ItemUid,
    ushort Count,
    byte Tab,
    bool Stackable);

public sealed record GuildDepotWithdrawal(
    ulong GuildCode,
    ulong CharacterId,
    string CharacterName,
    ulong ItemUid,
    ushort Count,
    byte Tab,
    bool Stackable,
    int UnitWeight,
    int WeightRoom);

public sealed record GuildDepotAccess(ulong GuildCode, ushort Rights, int Tabs)
{
    public bool MayUse => (Rights & GuildDepotRules.DepotRight) != 0;
}

public interface IGuildDepotStore
{
    GuildDepotAccess? AccessOf(ulong characterId);

    int TabsOf(ulong guildCode);

    bool SetTabs(ulong guildCode, int tabs);

    IReadOnlyList<GuildDepotItem> Contents(ulong guildCode, byte tab);

    IReadOnlyList<GuildDepotHistoryEntry> History(ulong guildCode);

    GuildDepotTransfer Deposit(GuildDepotDeposit request);

    GuildDepotTransfer Withdraw(GuildDepotWithdrawal request);
}
