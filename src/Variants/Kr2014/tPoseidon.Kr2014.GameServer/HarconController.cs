using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.HarconDefence;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.Game.Quests;
using tPoseidon.Core.Game.Warp;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.Dialogs;
using tPoseidon.Core.Net.World;

internal sealed class HarconController : IDisposable
{
    private sealed class RunState
    {
        public RunState(HarconRun run) => Run = run;

        public HarconRun Run { get; }
        public List<ushort> Monsters { get; } = new();
        public List<ushort> Defended { get; } = new();
        public List<ushort> Targets { get; } = new();
        public Dictionary<ushort, long> Gauges { get; } = new();
        public Dictionary<ushort, (ushort Turret, DateTimeOffset Until, ulong Builder)> Turrets { get; } = new();
        public HashSet<ulong> Arrived { get; } = new();
        public HashSet<ulong> DeadNow { get; } = new();
        public Dictionary<ulong, int> Deaths { get; } = new();
        public Dictionary<ulong, int> TowersBuilt { get; } = new();
        public Dictionary<ulong, int> TowerScore { get; } = new();
        public bool Lost { get; set; }
        public bool TargetDestroyed { get; set; }
        public DateTimeOffset? DestroyAt { get; set; }
    }

    private readonly Func<HarconDefTables> _tables;
    private readonly IHarconDefStore _store;
    private readonly MonsterManager _monsters;
    private readonly IReadOnlyDictionary<ushort, ZoneGeometry> _geometry;
    private readonly Func<int, string> _mapMonsterTable;
    private readonly Func<IReadOnlyCollection<InGameSessionState>> _sessions;
    private readonly Func<MonsterEntity, byte[]> _spawnFrame;
    private readonly Func<ushort, byte[]> _despawnFrame;
    private readonly Func<ushort, Opcode, byte[], Task> _sendToMap;
    private readonly IItemStore _items;
    private readonly ItemManager _itemManager;
    private readonly IItemParamLookup _catalog;
    private readonly Action<ushort, int> _castTactic;
    private readonly int _bagCapacity;
    private readonly Encoding _text;
    private readonly string _logTag;
    private readonly HarconLobby _lobby = new();
    private readonly Dictionary<uint, RunState> _runs = new();
    private readonly SemaphoreSlim _sync = new(1, 1);
    private readonly object _killGate = new();
    private readonly List<(ushort Victim, int MonsterId, ushort Map, ushort Killer)> _kills = new();
    private readonly Random _rng = new();
    private IReadOnlyList<HarconBattle> _battles = Array.Empty<HarconBattle>();

    public HarconController(
        Func<HarconDefTables> tables, IHarconDefStore store, MonsterManager monsters, IReadOnlyDictionary<ushort, ZoneGeometry> geometry,
        Func<int, string> mapMonsterTable, Func<IReadOnlyCollection<InGameSessionState>> sessions, Func<MonsterEntity, byte[]> spawnFrame,
        Func<ushort, byte[]> despawnFrame, Func<ushort, Opcode, byte[], Task> sendToMap, IItemStore items, ItemManager itemManager,
        IItemParamLookup catalog, Action<ushort, int> castTactic, int bagCapacity, Encoding text, string logTag)
    {
        _tables = tables ?? throw new ArgumentNullException(nameof(tables));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _monsters = monsters ?? throw new ArgumentNullException(nameof(monsters));
        _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        _mapMonsterTable = mapMonsterTable ?? throw new ArgumentNullException(nameof(mapMonsterTable));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _spawnFrame = spawnFrame ?? throw new ArgumentNullException(nameof(spawnFrame));
        _despawnFrame = despawnFrame ?? throw new ArgumentNullException(nameof(despawnFrame));
        _sendToMap = sendToMap ?? throw new ArgumentNullException(nameof(sendToMap));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _itemManager = itemManager ?? throw new ArgumentNullException(nameof(itemManager));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _castTactic = castTactic ?? throw new ArgumentNullException(nameof(castTactic));
        _bagCapacity = bagCapacity;
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
        _monsters.MonsterKilled += OnMonsterKilled;
    }

    public IReadOnlyList<HarconBattle> BattlesOn(ushort mapId) =>
        Volatile.Read(ref _battles).Where(battle => battle.MapId == mapId).ToList();

    public HarconBattle? BattleOf(ushort entityId) =>
        Volatile.Read(ref _battles).FirstOrDefault(battle => battle.Holds(entityId));

    public void Dispose()
    {
        _monsters.MonsterKilled -= OnMonsterKilled;
        _sync.Dispose();
    }

    public async Task<int> EnterAsync(InGameSessionState st, IReadOnlyList<ushort> args)
    {
        ArgumentNullException.ThrowIfNull(st);
        ArgumentNullException.ThrowIfNull(args);
        if (st.Character is not { } character) return NpcScript.Refused;
        var level = st.Stats?.Level ?? character.Level;
        var record = _store.Load(character.CharacterId);
        var entries = HarconRules.EntriesToday(record.EntryCount, record.EntryDay, Today);
        if (level < HarconRules.MinLevel)
        {
            Log(st, $"lobby refused at level {level}");
            return NpcScript.Refused;
        }
        if (!HarconRules.CanEnter(level, entries))
        {
            Log(st, $"lobby refused: {entries} entries today");
            return HarconRules.DailyLimitResult;
        }
        var category = args.Count > 0 && args[0] < HarconRules.CategoryLimit ? (byte)args[0] : (byte)0;
        await _sync.WaitAsync().ConfigureAwait(false);
        try
        {
            st.HarconLobbyMap = st.MapId;
            await SendAsync(st, Opcode.S2C_HARCON_LOBBY, HarconFrames.Lobby(category, record.Coins, record.Cleared.Order().ToArray())).ConfigureAwait(false);
            await SendAsync(st, Opcode.S2C_HARCON_TOTAL_RECORD, HarconFrames.TotalRecord(_store.Totals(character.CharacterId))).ConfigureAwait(false);
            await SendAsync(st, Opcode.S2C_HARCON_ROOM_LIST, HarconFrames.RoomList(_lobby.Rooms, _text)).ConfigureAwait(false);
        }
        finally
        {
            _sync.Release();
        }
        Log(st, $"opens the lobby ({_lobby.Rooms.Count} rooms, {record.Coins} coins, {entries} entries today)");
        return NpcScript.Silent;
    }

