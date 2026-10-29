namespace tPoseidon.Core.Game.MyCamp;

public static class MyCampRules
{
    public const ushort OwnerX = 490;
    public const ushort OwnerY = 330;
    public const int MaxGuests = 16;
    public const int TitleLength = 32;
    public const ushort DefaultModel = 0x759B;
    public const ushort ShellZoneBase = 0xF000;
    public const int InventoryCap = 1799;
    public const int ItemCap = 1800;
    public const int TransferChunkBytes = 2048;
    public static readonly TimeSpan KickBan = TimeSpan.FromMilliseconds(600000);

    public const byte Decorate = 0;
    public const byte Chat = 1;
    public const byte Shop = 2;

    public const byte TransferOk = 0;
    public const byte TransferFailed = 1;

    public const byte StateNew = 0;
    public const byte StateInventory = 1;
    public const byte StatePlaced = 2;

    public const byte OpenBusy = 1;
    public const byte OpenHasCamp = 4;
    public const byte OpenZoneType = 5;
    public const byte OpenInRegion = 6;
    public const byte OpenBlockedTile = 8;

    public const byte EnterFailed = 1;
    public const byte EnterNotReady = 9;
    public const byte EnterNotOpen = 0x0B;
    public const byte EnterWrongPassword = 0x0C;
    public const byte EnterFull = 0x0D;
    public const byte EnterBanned = 0x10;

    public const byte CloseReasonSave = 1;
    public const byte KickReasonModeChange = 3;

    public const byte HouseFlagState = 1;
    public const byte HouseFlagChat = 2;
    public const byte HouseFlagModel = 4;

    public static bool OpenZone(int zoneType) => zoneType is 1 or 2 or 10;

    public static bool BlockedTile(byte attribute) => attribute is 1 or 2 or 3;

    public static bool IsShellZone(ushort mapId) => mapId >= ShellZoneBase;

    public static byte? OpenRefusal(bool busy, bool hasCamp, int zoneType, bool inShell, bool inRegion, byte tileAttribute)
    {
        if (busy) return OpenBusy;
        if (hasCamp) return OpenHasCamp;
        if (!OpenZone(zoneType)) return OpenZoneType;
        if (inShell) return OpenBusy;
        if (inRegion) return OpenInRegion;
        if (BlockedTile(tileAttribute)) return OpenBlockedTile;
        return null;
    }
}

public readonly record struct MyCampItemRecord(ulong ItemUid, ulong ParentsUid, ushort ItemIndex, ulong LimitDate, byte State, short X = 0, short Y = 0, byte Z = 0, byte Direction = 0)
{
    public int WireSize => 27 + (State == MyCampRules.StatePlaced ? 6 : 0);
}

public enum MyCampTransferKind
{
    Open,
    ModeChange,
    Guest,
    Recycle,
    Shop,
}

public sealed class MyCampTransfer
{
    private readonly Queue<MyCampItemRecord> _left;

    public MyCampTransfer(IReadOnlyCollection<MyCampItemRecord> records, bool forGuest)
        : this(records, forGuest ? MyCampTransferKind.Guest : MyCampTransferKind.Open)
    {
    }

    public MyCampTransfer(IReadOnlyCollection<MyCampItemRecord> records, MyCampTransferKind kind)
    {
        ArgumentNullException.ThrowIfNull(records);
        Count = records.Count;
        Kind = kind;
        _left = new Queue<MyCampItemRecord>(records);
    }

    public int Count { get; }
    public MyCampTransferKind Kind { get; }
    public bool ForGuest => Kind == MyCampTransferKind.Guest;
    public bool Done => _left.Count == 0;

    public IReadOnlyList<MyCampItemRecord> NextChunk(int maxBytes)
    {
        var chunk = new List<MyCampItemRecord>();
        var used = 0;
        while (_left.Count > 0 && used + _left.Peek().WireSize <= maxBytes)
        {
            var record = _left.Dequeue();
            used += record.WireSize;
            chunk.Add(record);
        }
        return chunk;
    }
}

public sealed class MyCampShell
{
    private readonly HashSet<ulong> _guests = new();
    private readonly Dictionary<ulong, DateTimeOffset> _bans = new();
    private readonly Dictionary<ulong, MyCampItemRecord> _layout = new();

    public MyCampShell(ulong ownerId, uint accountId, string ownerName, ulong slotCode, string slotName, ushort shellZone, ushort houseId, ushort fieldMap, ushort houseX, ushort houseY, DateTime openedAt)
    {
        OwnerId = ownerId;
        AccountId = accountId;
        OwnerName = ownerName ?? throw new ArgumentNullException(nameof(ownerName));
        SlotCode = slotCode;
        SlotName = slotName ?? string.Empty;
        ShellZone = shellZone;
        HouseId = houseId;
        FieldMap = fieldMap;
        HouseX = houseX;
        HouseY = houseY;
        OpenedAt = openedAt;
    }

    public ulong OwnerId { get; }
    public uint AccountId { get; }
    public string OwnerName { get; }
    public ulong SlotCode { get; }
    public string SlotName { get; set; }
    public ushort ShellZone { get; }
    public ushort HouseId { get; }
    public ushort FieldMap { get; }
    public ushort HouseX { get; }
    public ushort HouseY { get; }
    public DateTime OpenedAt { get; }
    public byte Mode { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool HasPassword { get; set; }
    public byte MaxGuests { get; set; } = MyCampRules.MaxGuests;
    public ushort Model { get; set; } = MyCampRules.DefaultModel;
    public bool Ready { get; set; }
    public IReadOnlyCollection<ulong> Guests => _guests;
    public IReadOnlyDictionary<ulong, MyCampItemRecord> Layout => _layout;

    public void SetChat(string title, byte maxGuests, bool hasPassword, string password)
    {
        Title = Truncate(title ?? string.Empty, MyCampRules.TitleLength);
        MaxGuests = (byte)Math.Clamp((int)maxGuests, 0, MyCampRules.MaxGuests);
        HasPassword = hasPassword;
        Password = hasPassword ? password ?? string.Empty : string.Empty;
    }

    public byte? EnterRefusal(ulong guestId, string password, bool skipPassword, DateTimeOffset now)
    {
        if (!Ready) return MyCampRules.EnterNotReady;
        if (Mode != MyCampRules.Chat) return MyCampRules.EnterNotOpen;
        if (HasPassword && !skipPassword && !string.Equals(Password, password, StringComparison.Ordinal)) return MyCampRules.EnterWrongPassword;
        if (_guests.Count >= MaxGuests || _guests.Count >= MyCampRules.MaxGuests) return MyCampRules.EnterFull;
        if (_bans.TryGetValue(guestId, out var until) && now < until) return MyCampRules.EnterBanned;
        return null;
    }

    public bool AddGuest(ulong guestId) => guestId != OwnerId && _guests.Add(guestId);

    public bool RemoveGuest(ulong guestId) => _guests.Remove(guestId);

    public void Ban(ulong guestId, DateTimeOffset now) => _bans[guestId] = now + MyCampRules.KickBan;

    public void LoadLayout(IEnumerable<MyCampItemRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        _layout.Clear();
        foreach (var record in records) _layout[record.ItemUid] = record;
    }

    public void Apply(IEnumerable<MyCampItemRecord> changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        foreach (var record in changed) _layout[record.ItemUid] = record;
    }

    public IReadOnlyList<MyCampItemRecord> Placed() => _layout.Values.Where(record => record.State == MyCampRules.StatePlaced).ToArray();

    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
}
