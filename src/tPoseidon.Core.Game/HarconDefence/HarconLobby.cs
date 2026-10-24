using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.HarconDefence;

public static class HarconRules
{
    public const int EnterFunction = 259;
    public const int CannonFunction = 262;
    public const int SkillFunction = 264;
    public const int CannonCheckFunction = 265;
    public const int ShopFunction = 272;
    public const int CannonNotEnoughPoints = 3;
    public const int CannonActive = 4;
    public const int CannonCheckFree = 1;
    public const int CannonCheckActive = 2;
    public static readonly TimeSpan TurretLifetime = TimeSpan.FromMinutes(2);
    public const int MinLevel = 30;
    public const int DailyEntries = 6;
    public const int MaxRooms = 60;
    public const int RunsPerPlayZone = 3;
    public const int MaxTitleLength = 32;
    public const int DailyLimitResult = 1;
    public const byte CategoryLimit = 7;
    public const byte Ready = 1;
    public const byte NotReady = 2;
    public static readonly TimeSpan FinishDelay = TimeSpan.FromSeconds(10);

    public static int ShopPrice(int coins, int count) => coins * count;

    public const byte InvalidPlayerState = 1;
    public const byte CreateInvalidPlayerState = 2;
    public const byte CreateInvalidStage = 3;
    public const byte CreateNotCleared = 4;
    public const byte CreateItemMissing = 5;
    public const byte CreateInvalidTitle = 6;
    public const byte CreateInvalidMaxPlayers = 7;
    public const byte CreateFailed = 8;
    public const byte EnterInvalidPlayerState = 10;
    public const byte EnterNoStage = 11;
    public const byte EnterFull = 12;
    public const byte EnterStageState = 13;
    public const byte LeaveInvalidPlayerState = 15;
    public const byte LeaveNoStage = 16;
    public const byte BanInvalidPlayerState = 17;
    public const byte BanNoStage = 18;
    public const byte BanNotMaster = 19;
    public const byte BanNoPlayer = 20;
    public const byte StartInvalidPlayerState = 22;
    public const byte StartNoStage = 23;
    public const byte StartStageState = 24;
    public const byte StartNotMaster = 25;
    public const byte StartNotEnoughPlayers = 26;
    public const byte StartItemMissing = 27;
    public const byte ReadyInvalidPlayerState = 28;
    public const byte ReadyNoStage = 29;
    public const byte ReadyStageState = 30;
    public const byte BeginEntriesUsed = 35;
    public const byte BeginPlayZoneFull = 36;
    public const byte Banned = 37;

    public const int EventCategory = 4;
    public const int SealedHarconMonsterType = 18;

    public static bool IsTarget(int monsterType) => monsterType == SealedHarconMonsterType;

    public static bool KeepsRecords(HarconStageEntry stage) => stage is not null && stage.Category != EventCategory;

    public static bool CanEnter(int level, int entriesToday) => level >= MinLevel && entriesToday < DailyEntries;

    public static int EntriesToday(int count, DateOnly? day, DateOnly today) => day == today ? count : 0;
}

public enum HarconRoomState
{
    Waiting,
    Readying,
    Playing,
    Finished,
}

public sealed class HarconMember
{
    public HarconMember(ulong characterId, ushort entityId, string name)
    {
        CharacterId = characterId;
        EntityId = entityId;
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    public ulong CharacterId { get; }
    public ushort EntityId { get; }
    public string Name { get; }
    public byte ReadyState { get; set; }
}

public sealed class HarconRoom
{
    private readonly List<HarconMember> _members = new();

    public HarconRoom(uint key, HarconStageEntry stage, string title, byte maxPlayers)
    {
        Key = key;
        Stage = stage ?? throw new ArgumentNullException(nameof(stage));
        Title = title ?? throw new ArgumentNullException(nameof(title));
        MaxPlayers = maxPlayers;
    }

    public uint Key { get; }
    public HarconStageEntry Stage { get; }
    public string Title { get; }
    public byte MaxPlayers { get; }
    public HarconRoomState State { get; set; } = HarconRoomState.Waiting;
    public IReadOnlyList<HarconMember> Members => _members;
    public HarconMember? Master => _members.Count > 0 ? _members[0] : null;
    public bool InProgress => State is HarconRoomState.Playing or HarconRoomState.Finished;

    public HarconMember? Member(ulong characterId) => _members.FirstOrDefault(member => member.CharacterId == characterId);

    public HarconMember? MemberNamed(string name) => _members.FirstOrDefault(member => string.Equals(member.Name, name, StringComparison.Ordinal));

    public void Add(HarconMember member) => _members.Add(member ?? throw new ArgumentNullException(nameof(member)));

    public bool Remove(HarconMember member) => _members.Remove(member);

    public bool AllReady => _members.Count > 0 && _members.Skip(1).All(member => member.ReadyState == HarconRules.Ready);
}

public readonly record struct HarconCreate(int StageId, string Title, byte MaxPlayers);

public sealed class HarconLobby
{
    private readonly Dictionary<uint, HarconRoom> _rooms = new();
    private readonly Dictionary<ulong, HarconRoom> _byCharacter = new();
    private uint _nextKey = 1;