    public Task RoomListAsync(InGameSessionState st) => Locked(async () =>
    {
        if (st.HarconLobbyMap is null || st.Character is not { } character) return;
        await SendAsync(st, Opcode.S2C_HARCON_TOTAL_RECORD, HarconFrames.TotalRecord(_store.Totals(character.CharacterId))).ConfigureAwait(false);
        await SendAsync(st, Opcode.S2C_HARCON_ROOM_LIST, HarconFrames.RoomList(_lobby.Rooms, _text)).ConfigureAwait(false);
    });

    public Task CreateAsync(InGameSessionState st, ReadOnlyMemory<byte> payload) => Locked(async () =>
    {
        if (st.Character is not { } character) return;
        if (!HarconFrames.TryReadCreate(payload.Span, _text, out var request))
        {
            Log(st, $"create of {payload.Length} bytes not read");
            return;
        }
        var tables = _tables();
        var record = _store.Load(character.CharacterId);
        var code = st.HarconLobbyMap is null
            ? HarconRules.CreateInvalidPlayerState
            : _lobby.CheckCreate(tables, new HarconCreate(request.StageId, request.Title, request.MaxPlayers), character.CharacterId, record.Cleared, item => Holds(character.CharacterId, item));
        if (code is { } refused)
        {
            Log(st, $"create of stage {request.StageId} refused: {refused}");
            await SendAsync(st, Opcode.S2C_HARCON_CREATE_ERROR, new[] { refused }).ConfigureAwait(false);
            return;
        }
        var stage = tables.Stages[request.StageId];
        var room = _lobby.Create(stage, new HarconCreate(request.StageId, request.Title, request.MaxPlayers), new HarconMember(character.CharacterId, st.PlayerEntityId, character.Name));
        await SendAsync(st, Opcode.S2C_HARCON_CREATED, HarconFrames.Created(room, _text)).ConfigureAwait(false);
        await SendAsync(st, Opcode.S2C_HARCON_MEMBER_ADDED, HarconFrames.Member(room.Master!, _text)).ConfigureAwait(false);
        await SendLobbyAsync(Opcode.S2C_HARCON_ROOM_ADDED, HarconFrames.Room(room, _text), st).ConfigureAwait(false);
        Log(st, $"creates room {room.Key} (stage {stage.Id}, {room.MaxPlayers} players)");
        await WarpAsync(st, (ushort)stage.WaitMap, null).ConfigureAwait(false);
    });

    public Task StageRecordAsync(InGameSessionState st, ReadOnlyMemory<byte> payload) => Locked(async () =>
    {
        if (st.Character is not { } character || !HarconFrames.TryReadStage(payload.Span, out var stageId)) return;
        var record = _tables().Stages.TryGetValue(stageId, out var stage) && HarconRules.KeepsRecords(stage)
            ? _store.StageRecord(character.CharacterId, stageId)
            : HarconStageRecord.Empty(stageId);
        await SendAsync(st, Opcode.S2C_HARCON_STAGE_RECORD, HarconFrames.StageRecord(record)).ConfigureAwait(false);
    });

    public Task JoinAsync(InGameSessionState st, ReadOnlyMemory<byte> payload) => Locked(async () =>
    {
        if (st.Character is not { } character || !HarconFrames.TryReadRoomKey(payload.Span, out var key)) return;
        var level = st.Stats?.Level ?? character.Level;
        var record = _store.Load(character.CharacterId);
        var code = st.HarconLobbyMap is null || !HarconRules.CanEnter(level, HarconRules.EntriesToday(record.EntryCount, record.EntryDay, Today))
            ? HarconRules.EnterInvalidPlayerState
            : _lobby.CheckJoin(key, character.CharacterId);
        if (code is { } refused)
        {
            Log(st, $"join of room {key} refused: {refused}");
            await SendAsync(st, Opcode.S2C_HARCON_JOIN_ERROR, new[] { refused }).ConfigureAwait(false);
            return;
        }
        var joiner = new HarconMember(character.CharacterId, st.PlayerEntityId, character.Name);
        var room = _lobby.Join(key, joiner);
        await SendAsync(st, Opcode.S2C_HARCON_ROOM_INFO, HarconFrames.Room(room, _text)).ConfigureAwait(false);
        foreach (var member in room.Members) await SendAsync(st, Opcode.S2C_HARCON_MEMBER_ADDED, HarconFrames.Member(member, _text)).ConfigureAwait(false);
        await SendRoomAsync(room, Opcode.S2C_HARCON_MEMBER_ADDED, HarconFrames.Member(joiner, _text), joiner.CharacterId).ConfigureAwait(false);
        await SendLobbyAsync(Opcode.S2C_HARCON_ROOM_COUNT, HarconFrames.RoomValue(room.Key, (byte)room.Members.Count), null).ConfigureAwait(false);
        Log(st, $"joins room {room.Key} ({room.Members.Count}/{room.MaxPlayers})");
        await WarpAsync(st, (ushort)room.Stage.WaitMap, null).ConfigureAwait(false);
    });

