using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Drilling;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.Game.Warp;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net;
using tPoseidon.Core.Net.Dialogs;
using tPoseidon.Core.Net.World;

internal sealed class DrillDig
{
    public required ushort X { get; init; }
    public required ushort Y { get; init; }
    public DateTimeOffset BeginsAt { get; set; }
    public (ushort X, ushort Y)? WalkEnd { get; set; }
    public bool Begun { get; set; }
    public ushort Token { get; set; }
    public DateTimeOffset StartedAt { get; set; }
}

internal sealed class DrillController
{
    private readonly DrillTables _tables;
    private readonly IReadOnlyDictionary<ushort, ZoneGeometry> _geometry;
    private readonly IItemStore _items;
    private readonly IItemParamLookup _catalog;
    private readonly GroundItemField _groundItems;
    private readonly Func<ushort> _newEntityId;
    private readonly Func<ushort, Opcode, byte[], Task> _sendToMap;
    private readonly Func<InGameSessionState, Task> _disconnect;
    private readonly string _logTag;
    private readonly Random _rng = new();
    private readonly Dictionary<ushort, DrillField> _fields = new();
    private readonly Dictionary<ushort, DrillLoot?> _loot = new();
    private readonly object _lock = new();
    private readonly Func<TreasureMapTables> _treasureMaps;
    private readonly Func<int, ushort, ushort, ushort, Task<bool>> _spawnMonster;