    public IReadOnlyCollection<HarconRoom> Rooms => _rooms.Values;

    public HarconRoom? RoomOf(ulong characterId) => _byCharacter.GetValueOrDefault(characterId);

    public HarconRoom? Find(uint key) => _rooms.GetValueOrDefault(key);

    public int RunsOn(int playMap) => _rooms.Values.Count(room => room.InProgress && room.Stage.PlayMap == playMap);

    public byte? CheckCreate(HarconDefTables tables, HarconCreate request, ulong characterId, IReadOnlySet<int> cleared, Func<int, bool> holdsItem)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(cleared);
        ArgumentNullException.ThrowIfNull(holdsItem);
        if (_byCharacter.ContainsKey(characterId)) return HarconRules.CreateInvalidPlayerState;
        if (!tables.Stages.TryGetValue(request.StageId, out var stage) || !tables.Waves.ContainsKey(stage.WaveTable)) return HarconRules.CreateInvalidStage;
        if (stage.UnlockId != 0 && !cleared.Contains(stage.UnlockId)) return HarconRules.CreateNotCleared;
        if (stage.Item != 0 && !holdsItem(stage.Item)) return HarconRules.CreateItemMissing;
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > HarconRules.MaxTitleLength) return HarconRules.CreateInvalidTitle;
        if (request.MaxPlayers < stage.MinPlayers || request.MaxPlayers > stage.MaxPlayers) return HarconRules.CreateInvalidMaxPlayers;
        if (_rooms.Count >= HarconRules.MaxRooms) return HarconRules.CreateFailed;
        return null;
    }

    public HarconRoom Create(HarconStageEntry stage, HarconCreate request, HarconMember master)
    {
        ArgumentNullException.ThrowIfNull(stage);
        ArgumentNullException.ThrowIfNull(master);
        while (_rooms.ContainsKey(_nextKey) || _nextKey == 0) _nextKey = _nextKey >= ushort.MaxValue ? 1 : _nextKey + 1;
        var room = new HarconRoom(_nextKey, stage, request.Title, request.MaxPlayers);
        _nextKey = _nextKey >= ushort.MaxValue ? 1 : _nextKey + 1;
        room.Add(master);
        _rooms[room.Key] = room;
        _byCharacter[master.CharacterId] = room;
        return room;
    }

    public byte? CheckJoin(uint key, ulong characterId)
    {
        if (_byCharacter.ContainsKey(characterId)) return HarconRules.EnterInvalidPlayerState;
        if (!_rooms.TryGetValue(key, out var room)) return HarconRules.EnterNoStage;
        if (room.State != HarconRoomState.Waiting) return HarconRules.EnterStageState;
        if (room.Members.Count >= room.MaxPlayers) return HarconRules.EnterFull;
        return null;
    }

    public HarconRoom Join(uint key, HarconMember member)
    {
        ArgumentNullException.ThrowIfNull(member);
        var room = _rooms[key];
        room.Add(member);
        _byCharacter[member.CharacterId] = room;
        return room;
    }

    public HarconMember? Leave(ulong characterId, out HarconRoom? room, out bool closed)
    {
        closed = false;
        if (!_byCharacter.Remove(characterId, out room)) return null;
        var member = room.Member(characterId);
        if (member is not null) room.Remove(member);
        if (room.Members.Count == 0)
        {
            _rooms.Remove(room.Key);
            closed = true;
        }
        return member;
    }

    public void Close(HarconRoom room)
    {
        ArgumentNullException.ThrowIfNull(room);
        _rooms.Remove(room.Key);
        foreach (var member in room.Members) _byCharacter.Remove(member.CharacterId);
    }

    public static byte? CheckBan(HarconRoom? room, ulong masterId, HarconMember? target)
    {
        if (room is null) return HarconRules.BanNoStage;
        if (room.State != HarconRoomState.Waiting) return HarconRules.BanInvalidPlayerState;
        if (room.Master?.CharacterId != masterId) return HarconRules.BanNotMaster;
        if (target is null || target.CharacterId == masterId) return HarconRules.BanNoPlayer;
        return null;
    }

    public static byte? CheckStart(HarconRoom? room, ulong characterId, Func<int, bool> holdsItem)
    {
        ArgumentNullException.ThrowIfNull(holdsItem);
        if (room is null) return HarconRules.StartNoStage;
        if (room.State != HarconRoomState.Waiting) return HarconRules.StartStageState;
        if (room.Master?.CharacterId != characterId) return HarconRules.StartNotMaster;
        if (room.Members.Count < room.Stage.MinPlayers) return HarconRules.StartNotEnoughPlayers;
        if (room.Stage.Item != 0 && !holdsItem(room.Stage.Item)) return HarconRules.StartItemMissing;
        return null;
    }

    public static byte? CheckReady(HarconRoom? room, ulong characterId)
    {
        if (room is null) return HarconRules.ReadyNoStage;
        if (room.State != HarconRoomState.Readying) return HarconRules.ReadyStageState;
        if (room.Member(characterId) is null || room.Master?.CharacterId == characterId) return HarconRules.ReadyInvalidPlayerState;
        return null;
    }
}