    public Task LeaveAsync(InGameSessionState st) => Locked(async () =>
    {
        if (st.Character is not { } character) return;
        if (_lobby.RoomOf(character.CharacterId) is not { } room)
        {
            await SendAsync(st, Opcode.S2C_HARCON_LEAVE_ERROR, new[] { HarconRules.LeaveNoStage }).ConfigureAwait(false);
            return;
        }
        if (room.State != HarconRoomState.Waiting)
        {
            await SendAsync(st, Opcode.S2C_HARCON_LEAVE_ERROR, new[] { HarconRules.LeaveInvalidPlayerState }).ConfigureAwait(false);
            return;
        }
        await RemoveAsync(st, character.CharacterId, Opcode.S2C_HARCON_LEFT, Array.Empty<byte>()).ConfigureAwait(false);
    });

    public Task KickAsync(InGameSessionState st, ReadOnlyMemory<byte> payload) => Locked(async () =>
    {
        if (st.Character is not { } character || !HarconFrames.TryReadName(payload.Span, _text, out var targetName)) return;
        var room = _lobby.RoomOf(character.CharacterId);
        var target = room?.MemberNamed(targetName);
        if (HarconLobby.CheckBan(room, character.CharacterId, target) is { } refused)
        {
            await SendAsync(st, Opcode.S2C_HARCON_KICK_ERROR, new[] { refused }).ConfigureAwait(false);
            return;
        }
        var kicked = Session(target!.CharacterId);
        await RemoveAsync(kicked, target.CharacterId, Opcode.S2C_HARCON_CLOSED, new[] { HarconRules.Banned }).ConfigureAwait(false);
        Log(st, $"kicks {target.Name} from room {room!.Key}");
    });

    public Task StartAsync(InGameSessionState st) => Locked(async () =>
    {
        if (st.Character is not { } character) return;
        var room = _lobby.RoomOf(character.CharacterId);
        if (HarconLobby.CheckStart(room, character.CharacterId, item => Holds(character.CharacterId, item)) is { } refused)
        {
            await SendAsync(st, Opcode.S2C_HARCON_START_ERROR, new[] { refused }).ConfigureAwait(false);
            return;
        }
        room!.State = HarconRoomState.Readying;
        foreach (var member in room.Members) member.ReadyState = 0;
        Log(st, $"starts room {room.Key}: ready check of {room.Members.Count - 1} members");
        if (room.AllReady)
        {
            await BeginAsync(room).ConfigureAwait(false);
            return;
        }
        foreach (var member in room.Members.Skip(1))
        {
            if (Session(member.CharacterId) is { } session) await SendAsync(session, Opcode.S2C_HARCON_READY_ASK, Array.Empty<byte>()).ConfigureAwait(false);
        }
    });

    public Task ReadyAsync(InGameSessionState st, ReadOnlyMemory<byte> payload) => Locked(async () =>
    {
        if (st.Character is not { } character || payload.Length < 1) return;
        var answer = payload.Span[0] == HarconRules.Ready ? HarconRules.Ready : HarconRules.NotReady;
        var room = _lobby.RoomOf(character.CharacterId);
        if (HarconLobby.CheckReady(room, character.CharacterId) is { } refused)
        {
            await SendAsync(st, Opcode.S2C_HARCON_START_ERROR, new[] { refused }).ConfigureAwait(false);
            return;
        }
        var member = room!.Member(character.CharacterId)!;
        member.ReadyState = answer;
        await SendRoomAsync(room, Opcode.S2C_HARCON_READY_ANSWER, HarconFrames.ReadyAnswer(member.Name, answer, _text), null).ConfigureAwait(false);
        if (answer == HarconRules.NotReady)
        {
            room.State = HarconRoomState.Waiting;
            foreach (var other in room.Members) other.ReadyState = 0;
            Log(st, $"declines the start of room {room.Key}");
            return;
        }
        if (!room.AllReady) return;
        await SendRoomAsync(room, Opcode.S2C_HARCON_ALL_READY, Array.Empty<byte>(), null).ConfigureAwait(false);
        await BeginAsync(room).ConfigureAwait(false);
    });

    public async Task<int> OpenShopAsync(InGameSessionState st, int npcType)
    {
        ArgumentNullException.ThrowIfNull(st);
        if (npcType is < 0 or > ushort.MaxValue || !_tables().ShopOfNpc.ContainsKey(npcType))
        {
            Log(st, $"NPC {npcType} has no Harcon coin shop");
            return NpcScript.Refused;
        }
        st.Upgrade = UpgradeWindow.HarconShop;
        st.HarconShopNpc = npcType;
        await SendAsync(st, (Opcode)HarconFrames.ShopOpened, HarconFrames.ShopOpen((ushort)npcType)).ConfigureAwait(false);
        Log(st, $"opens the Harcon coin shop of NPC {npcType}");
        return NpcScript.Silent;
    }

    public async Task CloseShopAsync(InGameSessionState st)
    {
        ArgumentNullException.ThrowIfNull(st);
        if (st.Upgrade == UpgradeWindow.HarconShop) st.Upgrade = UpgradeWindow.None;
        st.HarconShopNpc = 0;
        await SendAsync(st, (Opcode)HarconFrames.ShopClosed, Array.Empty<byte>()).ConfigureAwait(false);
    }

