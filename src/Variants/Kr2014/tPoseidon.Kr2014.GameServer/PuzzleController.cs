using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.Game.Puzzle;
using tPoseidon.Core.Game.Warp;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net;
using tPoseidon.Core.Net.Dialogs;
using tPoseidon.Core.Net.World;

internal sealed class PuzzleController : IDisposable
{
    private readonly Func<PuzzleTables> _tables;
    private readonly MonsterManager _monsters;
    private readonly IReadOnlyDictionary<ushort, ZoneGeometry> _geometry;
    private readonly Func<IReadOnlyCollection<InGameSessionState>> _sessions;
    private readonly Func<InGameSessionState, DateTimeOffset, (int X, int Y)> _positionAt;
    private readonly Func<MonsterEntity, byte[]> _spawnFrame;
    private readonly Func<ushort, byte[]> _despawnFrame;
    private readonly Func<ushort, Opcode, byte[], Task> _sendToMap;
    private readonly IItemStore _items;
    private readonly ItemManager _itemManager;
    private readonly IItemParamLookup _catalog;
    private readonly int _bagCapacity;
    private readonly string _logTag;
    private readonly Dictionary<ushort, (MemoryPuzzleRun Run, ulong OwnerId)> _runs = new();
    private readonly Dictionary<ushort, (SlidePuzzleRun Run, ulong OwnerId)> _slides = new();
    private readonly List<ushort> _kills = new();
    private readonly SemaphoreSlim _sync = new(1, 1);
    private readonly Random _rng = new();

    public PuzzleController(
        Func<PuzzleTables> tables, MonsterManager monsters, IReadOnlyDictionary<ushort, ZoneGeometry> geometry,
        Func<IReadOnlyCollection<InGameSessionState>> sessions, Func<InGameSessionState, DateTimeOffset, (int X, int Y)> positionAt, Func<MonsterEntity, byte[]> spawnFrame, Func<ushort, byte[]> despawnFrame,
        Func<ushort, Opcode, byte[], Task> sendToMap, IItemStore items, ItemManager itemManager, IItemParamLookup catalog, int bagCapacity, string logTag)
    {
        _tables = tables ?? throw new ArgumentNullException(nameof(tables));
        _monsters = monsters ?? throw new ArgumentNullException(nameof(monsters));
        _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _positionAt = positionAt ?? throw new ArgumentNullException(nameof(positionAt));
        _spawnFrame = spawnFrame ?? throw new ArgumentNullException(nameof(spawnFrame));
        _despawnFrame = despawnFrame ?? throw new ArgumentNullException(nameof(despawnFrame));
        _sendToMap = sendToMap ?? throw new ArgumentNullException(nameof(sendToMap));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _itemManager = itemManager ?? throw new ArgumentNullException(nameof(itemManager));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _bagCapacity = bagCapacity;
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
        _monsters.MonsterKilled += OnMonsterKilled;
    }

    public void Dispose()
    {
        _monsters.MonsterKilled -= OnMonsterKilled;
        _sync.Dispose();
    }