    public DrillController(
        DrillTables tables,
        IReadOnlyDictionary<ushort, ZoneGeometry> geometry,
        IItemStore items,
        IItemParamLookup catalog,
        GroundItemField groundItems,
        Func<ushort> newEntityId,
        Func<ushort, Opcode, byte[], Task> sendToMap,
        Func<InGameSessionState, Task> disconnect,
        Func<TreasureMapTables> treasureMaps,
        Func<int, ushort, ushort, ushort, Task<bool>> spawnMonster,
        string logTag)
    {
        _spawnMonster = spawnMonster ?? throw new ArgumentNullException(nameof(spawnMonster));
        _treasureMaps = treasureMaps ?? throw new ArgumentNullException(nameof(treasureMaps));
        _tables = tables ?? throw new ArgumentNullException(nameof(tables));
        _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _groundItems = groundItems ?? throw new ArgumentNullException(nameof(groundItems));
        _newEntityId = newEntityId ?? throw new ArgumentNullException(nameof(newEntityId));
        _sendToMap = sendToMap ?? throw new ArgumentNullException(nameof(sendToMap));
        _disconnect = disconnect ?? throw new ArgumentNullException(nameof(disconnect));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public IReadOnlyDictionary<int, int> AutoDrillPets => _tables.AutoDrillPets;

    public async Task BeginAutoAsync(InGameSessionState digger)
    {
        digger.AutoDrill = true;
        Console.WriteLine($"{_logTag} [AUTODRILL] Begin 0x{digger.PlayerEntityId:X4}");
        await _sendToMap(digger.MapId, Opcode.AUTO_DRILL_BEGIN, AutoDrillFrames.Player(digger.PlayerEntityId)).ConfigureAwait(false);
    }

    public async Task EndAutoAsync(InGameSessionState digger)
    {
        digger.AutoDrill = false;
        Console.WriteLine($"{_logTag} [AUTODRILL] End 0x{digger.PlayerEntityId:X4}");
        await _sendToMap(digger.MapId, Opcode.AUTO_DRILL_END, AutoDrillFrames.Player(digger.PlayerEntityId)).ConfigureAwait(false);
    }

    public Task CoolTimeAsync(InGameSessionState digger)
    {
        digger.AutoDrillCoolTimeAt = DateTimeOffset.UtcNow;
        return SendAsync(digger, Opcode.S2C_AUTO_DRILL_COOLTIME, Array.Empty<byte>());
    }

    public async Task AbortAutoAsync(InGameSessionState digger, ushort error)
    {
        Console.WriteLine($"{_logTag} [AUTODRILL] 0x{digger.PlayerEntityId:X4} error {error}");
        if (!digger.AutoDrill) return;
        digger.AutoDrill = false;
        await SendAsync(digger, Opcode.S2C_AUTO_DRILL_REFUSED, AutoDrillFrames.Refused(error)).ConfigureAwait(false);
        await _sendToMap(digger.MapId, Opcode.AUTO_DRILL_END, AutoDrillFrames.Player(digger.PlayerEntityId)).ConfigureAwait(false);
    }

    public IReadOnlyList<(Opcode Opcode, byte[] Payload, bool ToDiggerOnly)> StopAutoFrames(InGameSessionState digger)
    {
        if (!digger.AutoDrill) return Array.Empty<(Opcode, byte[], bool)>();
        digger.AutoDrill = false;
        Console.WriteLine($"{_logTag} [AUTODRILL] 0x{digger.PlayerEntityId:X4} mode ended");
        return
        [
            (Opcode.S2C_AUTO_DRILL_STOPPED, Array.Empty<byte>(), true),
            (Opcode.AUTO_DRILL_END, AutoDrillFrames.Player(digger.PlayerEntityId), false),
        ];
    }

    public Item? WornDrill(InGameSessionState digger)
    {
        if (digger.Character is not { } character) return null;
        return _items.FindBySlot(character.CharacterId, SlotKind.Equipment, EquipmentSlot.Drill);
    }

    public DrillParamEntry? DrillOf(Item? drill) =>
        drill is not null && _tables.Drills.TryGetValue(drill.ItemIndex, out var entry) ? entry : null;

    public async Task BeginAsync(InGameSessionState digger)
    {
        if (digger.Dig is not { Begun: false } dig) return;
        var cell = (X: ZoneGeometry.CellOf(dig.X), Y: ZoneGeometry.CellOf(dig.Y));
        if (ZoneGeometry.CellOf(digger.PlayerX) != cell.X || ZoneGeometry.CellOf(digger.PlayerY) != cell.Y)
        {
            await FinishAsync(digger, DrillResult.Stopped, DrillFrames.CurrentLife, 0).ConfigureAwait(false);
            return;
        }
        dig.Begun = true;
        var drill = WornDrill(digger);
        if (drill is null)
        {
            await FinishAsync(digger, DrillResult.Stopped, DrillFrames.CurrentLife, 0).ConfigureAwait(false);
            return;
        }
        var field = FieldOf(digger.MapId);
        var nature = field?.NatureAt(cell.X, cell.Y) ?? 0;
        if (nature == DrillRules.NotDiggableNature)
        {
            await FinishAsync(digger, DrillResult.NotDiggable, DrillFrames.CurrentLife, 0).ConfigureAwait(false);
            return;
        }

        bool lucky;
        lock (_lock) lucky = _rng.Next(DrillRules.PercentRoll) < digger.PlayerDa + DrillRules.LuckBonus;
        if (digger.SurpriseSpot is { } diggingSpot && SurpriseSpotRules.IsOnSpot(diggingSpot, digger.MapId, digger.PlayerX, digger.PlayerY))
        {
            lucky = false;
            await SendAsync(digger, Opcode.S2C_SPOT_DIG, Array.Empty<byte>()).ConfigureAwait(false);
        }
        int? laid;
        lock (_lock) laid = field?.LaidDepth(cell.X, cell.Y);
        if (lucky && laid is null && digger.LastDigCell != cell && DrillOf(drill) is not null
            && _tables.Zones.TryGetValue(digger.MapId, out var zone) && digger.GainExpTotal is { } gainTotal)
        {
            await SendAllAsync(digger, gainTotal(DrillRules.StartExp(zone, digger.PlayerDa))).ConfigureAwait(false);
        }

        var invincible = digger.Skills.ContainsKey(DrillRules.InvincibleDrillSkill);
        lock (_lock) dig.Token = (ushort)(_rng.Next(ushort.MaxValue + 1) & DrillRules.TokenMask);
        dig.StartedAt = DateTimeOffset.UtcNow;
        digger.LastDigCell = cell;
        var spot = new DrillSpot(digger.PlayerX, digger.PlayerY, field?.MapDepth ?? 0, laid ?? 0, nature);
        await SendAsync(digger, Opcode.S2C_DRILL_SPOT, DrillFrames.Spot(digger.PlayerEntityId, spot, dig.Token, lucky, 0)).ConfigureAwait(false);
        await _sendToMap(digger.MapId, Opcode.S2C_DRILL_SEEN, DrillFrames.SeenStart(digger.PlayerEntityId, spot, invincible)).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} [drill] 0x{digger.PlayerEntityId:X4} digs at ({digger.PlayerX},{digger.PlayerY}) map {digger.MapId}: map depth {spot.MapDepth}, item depth {spot.LaidDepth}, nature {nature}, lucky {lucky}");
    }