    public Task BuyAsync(InGameSessionState st, ReadOnlyMemory<byte> payload) => Locked(async () =>
    {
        if (st.Character is not { } character) return;
        if (!HarconFrames.TryReadShop(payload.Span, out var request) || st.Upgrade != UpgradeWindow.HarconShop || request.NpcType != st.HarconShopNpc
            || _tables().Offer(request.NpcType, (int)request.ItemId) is not { } row || row.ItemCount <= 0)
        {
            await SendAsync(st, Opcode.S2C_HARCON_SHOP_ERROR, new[] { HarconShopErrors.BadRequest }).ConfigureAwait(false);
            return;
        }
        var price = HarconRules.ShopPrice(row.Coins, row.ItemCount);
        var record = _store.Load(character.CharacterId);
        if (record.Coins < price)
        {
            await SendAsync(st, Opcode.S2C_HARCON_SHOP_ERROR, new[] { HarconShopErrors.NotEnoughCoins }).ConfigureAwait(false);
            return;
        }
        if (Grant(character.CharacterId, row.ItemId, (ushort)Math.Clamp(row.ItemCount, 1, ushort.MaxValue)) is not { } change)
        {
            await SendAsync(st, Opcode.S2C_HARCON_SHOP_ERROR, new[] { HarconShopErrors.BagFull }).ConfigureAwait(false);
            return;
        }
        var coins = _store.AddCoins(character.CharacterId, -price);
        await SendAsync(st, Opcode.S2C_HARCON_SHOP_BOUGHT, HarconFrames.Bought(QuestResultFrames.ItemOperation(change), coins)).ConfigureAwait(false);
        Log(st, $"buys {row.ItemCount} x item {row.ItemId} from shop {row.Shop} row {row.Id} of NPC {request.NpcType} for {price} coins ({coins} left)");
    });

    public async Task<int> PlayAsync(InGameSessionState st, int function, IReadOnlyList<ushort> args, NpcEntity? npc)
    {
        ArgumentNullException.ThrowIfNull(st);
        ArgumentNullException.ThrowIfNull(args);
        var result = NpcScript.Refused;
        await Locked(async () => result = await PlayLockedAsync(st, function, args, npc).ConfigureAwait(false)).ConfigureAwait(false);
        return result;
    }

    private async Task<int> PlayLockedAsync(InGameSessionState st, int function, IReadOnlyList<ushort> args, NpcEntity? npc)
    {
        if (st.Character is not { } character || npc is null || _lobby.RoomOf(character.CharacterId) is not { } room
            || !_runs.TryGetValue(room.Key, out var state) || !state.Run.Started || st.MapId != room.Stage.PlayMap)
        {
            Log(st, $"Harcon function {function} refused: not in a running stage");
            return NpcScript.Refused;
        }
        var now = DateTimeOffset.UtcNow;
        var active = state.Turrets.TryGetValue(npc.EntityId, out var turret) && turret.Until > now && _monsters.Get(turret.Turret) is { IsDead: false };
        switch (function)
        {
            case HarconRules.CannonCheckFunction:
                return active ? HarconRules.CannonCheckActive : HarconRules.CannonCheckFree;
            case HarconRules.CannonFunction when args.Count >= 2:
                if (active) return HarconRules.CannonActive;
                var score = state.Run.Scores.GetValueOrDefault(character.CharacterId);
                if (score < args[0])
                {
                    Log(st, $"turret {args[1]} refused: {score} of {args[0]} points");
                    return HarconRules.CannonNotEnoughPoints;
                }
                var x = (ushort)Math.Clamp(npc.Spawn.X, 0, ushort.MaxValue);
                var y = (ushort)Math.Clamp(npc.Spawn.Y, 0, ushort.MaxValue);
                if (_monsters.Spawn(args[1], (ushort)room.Stage.PlayMap, x, y) is not { } built) return NpcScript.Refused;
                state.Turrets[npc.EntityId] = (built.EntityId, now + HarconRules.TurretLifetime, character.CharacterId);
                state.TowersBuilt[character.CharacterId] = state.TowersBuilt.GetValueOrDefault(character.CharacterId) + 1;
                await _sendToMap((ushort)room.Stage.PlayMap, Opcode.NPC_SPAWN, _spawnFrame(built)).ConfigureAwait(false);
                Log(st, $"builds turret {args[1]} (0x{built.EntityId:X4}) at controller 0x{npc.EntityId:X4}");
                return NpcScript.Silent;
            case HarconRules.SkillFunction when args.Count >= 1:
                var harcon = state.Defended.Concat(state.Targets).Select(_monsters.Get).OfType<MonsterEntity>()
                    .Where(monster => !monster.IsDead)
                    .OrderBy(monster => Math.Abs(monster.DestinationX - npc.Spawn.X) + Math.Abs(monster.DestinationY - npc.Spawn.Y))
                    .FirstOrDefault();
                if (harcon is null) return NpcScript.Refused;
                _castTactic(harcon.EntityId, args[0]);
                Log(st, $"Harcon 0x{harcon.EntityId:X4} uses tactic {args[0]}");
                return NpcScript.Silent;
            default:
                return NpcScript.Refused;
        }
    }