    public Task StartAsync(InGameSessionState st) => Locked(async () =>
    {
        if (st.Character is not { } character) return;
        var tables = _tables();
        if (!tables.Maps.TryGetValue(st.MapId, out var map))
        {
            Log(st, $"puzzle start on map {st.MapId}: no {PuzzleTables.MapTable} row");
            return;
        }
        if (_runs.ContainsKey(st.MapId) || _slides.ContainsKey(st.MapId))
        {
            Log(st, $"puzzle on map {st.MapId} already running");
            return;
        }
        if (map.PuzzleType == PuzzleTables.SlideType)
        {
            await StartSlideAsync(st, character.CharacterId, tables, map).ConfigureAwait(false);
            return;
        }
        if (map.PuzzleType != PuzzleTables.MemoryType)
        {
            Log(st, $"puzzle {map.PuzzleId} on map {st.MapId}: puzzle type {map.PuzzleType} is not known");
            return;
        }
        if (!tables.Memory.TryGetValue(map.PuzzleId, out var puzzle) || !tables.MemoryObjects.TryGetValue(puzzle.ObjectTable, out var objects))
        {
            Log(st, $"memory puzzle {map.PuzzleId}: no {PuzzleTables.MemoryTable} / object table");
            return;
        }
        var points = _geometry.TryGetValue(st.MapId, out var geometry)
            ? geometry.PointsOfType(ZoneGeometry.PointTypeRegionSpawn).Select(point => point.Id).ToHashSet()
            : new HashSet<int>();
        IReadOnlyList<(int PositionId, int MonsterId)> order;
        lock (_rng) order = MemoryPuzzleRun.ChooseOrder(objects, points, puzzle.ObjectCount, _rng.Next);
        if (order.Count == 0)
        {
            Log(st, $"memory puzzle {puzzle.Id}: map {st.MapId} has none of the object positions");
            return;
        }
        _runs[st.MapId] = (new MemoryPuzzleRun(puzzle, st.MapId, order, DateTimeOffset.UtcNow), character.CharacterId);
        await SendAsync(st, Opcode.S2C_PUZZLE_COUNTDOWN, Array.Empty<byte>()).ConfigureAwait(false);
        var w = new PacketWriter(2);
        w.WriteInt16((short)puzzle.Id);
        await SendAsync(st, Opcode.S2C_PUZZLE_MEMORY_START, w.ToArray()).ConfigureAwait(false);
        Log(st, $"memory puzzle {puzzle.Id} starts on map {st.MapId}: {order.Count} statues, {puzzle.TimeLimitSeconds} s");
    });

    public Task TickAsync(DateTimeOffset now) => Locked(async () =>
    {
        ushort[] kills;
        lock (_kills)
        {
            kills = _kills.ToArray();
            _kills.Clear();
        }
        foreach (var (mapId, (run, ownerId)) in _runs.ToArray())
        {
            var owner = Owner(ownerId);
            if (owner is null || owner.MapId != mapId)
            {
                await EndAsync(mapId, run, null).ConfigureAwait(false);
                continue;
            }
            foreach (var kill in kills) run.Killed(kill);
            if (run.NextToShow(now) is { } next) await ShowAsync(run, next).ConfigureAwait(false);
            if (run.StartInputIfShown(now)) await SendAsync(owner, Opcode.S2C_PUZZLE_MEMORY_INPUT, Array.Empty<byte>()).ConfigureAwait(false);
            run.TimedOut(now);
            if (run.Phase == MemoryPuzzlePhase.Finished) await EndAsync(mapId, run, owner).ConfigureAwait(false);
        }
        foreach (var (mapId, (slide, ownerId)) in _slides.ToArray())
        {
            var owner = Owner(ownerId);
            if (owner is null || owner.MapId != mapId)
            {
                _slides.Remove(mapId);
                continue;
            }
            if (slide.TimedOut(now)) await EndSlideAsync(slide, owner, now).ConfigureAwait(false);
        }
    });

    public Task StepAsync(ushort mapId, DateTimeOffset now) => Locked(async () =>
    {
        if (!_slides.TryGetValue(mapId, out var entry)) return;
        var owner = Owner(entry.OwnerId);
        if (owner is null || owner.MapId != mapId) return;
        var (x, y) = _positionAt(owner, now);
        if (entry.Run.StepAt(x, y) is not { } move) return;
        await SendAsync(owner, Opcode.S2C_PUZZLE_SLIDE_MOVE, PuzzleFrames.SlideMove(move)).ConfigureAwait(false);
        if (entry.Run.Result is not null) await EndSlideAsync(entry.Run, owner, now).ConfigureAwait(false);
    });

    public Task LeftAsync(InGameSessionState st) => Locked(async () =>
    {
        if (st.Character is not { } character) return;
        foreach (var (mapId, (run, ownerId)) in _runs.ToArray())
        {
            if (ownerId == character.CharacterId) await EndAsync(mapId, run, null).ConfigureAwait(false);
        }
        foreach (var (mapId, (_, ownerId)) in _slides.ToArray())
        {
            if (ownerId == character.CharacterId) _slides.Remove(mapId);
        }
    });