    public async Task CancelAsync(InGameSessionState digger, ReadOnlyMemory<byte> payload)
    {
        if (digger.Dig is null) return;
        var drill = WornDrill(digger);
        if (drill is null)
        {
            await FinishAsync(digger, DrillResult.Stopped, 0, 0).ConfigureAwait(false);
            return;
        }
        var life = WearDrill(digger, drill, DrillFrames.CancelPresses(payload.Span));
        await FinishAsync(digger, DrillResult.Stopped, life, 0).ConfigureAwait(false);
    }

    public async Task EndAsync(InGameSessionState digger, ReadOnlyMemory<byte> payload)
    {
        if (digger.Dig is not { } dig) return;
        if (!DrillFrames.TryReadEnd(payload.Span, out var presses, out var depth, out var check))
        {
            Console.WriteLine($"{_logTag} [drill] 0x{digger.PlayerEntityId:X4} end: {payload.Length} bytes is not a dig result");
            await FinishAsync(digger, DrillResult.Nothing, DrillFrames.CurrentLife, 0).ConfigureAwait(false);
            return;
        }
        var drill = WornDrill(digger);
        if (drill is null || DrillOf(drill) is not { } parameters)
        {
            await FinishAsync(digger, DrillResult.Nothing, 0, 0).ConfigureAwait(false);
            return;
        }
        var field = FieldOf(digger.MapId);
        var mapDepth = field?.MapDepth ?? 0;
        if (DrillRules.Check(check, parameters, depth, presses) != dig.Token || !DrillRules.InRange(parameters, depth, presses, mapDepth))
        {
            Console.WriteLine($"{_logTag} [drill] 0x{digger.PlayerEntityId:X4} sent an invalid dig result (depth {depth}, presses {presses}, check 0x{check:X4}); disconnecting");
            digger.Dig = null;
            await _disconnect(digger).ConfigureAwait(false);
            return;
        }
        dig.Token = 0;
        if (drill.Stack <= 0)
        {
            await FinishAsync(digger, DrillResult.WornOut, 0, 0).ConfigureAwait(false);
            return;
        }
        var cell = (X: ZoneGeometry.CellOf(dig.X), Y: ZoneGeometry.CellOf(dig.Y));
        var life = WearDrill(digger, drill, presses);
        if (await RandomSpotAsync(digger, parameters).ConfigureAwait(false))
        {
            await SendAsync(digger, Opcode.S2C_SPOT_DIG, Array.Empty<byte>()).ConfigureAwait(false);
            await FinishAsync(digger, DrillResult.Spot, life, 0).ConfigureAwait(false);
            return;
        }
        if (digger.SurpriseSpot is { IsTreasure: true } treasureSpot && SurpriseSpotRules.IsOnSpot(treasureSpot, digger.MapId, digger.PlayerX, digger.PlayerY))
        {
            digger.SurpriseSpot = null;
            if (_treasureMaps().Items.TryGetValue(treasureSpot.ItemId, out var treasureRow))
            {
                await PopTreasureAsync(digger, treasureRow, Opcode.S2C_SPOT_TREASURE, limitCount: false).ConfigureAwait(false);
            }
            await FinishAsync(digger, DrillResult.Spot, life, 0).ConfigureAwait(false);
            return;
        }
        if (digger.SurpriseSpot is { CreviceFound: false, IsTreasure: false } drilledSpot && SurpriseSpotRules.IsOnSpot(drilledSpot, digger.MapId, digger.PlayerX, digger.PlayerY))
        {
            drilledSpot.CreviceFound = true;
            await _sendToMap(digger.MapId, Opcode.SPOT_CREVICE, SpotFrame(digger.PlayerEntityId, SurpriseSpotRules.CreviceOpened)).ConfigureAwait(false);
            Console.WriteLine($"{_logTag} [spot] 0x{digger.PlayerEntityId:X4} found the crevice of treasure map {drilledSpot.ItemId}");
            await FinishAsync(digger, DrillResult.Spot, life, 0).ConfigureAwait(false);
            return;
        }

        int? laid;
        var rolled = 0;
        lock (_lock)
        {
            laid = field?.LaidDepth(cell.X, cell.Y);
            if (laid is not null && LootOf(digger.MapId) is { } loot) rolled = loot.Pop(_ => false, _rng);
        }
        var result = DrillRules.EndResult(laid, rolled, depth, mapDepth);
        var givesExp = DrillRules.GivesEndExp(laid, rolled, depth);
        if (result != DrillResult.Found)
        {
            await FinishAsync(digger, result, life, 0).ConfigureAwait(false);
            if (givesExp) await GrantEndExpAsync(digger, parameters, depth).ConfigureAwait(false);
            return;
        }

        var elapsed = (DateTimeOffset.UtcNow - dig.StartedAt).TotalMilliseconds;
        var minimum = DrillRules.MinimumMilliseconds(parameters, DrillRules.ItemDepth(laid!.Value), mapDepth);
        var tooFast = elapsed < minimum;
        if (tooFast)
        {
            Console.WriteLine($"{_logTag} [drill] 0x{digger.PlayerEntityId:X4} dug faster than possible: {elapsed:F0} ms of {minimum} ms");
        }

        var count = _tables.Drills.TryGetValue(rolled, out var dugDrill) ? dugDrill.DrillLife : 1;
        var placed = MonsterDropBatch.Items(
            new DropSource(digger.PlayerEntityId, 0, digger.MapId, digger.PlayerX, digger.PlayerY, digger.PlayerEntityId, DateTimeOffset.UtcNow),
            new[] { new RolledDrop(rolled, count) },
            _catalog,
            _newEntityId,
            _geometry.TryGetValue(digger.MapId, out var geometry) ? geometry : null,
            _rng);
        foreach (var item in placed)
        {
            _groundItems.Add(item);
            await _sendToMap(digger.MapId, Opcode.S2C_ITEM_DUG_UP, GroundItemFrames.DugUp(item, digger.PlayerX, digger.PlayerY)).ConfigureAwait(false);
        }
        await FinishAsync(digger, DrillResult.Found, life, rolled).ConfigureAwait(false);
        if (digger.Stats is { } stats && stats.TmLevel < DrillRules.TmLevelCap && digger.GainSkillTmExp is { } gainTm)
        {
            await SendAllAsync(digger, gainTm(AutoDrillRules.FindTmExp(DrillRules.FindTmExp(stats.Level, depth, parameters.Grade), digger.AutoDrill))).ConfigureAwait(false);
        }
        lock (_lock) field!.Take(cell.X, cell.Y, _rng);
        await GrantEndExpAsync(digger, parameters, depth).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} [drill] 0x{digger.PlayerEntityId:X4} dug up item {rolled} x{count} at depth {depth}; drill life {life}");
        if (tooFast) await _disconnect(digger).ConfigureAwait(false);
    }

    public async Task InterruptAsync(InGameSessionState digger)
    {
        if (digger.Dig is null) return;
        if (digger.Skills.ContainsKey(DrillRules.InvincibleDrillSkill)) return;
        await FinishAsync(digger, DrillResult.Interrupted, DrillFrames.CurrentLife, 0).ConfigureAwait(false);
        foreach (var (opcode, payload, toDiggerOnly) in StopAutoFrames(digger))
        {
            if (toDiggerOnly) await SendAsync(digger, opcode, payload).ConfigureAwait(false);
            else await _sendToMap(digger.MapId, opcode, payload).ConfigureAwait(false);
        }
    }

    private int WearDrill(InGameSessionState digger, Item drill, int presses)
    {
        var parameters = DrillOf(drill);
        var nature = 0;
        if (digger.Dig is { } dig && FieldOf(digger.MapId) is { } field)
        {
            lock (_lock) nature = field.NatureAt(ZoneGeometry.CellOf(dig.X), ZoneGeometry.CellOf(dig.Y));
        }
        var life = DrillRules.Wear(drill.Stack, presses, parameters?.GroundNature ?? 0, nature);
        if (life != drill.Stack) _items.SetStack(drill.ItemUid, (ushort)Math.Clamp(life, 0, ushort.MaxValue));
        return life;
    }

    private async Task GrantEndExpAsync(InGameSessionState digger, DrillParamEntry parameters, int depth)
    {
        if (digger.Stats is not { } stats || digger.GainHitExp is not { } gainExp) return;
        foreach (var (opcode, payload, audience) in gainExp(DrillRules.EndExp(_tables.GradeExpRates, parameters.Grade, depth, stats.Level)))
        {
            if (audience == SwingAudience.Everyone) await _sendToMap(digger.MapId, opcode, payload).ConfigureAwait(false);
            else await SendAsync(digger, opcode, payload).ConfigureAwait(false);
        }
    }

    public async Task<bool> CreateTeleportSpotAsync(InGameSessionState owner, ItemParamEntry treasureMap)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(treasureMap);
        if (!_treasureMaps().Teleports.TryGetValue(treasureMap.Id, out var row) || row.TimeMinutes <= 0 || row.SpotMaps.Count == 0)
        {
            Console.WriteLine($"{_logTag} [spot] treasure map {treasureMap.Id}: no usable {TreasureMapTables.TeleportTable} row");
            await SendAsync(owner, Opcode.S2C_SPOT_CREATED, new[] { SurpriseSpotRules.Failed }).ConfigureAwait(false);
            return false;
        }
        return await PlaceSpotAsync(owner, treasureMap.Id, row.TimeMinutes, row.SpotMaps, treasure: false).ConfigureAwait(false);
    }

    public async Task<bool> CreateTreasureSpotAsync(InGameSessionState owner, ItemParamEntry treasureMap)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(treasureMap);
        if (!_treasureMaps().Items.TryGetValue(treasureMap.Id, out var row) || row.TimeMinutes <= 0 || row.SpotMaps.Count == 0)
        {
            Console.WriteLine($"{_logTag} [spot] treasure map {treasureMap.Id}: no usable {TreasureMapTables.ItemTable} row");
            await SendAsync(owner, Opcode.S2C_SPOT_CREATED, new[] { SurpriseSpotRules.Failed }).ConfigureAwait(false);
            return false;
        }
        return await PlaceSpotAsync(owner, treasureMap.Id, row.TimeMinutes, row.SpotMaps, treasure: true).ConfigureAwait(false);
    }

    private async Task<bool> PlaceSpotAsync(InGameSessionState owner, int itemId, int timeMinutes, IReadOnlyList<int> spotMaps, bool treasure)
    {
        ushort zone;
        ZonePoint? point = null;
        lock (_lock)
        {
            zone = (ushort)spotMaps[_rng.Next(spotMaps.Count)];
            if (_geometry.TryGetValue(zone, out var geometry))
            {
                var field = FieldOf(zone);
                var candidates = SurpriseSpotRules.MonsterPoints(geometry)
                    .Where(candidate => field is null || field.IsDiggable(ZoneGeometry.CellOf(candidate.X), ZoneGeometry.CellOf(candidate.Y)))
                    .ToList();
                if (candidates.Count > 0) point = candidates[_rng.Next(candidates.Count)];
            }
        }
        if (point is not { } spotPoint)
        {
            Console.WriteLine($"{_logTag} [spot] treasure map {itemId}: Failed Monster Point on map {zone}");
            await SendAsync(owner, Opcode.S2C_SPOT_CREATED, new[] { SurpriseSpotRules.Failed }).ConfigureAwait(false);
            return false;
        }
        var spot = new SurpriseSpot(itemId, zone, (short)spotPoint.X, (short)spotPoint.Y, DateTimeOffset.UtcNow + TimeSpan.FromMinutes(timeMinutes), treasure);
        owner.SurpriseSpot = spot;
        var w = new PacketWriter(11);
        w.WriteByte(SurpriseSpotRules.Created);
        w.WriteUInt16(spot.Zone);
        w.WriteInt16(spot.X);
        w.WriteInt16(spot.Y);
        w.WriteUInt32((uint)(timeMinutes * 60000));
        await SendAsync(owner, Opcode.S2C_SPOT_CREATED, w.ToArray()).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} [spot] 0x{owner.PlayerEntityId:X4} treasure map {itemId}: {(treasure ? "treasure" : "crevice")} spot on map {zone} ({spot.X},{spot.Y}) for {timeMinutes} min");
        return true;
    }

    public async Task CreviceAnswerAsync(InGameSessionState owner, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (payload.Length < 2 || owner.SurpriseSpot is not { } spot || SurpriseSpotRules.HasExpired(spot, DateTimeOffset.UtcNow)) return;
        owner.SurpriseSpot = null;
        if (payload.Span[1] == 0)
        {
            await _sendToMap(owner.MapId, Opcode.S2C_SPOT_TELEPORT_RESULT, SpotFrame(owner.PlayerEntityId, SurpriseSpotRules.CreviceClosedAsDeclined)).ConfigureAwait(false);
            return;
        }
        TreasureTeleportDestination? destination = null;
        if (_treasureMaps().Teleports.TryGetValue(spot.ItemId, out var row))
        {
            lock (_lock) destination = SurpriseSpotRules.PickDestination(row, _rng.NextDouble);
        }
        if (destination is null || owner.RequestZoneChange is not { } requestZoneChange)
        {
            Console.WriteLine($"{_logTag} [spot] 0x{owner.PlayerEntityId:X4} crevice of treasure map {spot.ItemId} has no destination");
            return;
        }
        owner.ProtectedUntil = DateTimeOffset.UtcNow + SurpriseSpotRules.TeleportProtection;
        await _sendToMap(owner.MapId, Opcode.S2C_SPOT_TELEPORT_RESULT, SpotFrame(owner.PlayerEntityId, SurpriseSpotRules.CreviceUsed)).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} [spot] 0x{owner.PlayerEntityId:X4} goes through the crevice to map {destination.ZoneId} point {destination.PointId}");
        await requestZoneChange(new ZoneChangeRequest(
            (ushort)destination.ZoneId, TeleportArrival.ChangeZoneType, destination.PointId == 0 ? null : destination.PointId, TimeSpan.Zero), default).ConfigureAwait(false);
    }

    public async Task TickSpotAsync(InGameSessionState owner, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (owner.SurpriseSpot is not { } spot || !SurpriseSpotRules.HasExpired(spot, now)) return;
        owner.SurpriseSpot = null;
        var w = new PacketWriter(2);
        w.WriteUInt16(owner.PlayerEntityId);
        await SendAsync(owner, Opcode.S2C_SPOT_LIFE_OVER, w.ToArray()).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} [spot] 0x{owner.PlayerEntityId:X4} treasure map {spot.ItemId} spot ran out");
    }

    private async Task<bool> RandomSpotAsync(InGameSessionState digger, DrillParamEntry drill)
    {
        if (digger.AutoDrill) return false;
        var tables = _treasureMaps();
        if (RandomSpot.RowFor(tables, digger.MapId) is not { } row) return false;
        RandomSpotRoll? roll;
        lock (_lock)
        {
            if (!RandomSpot.Triggers(RandomSpot.Chance(drill.RandomSpotRatio, tables.RandomSpotAddedRatio), _rng.Next(RandomSpot.RandMax + 1))) return false;
            roll = RandomSpot.Choose(row, _rng.Next(RandomSpot.RandMax + 1));
        }
        if (roll is not { } chosen)
        {
            Console.WriteLine($"{_logTag} [spot] 0x{digger.PlayerEntityId:X4} random spot of region {row.RegionId}: Failed Spawning Monster by ratio error");
            return false;
        }
        Console.WriteLine($"{_logTag} [spot] 0x{digger.PlayerEntityId:X4} hit a random spot of region {row.RegionId} at ({digger.PlayerX},{digger.PlayerY}) map {digger.MapId}: {chosen.Kind} {chosen.Id}");
        if (chosen.Kind == RandomSpotKind.Monster)
        {
            if (!await _spawnMonster(chosen.Id, digger.MapId, digger.PlayerX, digger.PlayerY).ConfigureAwait(false))
            {
                Console.WriteLine($"{_logTag} [spot] random spot monster {chosen.Id} not spawned on map {digger.MapId}");
            }
            return true;
        }
        if (tables.Items.TryGetValue(chosen.Id, out var treasure))
        {
            await PopTreasureAsync(digger, treasure, Opcode.S2C_RANDOM_SPOT_TREASURE, limitCount: true).ConfigureAwait(false);
        }
        else
        {
            Console.WriteLine($"{_logTag} [spot] random spot treasure {chosen.Id} has no {TreasureMapTables.ItemTable} row");
        }
        return true;
    }

    private async Task PopTreasureAsync(InGameSessionState owner, TreasureItemEntry row, Opcode opcode, bool limitCount)
    {
        if (owner.Character is not { } character) return;
        var seconds = (DateTimeOffset.UtcNow - character.GenTime).TotalSeconds;
        var today = TreasureLoot.TodayValue((int)character.Species, seconds, TreasureLoot.DefaultMultiplier);
        var lootCount = TreasureLoot.Count(row, today);
        if (limitCount && !RandomSpot.CountAllowed(lootCount))
        {
            Console.WriteLine($"{_logTag} [spot] treasure {row.ItemId}: count {lootCount} refused");
            return;
        }
        IReadOnlyList<(int ItemId, int Count)> picked;
        lock (_lock) picked = TreasureLoot.Pick(row, lootCount, _rng.NextDouble, () => _rng.Next(TreasureLoot.RandRange));
        var dropped = new List<FieldItem>(picked.Count);
        var index = 0;
        foreach (var (itemId, count) in picked)
        {
            var (x, y) = TreasureLoot.RingPosition(owner.PlayerX, owner.PlayerY, index++, picked.Count);
            var item = new FieldItem(_newEntityId(), owner.MapId, x, y, itemId, count,
                _catalog.GetById(itemId)?.ObjectClass ?? (byte)ItemParamEntry.StackableClass, false,
                owner.PlayerEntityId, owner.PlayerEntityId, DateTimeOffset.UtcNow);
            _groundItems.Add(item);
            dropped.Add(item);
        }
        var w = new PacketWriter(4 + dropped.Count * (GroundItemFrames.AlreadyThereSize + GroundItemFrames.RareItemExtraBytes));
        w.WriteUInt16(owner.PlayerEntityId);
        w.WriteUInt16((ushort)dropped.Count);
        foreach (var item in dropped) w.WriteBytes(GroundItemFrames.AlreadyThere(item));
        await _sendToMap(owner.MapId, opcode, w.ToArray()).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} [spot] 0x{owner.PlayerEntityId:X4} dug treasure {row.ItemId}: {dropped.Count} items (today value {today})");
    }

    private static byte[] SpotFrame(ushort ownerId, byte flag)
    {
        var w = new PacketWriter(3);
        w.WriteUInt16(ownerId);
        w.WriteByte(flag);
        return w.ToArray();
    }

    private async Task FinishAsync(InGameSessionState digger, byte result, int life, int itemId)
    {
        digger.Dig = null;
        var reported = life == DrillFrames.CurrentLife ? WornDrill(digger)?.Stack ?? 0 : life;
        await SendAsync(digger, Opcode.S2C_DRILL_RESULT, DrillFrames.Result(result, reported, itemId)).ConfigureAwait(false);
        await _sendToMap(digger.MapId, Opcode.S2C_DRILL_SEEN_RESULT, DrillFrames.SeenResult(digger.PlayerEntityId, result)).ConfigureAwait(false);
    }

    private DrillField? FieldOf(ushort mapId)
    {
        lock (_lock)
        {
            if (_fields.TryGetValue(mapId, out var field)) return field;
            if (!_geometry.TryGetValue(mapId, out var geometry) || !_tables.Zones.TryGetValue(mapId, out var zone)) return null;
            field = new DrillField(geometry, zone, _tables.GroundNatures, _rng);
            _fields[mapId] = field;
            Console.WriteLine($"{_logTag} [drill] map {mapId}: {field.LaidCount} items laid on {field.DiggableCount} diggable tiles ({zone.ItemPercentage}%, depth {zone.MinItemDepth}..{zone.MaxItemDepth}), ground types {(geometry.HasGroundTypes ? "loaded" : "missing")}");
            return field;
        }
    }

    private DrillLoot? LootOf(ushort mapId)
    {
        if (_loot.TryGetValue(mapId, out var loot)) return loot;
        loot = _tables.ZoneItems.TryGetValue(mapId, out var row) ? new DrillLoot(row, _tables.ItemTables, _tables.EventRateAdd) : null;
        _loot[mapId] = loot;
        return loot;
    }

    private async Task SendAllAsync(InGameSessionState digger, IEnumerable<(Opcode Opcode, byte[] Payload, SwingAudience Audience)> frames)
    {
        foreach (var (opcode, payload, audience) in frames)
        {
            if (audience == SwingAudience.Everyone) await _sendToMap(digger.MapId, opcode, payload).ConfigureAwait(false);
            else await SendAsync(digger, opcode, payload).ConfigureAwait(false);
        }
    }

    private async Task SendAllAsync(InGameSessionState digger, IEnumerable<(Opcode Opcode, byte[] Payload)> frames)
    {
        foreach (var (opcode, payload) in frames) await SendAsync(digger, opcode, payload).ConfigureAwait(false);
    }

    private Task SendAsync(InGameSessionState digger, Opcode opcode, byte[] payload)
    {
        byte randKey;
        lock (_lock) randKey = (byte)_rng.Next(1, 256);
        return digger.SendFrameAsync(opcode, payload, randKey, default);
    }
}