    public Task EnteredZoneAsync(InGameSessionState st) => Locked(async () =>
    {
        if (st.Character is not { } character) return;
        var tables = _tables();
        var harconMap = tables.Stages.Values.Any(stage => stage.WaitMap == st.MapId || stage.PlayMap == st.MapId);
        if (st.HarconLobbyMap is { } lobbyMap && lobbyMap != st.MapId && !harconMap) st.HarconLobbyMap = null;
        if (_lobby.RoomOf(character.CharacterId) is not { } room) return;
        if (room.InProgress && _runs.TryGetValue(room.Key, out var state))
        {
            if (st.MapId == room.Stage.PlayMap)
            {
                state.Arrived.Add(character.CharacterId);
                if (!state.Run.Started && room.Members.All(member => state.Arrived.Contains(member.CharacterId)) && state.Run.Start(DateTimeOffset.UtcNow) is not null)
                {
                    await SendGaugesAsync(room, state, true).ConfigureAwait(false);
                    await SendRoomAsync(room, Opcode.S2C_HARCON_WAVE, new[] { (byte)state.Run.WaveIndex }).ConfigureAwait(false);
                    Console.WriteLine($"{_logTag} [harcon] room {room.Key}: wave 1 of stage {room.Stage.Id}");
                }
                return;
            }
            if (state.Arrived.Contains(character.CharacterId))
            {
                state.Run.Left(character.CharacterId);
                await RemoveAsync(null, character.CharacterId, null, Array.Empty<byte>()).ConfigureAwait(false);
            }
            return;
        }
        if (st.MapId != room.Stage.WaitMap && st.MapId != st.HarconLobbyMap)
        {
            await RemoveAsync(null, character.CharacterId, null, Array.Empty<byte>()).ConfigureAwait(false);
            Log(st, $"left the waiting map: out of room {room.Key}");
        }
    });

    public Task LeftAsync(InGameSessionState st) => Locked(async () =>
    {
        if (st.Character is not { } character || _lobby.RoomOf(character.CharacterId) is not { } room) return;
        if (_runs.TryGetValue(room.Key, out var state)) state.Run.Left(character.CharacterId);
        await RemoveAsync(null, character.CharacterId, null, Array.Empty<byte>()).ConfigureAwait(false);
    });

    public Task TickAsync(DateTimeOffset now) => Locked(async () =>
    {
        List<(ushort Victim, int MonsterId, ushort Map, ushort Killer)> kills;
        lock (_killGate)
        {
            kills = _kills.ToList();
            _kills.Clear();
        }
        if (_runs.Count == 0) return;
        var tables = _tables();
        foreach (var (key, state) in _runs.ToArray())
        {
            if (_lobby.Find(key) is not { } room)
            {
                await ClearMonstersAsync(state, (ushort)0).ConfigureAwait(false);
                _runs.Remove(key);
                continue;
            }
            var playMap = (ushort)room.Stage.PlayMap;
            if (state.DestroyAt is { } destroyAt)
            {
                if (now >= destroyAt) await DestroyAsync(room, state).ConfigureAwait(false);
                continue;
            }
            Credit(room, state, kills);
            foreach (var member in room.Members)
            {
                if (Session(member.CharacterId) is not { IsDead: true })
                {
                    state.DeadNow.Remove(member.CharacterId);
                    continue;
                }
                state.Run.Died(member.CharacterId);
                if (state.DeadNow.Add(member.CharacterId)) state.Deaths[member.CharacterId] = state.Deaths.GetValueOrDefault(member.CharacterId) + 1;
            }
            if (!state.Run.Started) continue;
            await SendGaugesAsync(room, state, false).ConfigureAwait(false);
            await ExpireTurretsAsync(state, playMap, now).ConfigureAwait(false);
            if (state.Lost || state.TargetDestroyed)
            {
                await EndAsync(room, state, state.TargetDestroyed && !state.Lost, tables, now).ConfigureAwait(false);
                continue;
            }
            foreach (var order in state.Run.DueSpawns(now)) await SpawnAsync(state.Monsters, playMap, order).ConfigureAwait(false);
            var alive = Alive(state);
            if (state.Run.NextWaveIfDone(now, alive) is not null)
            {
                await SendRoomAsync(room, Opcode.S2C_HARCON_WAVE, new[] { (byte)state.Run.WaveIndex }).ConfigureAwait(false);
                Console.WriteLine($"{_logTag} [harcon] room {room.Key}: wave {state.Run.WaveIndex + 1}");
            }
            if (state.Targets.Count == 0 && state.Run.Won(Alive(state))) await EndAsync(room, state, true, tables, now).ConfigureAwait(false);
        }
    });

    private void Credit(HarconRoom room, RunState state, IReadOnlyList<(ushort Victim, int MonsterId, ushort Map, ushort Killer)> kills)
    {
        foreach (var kill in kills)
        {
            if (kill.Map != room.Stage.PlayMap) continue;
            if (state.Targets.Remove(kill.Victim))
            {
                state.TargetDestroyed = true;
                Console.WriteLine($"{_logTag} [harcon] room {room.Key}: target {kill.MonsterId} destroyed");
                continue;
            }
            if (state.Defended.Remove(kill.Victim))
            {
                state.Lost = true;
                Console.WriteLine($"{_logTag} [harcon] room {room.Key}: defended monster {kill.MonsterId} destroyed");
                continue;
            }
            if (!state.Monsters.Remove(kill.Victim)) continue;
            if (state.Turrets.Values.FirstOrDefault(turret => turret.Turret == kill.Killer) is { Turret: not 0 } turretKill)
            {
                state.TowerScore[turretKill.Builder] = state.TowerScore.GetValueOrDefault(turretKill.Builder) + state.Run.Killed(turretKill.Builder);
                continue;
            }
            if (room.Members.FirstOrDefault(member => member.EntityId == kill.Killer) is { } killer) state.Run.Killed(killer.CharacterId);
        }
    }