    private async Task StartSlideAsync(InGameSessionState st, ulong ownerId, PuzzleTables tables, PuzzleMapEntry map)
    {
        if (!tables.Slide.TryGetValue(map.PuzzleId, out var puzzle) || !tables.SlideBlocks.TryGetValue(puzzle.BlockTable, out var blocks))
        {
            Log(st, $"slide puzzle {map.PuzzleId}: no {PuzzleTables.SlideTable} row / block table");
            return;
        }
        if (puzzle.Columns <= 0 || puzzle.Rows <= 0 || blocks.Count != puzzle.Columns * puzzle.Rows || blocks.Count(block => block.IsEmpty) != 1)
        {
            Log(st, $"slide puzzle {puzzle.Id}: {puzzle.BlockTable} has {blocks.Count} blocks for {puzzle.Columns} x {puzzle.Rows}, not started");
            return;
        }
        var centre = _geometry.TryGetValue(st.MapId, out var geometry)
            ? geometry.PointsOfType(ZoneGeometry.PointTypeSpawn).Take(1).ToArray()
            : Array.Empty<ZonePoint>();
        if (centre.Length == 0)
        {
            Log(st, $"slide puzzle {puzzle.Id}: map {st.MapId} has no spawn point for the board centre, not started");
            return;
        }
        var origin = SlidePuzzleBoard.Origin(centre[0].X, centre[0].Y, puzzle.Columns, puzzle.Rows);
        int[] grid;
        lock (_rng) grid = SlidePuzzleBoard.Shuffle(blocks, puzzle.Columns, puzzle.Rows, _rng.Next);
        var now = DateTimeOffset.UtcNow;
        var run = new SlidePuzzleRun(puzzle, st.MapId, origin, blocks, grid, now);
        var (x, y) = _positionAt(st, now);
        run.Arrive(x, y);
        _slides[st.MapId] = (run, ownerId);
        await SendAsync(st, Opcode.S2C_PUZZLE_COUNTDOWN, Array.Empty<byte>()).ConfigureAwait(false);
        await SendAsync(st, Opcode.S2C_PUZZLE_SLIDE_START, PuzzleFrames.SlideStart(puzzle.Id, origin, grid)).ConfigureAwait(false);
        Log(st, $"slide puzzle {puzzle.Id} starts on map {st.MapId}: {puzzle.Columns} x {puzzle.Rows} at ({origin.X},{origin.Y}), {puzzle.TimeLimitSeconds} s");
    }

    private async Task EndSlideAsync(SlidePuzzleRun run, InGameSessionState owner, DateTimeOffset now)
    {
        _slides.Remove(run.MapId);
        if (run.Result is not { } result) return;
        await FinishAsync(owner, Opcode.S2C_PUZZLE_SLIDE_END, $"slide puzzle {run.Puzzle.Id}", result, run.RecordSeconds(now), run.Puzzle.Rewards, run.Puzzle.PortalMap)
            .ConfigureAwait(false);
    }

    private async Task ShowAsync(MemoryPuzzleRun run, (int PositionId, int MonsterId) next)
    {
        var point = _geometry.TryGetValue(run.MapId, out var geometry)
            ? geometry.PointsOfType(ZoneGeometry.PointTypeRegionSpawn).Where(candidate => candidate.Id == next.PositionId).Take(1).ToArray()
            : Array.Empty<ZonePoint>();
        if (point.Length == 0 || _monsters.Spawn(next.MonsterId, run.MapId, (ushort)point[0].X, (ushort)point[0].Y) is not { } statue)
        {
            Console.WriteLine($"{_logTag} [puzzle] map {run.MapId}: statue {next.MonsterId} at position {next.PositionId} not spawned");
            run.Shown(0);
            return;
        }
        run.Shown(statue.EntityId);
        await _sendToMap(run.MapId, Opcode.NPC_SPAWN, _spawnFrame(statue)).ConfigureAwait(false);
    }