    private async Task BeginAsync(HarconRoom room)
    {
        var tables = _tables();
        if (_lobby.RunsOn(room.Stage.PlayMap) >= HarconRules.RunsPerPlayZone)
        {
            await AbortStartAsync(room, HarconRules.BeginPlayZoneFull).ConfigureAwait(false);
            return;
        }
        var today = Today;
        var entering = new List<(HarconMember Member, InGameSessionState Session, HarconDefRecord Record)>();
        var failed = 0;
        foreach (var member in room.Members.ToArray())
        {
            if (Session(member.CharacterId) is not { } session || session.MapId != room.Stage.WaitMap)
            {
                failed++;
                _lobby.Leave(member.CharacterId, out _, out _);
                continue;
            }
            var record = _store.Load(member.CharacterId);
            if (HarconRules.EntriesToday(record.EntryCount, record.EntryDay, today) >= HarconRules.DailyEntries)
            {
                await AbortStartAsync(room, HarconRules.BeginEntriesUsed).ConfigureAwait(false);
                return;
            }
            entering.Add((member, session, record));
        }
        if (entering.Count == 0 || room.Master is not { } master)
        {
            if (_lobby.Find(room.Key) is not null) _lobby.Close(room);
            await SendLobbyAsync(Opcode.S2C_HARCON_ROOM_REMOVED, HarconFrames.RoomKey(room.Key), null).ConfigureAwait(false);
            return;
        }
        var masterChange = room.Stage.Item != 0 ? TakeOne(master.CharacterId, room.Stage.Item) : null;
        if (room.Stage.Item != 0 && masterChange is null)
        {
            await AbortStartAsync(room, HarconRules.StartItemMissing).ConfigureAwait(false);
            return;
        }
        room.State = HarconRoomState.Playing;
        var state = new RunState(new HarconRun(tables.Waves[room.Stage.WaveTable], tables.Points, room.Members.Select(member => member.CharacterId)));
        _runs[room.Key] = state;
        foreach (var (monsterId, regionId, count) in tables.DefendedSpawns.GetValueOrDefault(_mapMonsterTable(room.Stage.PlayMap)) ?? Array.Empty<(int, int, int)>())
        {
            var owned = HarconRules.IsTarget(_monsters.Param(monsterId)?.Type ?? 0) ? state.Targets : state.Defended;
            await SpawnAsync(owned, (ushort)room.Stage.PlayMap, new HarconSpawnOrder(monsterId, regionId, count)).ConfigureAwait(false);
        }
        var gauges = GaugeEntries(state).Select(monster => (monster.EntityId, monster.MaxHp)).ToArray();
        await SendLobbyAsync(Opcode.S2C_HARCON_ROOM_PROGRESS, HarconFrames.RoomValue(room.Key, 1), null).ConfigureAwait(false);
        var noChange = HarconFrames.NoItemChange();
        foreach (var (member, session, record) in entering)
        {
            _store.SaveEntries(member.CharacterId, HarconRules.EntriesToday(record.EntryCount, record.EntryDay, today) + 1, today);
            var change = member.CharacterId == master.CharacterId && masterChange is { } taken ? QuestResultFrames.ItemOperation(taken) : noChange;
            await SendAsync(session, Opcode.S2C_HARCON_GAME_START, HarconFrames.GameStart(room.Members, failed, gauges, change, _text)).ConfigureAwait(false);
        }
        Console.WriteLine($"{_logTag} [harcon] room {room.Key}: stage {room.Stage.Id} begins on map {room.Stage.PlayMap} with {entering.Count} players ({failed} failed)");
        foreach (var (_, session, _) in entering) await WarpAsync(session, (ushort)room.Stage.PlayMap, null).ConfigureAwait(false);
    }