    private async Task EndAsync(ushort mapId, MemoryPuzzleRun run, InGameSessionState? owner)
    {
        _runs.Remove(mapId);
        foreach (var id in run.Spawned)
        {
            if (id == 0 || !_monsters.Remove(id)) continue;
            await _sendToMap(mapId, Opcode.S2C_ENTITY_DESPAWN, _despawnFrame(id)).ConfigureAwait(false);
        }
        if (owner is null || run.Result is not { } result) return;
        await FinishAsync(owner, Opcode.S2C_PUZZLE_MEMORY_END, $"memory puzzle {run.Puzzle.Id}", result, run.RecordSeconds(DateTimeOffset.UtcNow), run.Puzzle.Rewards, run.Puzzle.PortalMap)
            .ConfigureAwait(false);
    }

    private async Task FinishAsync(
        InGameSessionState owner, Opcode resultOpcode, string what, uint result, uint record, IReadOnlyList<(int ItemId, int Count)> rewards, int portalMap)
    {
        await SendAsync(owner, resultOpcode, PuzzleFrames.Result(result, record)).ConfigureAwait(false);
        Log(owner, $"{what} {(result == PuzzleRules.Solved ? "solved" : "failed")}");
        if (result != PuzzleRules.Solved) return;
        foreach (var (itemId, count) in rewards)
        {
            if (Grant(owner, itemId, (ushort)Math.Clamp(count, 1, ushort.MaxValue)) is { } change)
            {
                await SendAsync(owner, Opcode.S2C_ITEM_OPERATION, QuestResultFrames.ItemOperation(change)).ConfigureAwait(false);
            }
        }
        if (portalMap != 0 && owner.RequestZoneChange is { } requestZoneChange)
        {
            await requestZoneChange(new ZoneChangeRequest((ushort)portalMap, TeleportArrival.ChangeZoneType, null, TimeSpan.Zero), default).ConfigureAwait(false);
        }
    }

    private BagOperation? Grant(InGameSessionState owner, int itemId, ushort count)
    {
        if (owner.Character is not { } character) return null;
        var definition = _catalog.GetById(itemId);
        var itemClass = definition?.ObjectClass ?? (byte)ItemParamEntry.StackableClass;
        var bag = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
        var stack = definition is { IsStackable: true } ? bag.FirstOrDefault(item => item.ItemIndex == itemId) : null;
        if (stack is not null)
        {
            var total = _items.AdjustStack(stack.ItemUid, count);
            return new BagOperation(QuestResultFrames.StackChanged, itemClass, (uint)itemId, BagItemIds.OnWire(stack.ItemUid), total);
        }
        if (bag.Count >= _bagCapacity)
        {
            Log(owner, $"puzzle reward {itemId} not given: bag full");
            return null;
        }
        var minted = _itemManager.GrantRewardItem(character.CharacterId, itemId, count, itemClassOverride: itemClass);
        return new BagOperation(QuestResultFrames.Created, itemClass, (uint)itemId, BagItemIds.OnWire(minted.ItemUid), minted.Stack, minted.ItemSlot);
    }

    private void OnMonsterKilled(MonsterEntity monster, ushort killer)
    {
        lock (_kills) _kills.Add(monster.EntityId);
    }

    private InGameSessionState? Owner(ulong characterId) =>
        _sessions().FirstOrDefault(session => session.Character?.CharacterId == characterId);

    private async Task Locked(Func<Task> action)
    {
        await _sync.WaitAsync().ConfigureAwait(false);
        try
        {
            await action().ConfigureAwait(false);
        }
        finally
        {
            _sync.Release();
        }
    }

    private void Log(InGameSessionState st, string what) =>
        Console.WriteLine($"{_logTag} [puzzle] 0x{st.PlayerEntityId:X4} {what}");

    private Task SendAsync(InGameSessionState player, Opcode opcode, byte[] payload)
    {
        byte randKey;
        lock (_rng) randKey = (byte)_rng.Next(1, 256);
        return player.SendFrameAsync(opcode, payload, randKey, default);
    }
}