    private async Task AbortStartAsync(HarconRoom room, byte code)
    {
        room.State = HarconRoomState.Waiting;
        foreach (var member in room.Members) member.ReadyState = 0;
        await SendRoomAsync(room, Opcode.S2C_HARCON_START_ERROR, new[] { code }, null).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} [harcon] room {room.Key}: start refused ({code})");
    }

    private async Task EndAsync(HarconRoom room, RunState state, bool won, HarconDefTables tables, DateTimeOffset now)
    {
        room.State = HarconRoomState.Finished;
        state.DestroyAt = now + HarconRules.FinishDelay;
        if (!won) await ClearMonstersAsync(state, (ushort)room.Stage.PlayMap).ConfigureAwait(false);
        tables.Drops.TryGetValue(room.Stage.DropId, out var drop);
        var keepsRecords = HarconRules.KeepsRecords(room.Stage);
        var placings = state.Run.Finish(won, drop);
        foreach (var placing in placings)
        {
            var coins = _store.AddCoins(placing.CharacterId, placing.Coins);
            if (keepsRecords)
            {
                _store.AddResult(placing.CharacterId, won);
                if (won) _store.AddClear(placing.CharacterId, room.Stage.Id);
                _store.AddStageResult(placing.CharacterId, new HarconStageResult(
                    room.Stage.Id, won, placing.Score, placing.Rank, HarconRun.Contribution(placing.Score, placings),
                    state.Deaths.GetValueOrDefault(placing.CharacterId), state.TowerScore.GetValueOrDefault(placing.CharacterId),
                    state.TowersBuilt.GetValueOrDefault(placing.CharacterId)));
            }
            if (Session(placing.CharacterId) is not { } session) continue;
            await SendAsync(session, Opcode.S2C_HARCON_GAME_END, HarconFrames.GameEnd(won, coins)).ConfigureAwait(false);
            if (keepsRecords)
            {
                await SendAsync(session, Opcode.S2C_HARCON_STAGE_RECORD, HarconFrames.StageRecord(_store.StageRecord(placing.CharacterId, room.Stage.Id))).ConfigureAwait(false);
            }
            await SendAsync(session, Opcode.S2C_HARCON_FINISH_NOTICE, Array.Empty<byte>()).ConfigureAwait(false);
            Console.WriteLine($"{_logTag} [harcon] room {room.Key}: {placing.CharacterId} rank {placing.Rank}, score {placing.Score}, +{placing.Coins} coins ({coins})");
        }
        Console.WriteLine($"{_logTag} [harcon] room {room.Key}: stage {room.Stage.Id} {(won ? "cleared" : "failed")} at wave {state.Run.WaveIndex + 1}");
    }

    private async Task DestroyAsync(HarconRoom room, RunState state)
    {
        _runs.Remove(room.Key);
        await ClearMonstersAsync(state, (ushort)room.Stage.PlayMap).ConfigureAwait(false);
        var members = room.Members.ToArray();
        _lobby.Close(room);
        await SendLobbyAsync(Opcode.S2C_HARCON_ROOM_REMOVED, HarconFrames.RoomKey(room.Key), null).ConfigureAwait(false);
        foreach (var member in members)
        {
            if (Session(member.CharacterId) is not { } session) continue;
            await SendAsync(session, Opcode.S2C_HARCON_DESTROYED, Array.Empty<byte>()).ConfigureAwait(false);
            if (session.MapId == room.Stage.PlayMap) await WarpAsync(session, (ushort)room.Stage.WaitMap, null).ConfigureAwait(false);
        }
        Console.WriteLine($"{_logTag} [harcon] room {room.Key} closed");
    }

    private async Task RemoveAsync(InGameSessionState? self, ulong characterId, Opcode? reply, byte[] replyPayload)
    {
        var member = _lobby.Leave(characterId, out var room, out var closed);
        if (member is null || room is null) return;
        if (self is not null && reply is { } opcode) await SendAsync(self, opcode, replyPayload).ConfigureAwait(false);
        await SendRoomAsync(room, Opcode.S2C_HARCON_MEMBER_LEFT, HarconFrames.Name(member.Name, _text), null).ConfigureAwait(false);
        if (closed)
        {
            if (_runs.Remove(room.Key, out var state)) await ClearMonstersAsync(state, (ushort)room.Stage.PlayMap).ConfigureAwait(false);
            await SendLobbyAsync(Opcode.S2C_HARCON_ROOM_REMOVED, HarconFrames.RoomKey(room.Key), null).ConfigureAwait(false);
            Console.WriteLine($"{_logTag} [harcon] room {room.Key} closed: last member {member.Name} left");
            return;
        }
        await SendLobbyAsync(Opcode.S2C_HARCON_ROOM_COUNT, HarconFrames.RoomValue(room.Key, (byte)room.Members.Count), null).ConfigureAwait(false);
        if (room.Members[0].CharacterId != member.CharacterId && room.State == HarconRoomState.Waiting)
        {
            await SendRoomAsync(room, Opcode.S2C_HARCON_ROOM_INFO, HarconFrames.Room(room, _text), null).ConfigureAwait(false);
        }
    }

    private async Task SpawnAsync(List<ushort> owned, ushort mapId, HarconSpawnOrder order)
    {
        _geometry.TryGetValue(mapId, out var geometry);
        if (geometry?.Regions.FirstOrDefault(candidate => candidate.Id == order.RegionId) is not { } area)
        {
            Console.WriteLine($"{_logTag} [harcon] map {mapId} has no region {order.RegionId} for monster {order.MonsterId}");
            return;
        }
        for (var index = 0; index < order.Count; index++)
        {
            ushort x, y;
            lock (_rng)
            {
                x = (ushort)_rng.Next(Math.Min(area.X1, area.X2), Math.Max(area.X1, area.X2) + 1);
                y = (ushort)_rng.Next(Math.Min(area.Y1, area.Y2), Math.Max(area.Y1, area.Y2) + 1);
            }
            if (_monsters.Spawn(order.MonsterId, mapId, x, y, null, null, order.RegionId) is not { } monster) continue;
            owned.Add(monster.EntityId);
            await _sendToMap(mapId, Opcode.NPC_SPAWN, _spawnFrame(monster)).ConfigureAwait(false);
        }
    }

    private async Task ClearMonstersAsync(RunState state, ushort mapId)
    {
        foreach (var id in state.Monsters.Concat(state.Defended).Concat(state.Targets).Concat(state.Turrets.Values.Select(turret => turret.Turret)).ToArray())
        {
            if (!_monsters.Remove(id) || mapId == 0) continue;
            await _sendToMap(mapId, Opcode.S2C_ENTITY_DESPAWN, _despawnFrame(id)).ConfigureAwait(false);
        }
        state.Monsters.Clear();
        state.Defended.Clear();
        state.Targets.Clear();
        state.Turrets.Clear();
    }

    private async Task ExpireTurretsAsync(RunState state, ushort mapId, DateTimeOffset now)
    {
        foreach (var (controller, (turret, until, _)) in state.Turrets.ToArray())
        {
            if (until > now && _monsters.Get(turret) is { IsDead: false }) continue;
            state.Turrets.Remove(controller);
            if (_monsters.Remove(turret)) await _sendToMap(mapId, Opcode.S2C_ENTITY_DESPAWN, _despawnFrame(turret)).ConfigureAwait(false);
        }
    }

    private IEnumerable<MonsterEntity> GaugeEntries(RunState state) =>
        state.Defended.Concat(state.Targets).Select(_monsters.Get).OfType<MonsterEntity>().Take(HarconFrames.MaxGauges);

    private async Task SendGaugesAsync(HarconRoom room, RunState state, bool all)
    {
        if (all)
        {
            state.Gauges.Clear();
            foreach (var monster in GaugeEntries(state)) state.Gauges[monster.EntityId] = monster.CurrentHp;
        }
        var changed = new List<(ushort EntityId, long Value)>();
        foreach (var (id, last) in state.Gauges.ToArray())
        {
            var hp = _monsters.Get(id) is { IsDead: false } monster ? monster.CurrentHp : 0;
            if (!all && hp == last) continue;
            state.Gauges[id] = hp;
            changed.Add((id, hp));
        }
        if (changed.Count == 0) return;
        await SendRoomAsync(room, Opcode.S2C_HARCON_GAUGES, HarconFrames.Gauges(changed), null).ConfigureAwait(false);
    }

    private int Alive(RunState state)
    {
        state.Monsters.RemoveAll(id => _monsters.Get(id) is not { IsDead: false });
        return state.Monsters.Count;
    }

    private void OnMonsterKilled(MonsterEntity monster, ushort killer)
    {
        lock (_killGate) _kills.Add((monster.EntityId, monster.MonsterParamId, monster.MapId, killer));
    }

    private bool Holds(ulong characterId, int itemId) =>
        _items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory).Any(item => item.ItemIndex == itemId);

    private BagOperation? TakeOne(ulong characterId, int itemId)
    {
        if (_items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory).FirstOrDefault(item => item.ItemIndex == itemId) is not { } held) return null;
        var itemClass = _catalog.GetById(held.ItemIndex)?.ObjectClass ?? held.ItemClass;
        if (held.Stack > 1)
        {
            var left = _items.AdjustStack(held.ItemUid, -1);
            return new BagOperation(QuestResultFrames.StackChanged, itemClass, (uint)held.ItemIndex, BagItemIds.OnWire(held.ItemUid), left);
        }
        _items.Delete(held.ItemUid);
        return new BagOperation(QuestResultFrames.Removed, itemClass, (uint)held.ItemIndex, BagItemIds.OnWire(held.ItemUid), 0);
    }

    private BagOperation? Grant(ulong characterId, int itemId, ushort count)
    {
        var definition = _catalog.GetById(itemId);
        var itemClass = definition?.ObjectClass ?? (byte)ItemParamEntry.StackableClass;
        var bag = _items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory);
        var stack = definition is { IsStackable: true } ? bag.FirstOrDefault(item => item.ItemIndex == itemId) : null;
        if (stack is not null)
        {
            var total = _items.AdjustStack(stack.ItemUid, count);
            return new BagOperation(QuestResultFrames.StackChanged, itemClass, (uint)itemId, BagItemIds.OnWire(stack.ItemUid), total);
        }
        if (bag.Count >= _bagCapacity) return null;
        var minted = _itemManager.GrantRewardItem(characterId, itemId, count, itemClassOverride: itemClass);
        return new BagOperation(QuestResultFrames.Created, itemClass, (uint)itemId, BagItemIds.OnWire(minted.ItemUid), minted.Stack, minted.ItemSlot);
    }

    private static Task WarpAsync(InGameSessionState st, ushort zone, (ushort X, ushort Y)? position)
    {
        if (st.MapId == zone || st.RequestZoneChange is not { } requestZoneChange) return Task.CompletedTask;
        return requestZoneChange(new ZoneChangeRequest(zone, TeleportArrival.ChangeZoneType, null, TimeSpan.Zero, position), default);
    }

    private InGameSessionState? Session(ulong characterId) =>
        _sessions().FirstOrDefault(session => session.Character?.CharacterId == characterId);

    private async Task SendRoomAsync(HarconRoom room, Opcode opcode, byte[] payload, ulong? except = null)
    {
        foreach (var member in room.Members.ToArray())
        {
            if (member.CharacterId == except || Session(member.CharacterId) is not { } session) continue;
            await SendAsync(session, opcode, payload).ConfigureAwait(false);
        }
    }

    private async Task SendLobbyAsync(Opcode opcode, byte[] payload, InGameSessionState? except)
    {
        foreach (var viewer in _sessions().Where(session => session.HarconLobbyMap is not null && !ReferenceEquals(session, except)).ToArray())
        {
            await SendAsync(viewer, opcode, payload).ConfigureAwait(false);
        }
    }

    private async Task Locked(Func<Task> action)
    {
        await _sync.WaitAsync().ConfigureAwait(false);
        try
        {
            await action().ConfigureAwait(false);
        }
        finally
        {
            Volatile.Write(ref _battles, Battles());
            _sync.Release();
        }
    }

    private List<HarconBattle> Battles() =>
        _runs
            .Where(run => run.Value.Run.Started && run.Value.DestroyAt is null && !run.Value.Lost && !run.Value.TargetDestroyed)
            .Select(run => (Room: _lobby.Find(run.Key), State: run.Value))
            .Where(run => run.Room is not null)
            .Select(run => new HarconBattle(
                (ushort)run.Room!.Stage.PlayMap,
                run.State.Defended.Concat(run.State.Targets),
                run.State.Monsters,
                run.State.Turrets.Values.Select(turret => (turret.Turret, turret.Builder))))
            .ToList();

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    private void Log(InGameSessionState st, string what) =>
        Console.WriteLine($"{_logTag} [harcon] 0x{st.PlayerEntityId:X4} {what}");

    private Task SendAsync(InGameSessionState player, Opcode opcode, byte[] payload)
    {
        byte randKey;
        lock (_rng) randKey = (byte)_rng.Next(1, 256);
        return player.SendFrameAsync(opcode, payload, randKey, default);
    }
}

internal static class HarconShopErrors
{
    public const byte BadRequest = 2;
    public const byte NotEnoughCoins = 3;
    public const byte BagFull = 4;
}
