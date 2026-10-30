using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.Game.ShadowWorld;
using tPoseidon.Core.Game.Warp;
using tPoseidon.Core.InterServer;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.Dialogs;
using tPoseidon.Core.Net.World;

internal sealed record ShadowEventPlan(ShadowTimerEntry Timer, ushort MapId)
{
    public static IReadOnlyList<ShadowEventPlan> Build(
        IEnumerable<ShadowTimerEntry> timers, IEnumerable<NpcTalkEntry> talks, IReadOnlyDictionary<int, TeleportEntry> teleports)
    {
        var plans = new List<ShadowEventPlan>();
        var talkList = talks.ToArray();
        foreach (var timer in timers)
        {
            var talk = talkList.FirstOrDefault(entry => entry.TimerId == timer.TimerId);
            if (talk is null || !teleports.TryGetValue(talk.TeleportId, out var teleport) || teleport.Destinations.Count == 0) continue;
            var zone = teleport.Destinations[0].Zone;
            if (zone is > 0 and <= ushort.MaxValue) plans.Add(new ShadowEventPlan(timer, (ushort)zone));
        }
        return plans;
    }
}

internal static class ShadowSanctuaryMaps
{
    private static IReadOnlySet<ushort> _maps = new HashSet<ushort>();

    public static void Register(IEnumerable<ushort> maps)
    {
        ArgumentNullException.ThrowIfNull(maps);
        Volatile.Write(ref _maps, maps.ToHashSet());
    }

    public static bool Contains(ushort mapId) => Volatile.Read(ref _maps).Contains(mapId);
}

internal sealed class ShadowEventController : IDisposable
{
    public const long FrameMilliseconds = 100;
    public const ushort DefaultSpawnX = 600;
    public const ushort DefaultSpawnY = 400;
    public const int BossMonsterType = 7;
    public const int UncappedMonsterType = 10;
    public const ushort SharedScene = 0;

    private sealed class Zone
    {
        public required ShadowEventPlan Plan { get; init; }
        public required ShadowWorldMission Mission { get; init; }
        public required string Scenario { get; init; }
        public required DateTimeOffset StartedAt { get; init; }
        public MonsterScenarioRun Runner { get; set; } = null!;
        public DateTimeOffset LastTick { get; set; }
        public long RunnerTickedUntil { get; set; }
        public HashSet<ushort> Greeted { get; } = new();
        public List<(Opcode Opcode, byte[] Payload)> Outbox { get; } = new();
    }

    private sealed class Host : IScenarioHost
    {
        private readonly ShadowEventController _owner;
        private readonly Zone _zone;

        public Host(ShadowEventController owner, Zone zone)
        {
            _owner = owner;
            _zone = zone;
        }

        public int AliveMonsters => _owner.OnMap(_zone).Count(monster => monster.MonsterParamId != ShadowStatues.MonsterId);

        public int AliveOf(int monsterId) => _owner.OnMap(_zone).Count(monster => monster.MonsterParamId == monsterId);

        public int AliveWithInstanceKey(int instanceKey) =>
            _owner.OnMap(_zone).Count(monster => monster.InstanceKey == instanceKey && monster.MonsterParamId != ShadowStatues.MonsterId);

        public int ZonePlayers => _owner._sessions().Count(session => session.MapId == _zone.Plan.MapId && session.ClientReady);

        public int MonsterTypeOf(int monsterId) => _owner.TypeOf(monsterId);

        public void Spawn(int monsterId, int regionId) => _owner.SpawnAt(_zone, monsterId, regionId);

        public int SpawnCaster(int monsterId, int regionId) => _owner.SpawnAt(_zone, monsterId, regionId);

        public void CastAllMapSkill(int casterEntityId, ScenarioStep scenarioStep) =>
            _zone.Outbox.Add((Opcode.S2C_SCENARIO_ALL_MAP_SKILL,
                ScenarioFrames.AllMapSkill((uint)scenarioStep.MonsterId, (uint)scenarioStep.AllMapSkillId, scenarioStep.SkillComment, _owner._text)));

        public void Announce(string comment) =>
            _zone.Outbox.Add((Opcode.S2C_SCENARIO_NOTICE, ScenarioFrames.Notice(comment, _owner._text)));
    }

    private readonly Func<IReadOnlyList<ShadowEventPlan>> _plans;
    private readonly Func<ScenarioTables> _scenarios;
    private readonly Func<ShadowEventTables> _tables;
    private readonly MonsterManager _monsters;
    private readonly IReadOnlyDictionary<ushort, ZoneGeometry> _geometry;
    private readonly Func<IReadOnlyCollection<InGameSessionState>> _sessions;
    private readonly Func<MonsterEntity, byte[]> _spawnFrame;
    private readonly Func<ushort, byte[]> _despawnFrame;
    private readonly IReadOnlyDictionary<int, MapKindEntry> _maps;
    private readonly Action<MonsterEntity> _refreshStats;
    private readonly ShadowRegionReduction _reduction = new();
    private bool _scaled;
    private readonly Func<int, bool> _isShadowUnit;
    private readonly Func<ushort, ushort?> _battleTargetOf;
    private readonly Func<int, bool, Task> _setTimerNpc;
    private readonly Func<InGameSessionState, ShadowBonus, Task> _grant;
    private readonly IShadowBonusStore _bonusStore;
    private readonly Func<ShadowAreaReward, Task<bool>> _announceArea;
    private readonly Encoding _text;
    private readonly string _logTag;
    private readonly Dictionary<int, bool> _open = new();
    private readonly Dictionary<ushort, Zone> _zones = new();
    private readonly object _killGate = new();
    private readonly List<(MonsterEntity Monster, ushort Killer)> _kills = new();
    private readonly Random _rng = new();

    public ShadowEventController(
        Func<IReadOnlyList<ShadowEventPlan>> plans, Func<ScenarioTables> scenarios, Func<ShadowEventTables> tables,
        MonsterManager monsters, IReadOnlyDictionary<ushort, ZoneGeometry> geometry,
        Func<IReadOnlyCollection<InGameSessionState>> sessions, Func<MonsterEntity, byte[]> spawnFrame, Func<ushort, byte[]> despawnFrame,
        IReadOnlyDictionary<int, MapKindEntry> maps, Action<MonsterEntity> refreshStats, Func<int, bool> isShadowUnit, Func<ushort, ushort?> battleTargetOf, Func<int, bool, Task> setTimerNpc, Func<InGameSessionState, ShadowBonus, Task> grant, IShadowBonusStore bonusStore, Func<ShadowAreaReward, Task<bool>> announceArea,
        Encoding text, string logTag)
    {
        _plans = plans ?? throw new ArgumentNullException(nameof(plans));
        _scenarios = scenarios ?? throw new ArgumentNullException(nameof(scenarios));
        _tables = tables ?? throw new ArgumentNullException(nameof(tables));
        _monsters = monsters ?? throw new ArgumentNullException(nameof(monsters));
        _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _spawnFrame = spawnFrame ?? throw new ArgumentNullException(nameof(spawnFrame));
        _despawnFrame = despawnFrame ?? throw new ArgumentNullException(nameof(despawnFrame));
        _maps = maps ?? throw new ArgumentNullException(nameof(maps));
        _refreshStats = refreshStats ?? throw new ArgumentNullException(nameof(refreshStats));
        _isShadowUnit = isShadowUnit ?? throw new ArgumentNullException(nameof(isShadowUnit));
        _battleTargetOf = battleTargetOf ?? throw new ArgumentNullException(nameof(battleTargetOf));
        _setTimerNpc = setTimerNpc ?? throw new ArgumentNullException(nameof(setTimerNpc));
        _grant = grant ?? throw new ArgumentNullException(nameof(grant));
        _bonusStore = bonusStore ?? throw new ArgumentNullException(nameof(bonusStore));
        _announceArea = announceArea ?? throw new ArgumentNullException(nameof(announceArea));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
        _monsters.MonsterKilled += OnMonsterKilled;
    }

    public void Dispose() => _monsters.MonsterKilled -= OnMonsterKilled;

    public static bool OwnsMap(ushort mapId) => ShadowSanctuaryMaps.Contains(mapId);

    public bool IsBesieging(MonsterEntity monster) => _zones.ContainsKey(monster.MapId) && IsSiegeUnit(monster);

    public async Task TickAsync(DateTimeOffset now)
    {
        var sessions = _sessions().ToArray();
        foreach (var plan in _plans())
        {
            var open = plan.Timer.IsOpen(now.LocalDateTime);
            var known = _open.TryGetValue(plan.Timer.TimerId, out var was);
            _open[plan.Timer.TimerId] = open;
            if (known && was == open) continue;
            await _setTimerNpc(plan.Timer.TimerId, open).ConfigureAwait(false);
            if (open && !_zones.ContainsKey(plan.MapId)) await ActivateAsync(plan, now, sessions).ConfigureAwait(false);
            else if (!open && known) await ExpireAsync(plan, sessions).ConfigureAwait(false);
        }
        foreach (var zone in _zones.Values.ToArray()) await TickZoneAsync(zone, now, sessions).ConfigureAwait(false);
        ApplyReductions(now);
    }

    public void ApplyAreaReward(ShadowAreaReward reward)
    {
        ArgumentNullException.ThrowIfNull(reward);
        if (_tables().RewardById(reward.RewardId) is not { } area || !_reduction.Apply(area, reward.Region, reward.ExpiresAt)) return;
        Console.WriteLine($"{_logTag} [shadow] region {reward.Region}: monster stats at {ShadowRegionReduction.FullPercent - area.MonsterRespawnDecrease} percent until {reward.ExpiresAt:u}");
    }

    private void ApplyReductions(DateTimeOffset now)
    {
        if (!_scaled && !_reduction.AnyActive(now)) return;
        var scaled = false;
        foreach (var monster in _monsters.All)
        {
            var percent = _maps.TryGetValue(monster.MapId, out var map)
                ? _reduction.PercentFor(map.WorldMapRegion, map.Class, now)
                : ShadowRegionReduction.FullPercent;
            scaled |= percent != ShadowRegionReduction.FullPercent;
            if (monster.StatScalePercent == percent) continue;
            monster.StatScalePercent = percent;
            _refreshStats(monster);
        }

        _scaled = scaled;
    }

    private async Task ActivateAsync(ShadowEventPlan plan, DateTimeOffset now, InGameSessionState[] sessions)
    {
        var scenarios = _scenarios();
        if (scenarios.TableOf(plan.MapId, _rng.Next()) is not { } table || !scenarios.Steps.TryGetValue(table, out var steps))
        {
            Console.WriteLine($"{_logTag} [shadow] map {plan.MapId}: no scenario table, timer {plan.Timer.TimerId} not started");
            return;
        }
        var mission = new ShadowWorldMission();
        mission.Activate();
        var zone = new Zone { Plan = plan, Mission = mission, Scenario = table, StartedAt = now, LastTick = now };
        zone.Runner = new MonsterScenarioRun(steps, new Host(this, zone));
        _zones[plan.MapId] = zone;
        ClearMap(zone);
        zone.Outbox.Add((Opcode.S2C_SHADOW_WORLD_PHASE, ShadowWorldFrames.Phase(ShadowWorldFrames.ActivePhase)));
        for (var region = ShadowStatues.FirstStatueRegion; region < ShadowStatues.FirstStatueRegion + ShadowStatues.StatueCount; region++)
        {
            SpawnAt(zone, ShadowStatues.MonsterId, region);
        }
        Console.WriteLine($"{_logTag} [shadow] map {plan.MapId}: timer {plan.Timer.TimerId} opened on {table}");
        await FlushAsync(zone, sessions).ConfigureAwait(false);
    }

    private async Task ExpireAsync(ShadowEventPlan plan, InGameSessionState[] sessions)
    {
        if (!_zones.TryGetValue(plan.MapId, out var zone) || !zone.Mission.Active || zone.Mission.Finished) return;
        await FinishAsync(zone, true, sessions).ConfigureAwait(false);
    }

    private async Task TickZoneAsync(Zone zone, DateTimeOffset now, InGameSessionState[] sessions)
    {
        var elapsed = (int)Math.Clamp((now - zone.LastTick).TotalMilliseconds, 0, int.MaxValue);
        zone.LastTick = now;
        var present = sessions.Where(session => session.MapId == zone.Plan.MapId && session.ClientReady && session.Character is not null).ToArray();
        foreach (var kill in TakeKills().Where(kill => kill.Monster.MapId == zone.Plan.MapId)) Handle(zone, kill.Monster, kill.Killer, present);
        var mission = zone.Mission;
        if (mission.Finished)
        {
            await TickLingerAsync(zone, elapsed, sessions, present).ConfigureAwait(false);
            return;
        }
        foreach (var arrival in present.Where(session => zone.Greeted.Add(session.PlayerEntityId)))
        {
            await SendAsync(arrival, Opcode.S2C_SHADOW_WORLD_STATUE_BOARD,
                ShadowWorldFrames.StatueBoard(mission.RemainingMilliseconds, GaugesOf(zone, present, now))).ConfigureAwait(false);
        }
        var clock = (long)(now - zone.StartedAt).TotalMilliseconds;
        for (var frame = zone.RunnerTickedUntil + FrameMilliseconds; frame <= clock; frame += FrameMilliseconds)
        {
            zone.Runner.Tick(frame);
            zone.RunnerTickedUntil = frame;
        }
        if (mission.Tick(elapsed))
        {
            zone.Outbox.Add((Opcode.S2C_SHADOW_WORLD_LIVE_BOARD,
                ShadowWorldFrames.LiveBoard(mission.RemainingMilliseconds, GaugesOf(zone, present, now), mission.Standings, null, _text)));
        }
        await FlushAsync(zone, sessions).ConfigureAwait(false);
        if (mission.ResultPending) await FinishAsync(zone, mission.PendingSuccess, sessions).ConfigureAwait(false);
    }

    private async Task FinishAsync(Zone zone, bool success, InGameSessionState[] sessions)
    {
        var present = sessions.Where(session => session.MapId == zone.Plan.MapId && session.Character is not null).ToArray();
        var participants = present.Select(ParticipantOf).ToArray();
        var result = zone.Mission.Finish(success, participants);
        zone.Outbox.Add((Opcode.S2C_SHADOW_WORLD_RESULT, ShadowWorldFrames.Result(result, _text)));
        zone.Outbox.Add((Opcode.S2C_SHADOW_WORLD_PHASE, ShadowWorldFrames.Phase(ShadowWorldFrames.EndedPhase)));
        await FlushAsync(zone, sessions).ConfigureAwait(false);
        var awards = ShadowWorldMission.Awards(result, participants, _tables(), zone.Scenario, DateTimeOffset.UtcNow);
        foreach (var award in awards)
        {
            _bonusStore.Save(new ShadowBonusRecord(award.CharacterId, award.Reward.Id, award.Expires));
            if (present.FirstOrDefault(session => session.Character!.CharacterId == award.CharacterId) is { } recipient)
            {
                await _grant(recipient, ShadowBonus.Of(award.Reward, award.Expires)).ConfigureAwait(false);
            }
        }
        if (success && _tables().Reward(zone.Scenario, ShadowWorldMission.AreaRank) is { MonsterRespawnDecrease: not 0 } area)
        {
            var reward = new ShadowAreaReward(area.Group, zone.Plan.Timer.TimerId, zone.Plan.Timer.WorldMapRegion, area.Id, DateTimeOffset.UtcNow + ShadowWorldMission.AwardLifetime);
            if (!await _announceArea(reward).ConfigureAwait(false)) ApplyAreaReward(reward);
        }
        Console.WriteLine($"{_logTag} [shadow] map {zone.Plan.MapId}: {(success ? "success" : "failure")}, winner {(result.Outcome == ShadowOutcome.None ? "none" : result.WinnerName)}, {awards.Count} awards");
    }

    private async Task TickLingerAsync(Zone zone, int elapsed, InGameSessionState[] sessions, InGameSessionState[] present)
    {
        var mission = zone.Mission;
        var wasOver = mission.LingerOver;
        mission.Tick(elapsed);
        if (!wasOver && mission.LingerOver)
        {
            foreach (var session in present) await SendOutAsync(session).ConfigureAwait(false);
            ClearMap(zone);
            mission.Close();
            await FlushAsync(zone, sessions).ConfigureAwait(false);
        }
        if (mission.ReadyAgain)
        {
            _zones.Remove(zone.Plan.MapId);
            Console.WriteLine($"{_logTag} [shadow] map {zone.Plan.MapId}: event closed");
        }
    }

    private void Handle(Zone zone, MonsterEntity victim, ushort killer, InGameSessionState[] present)
    {
        var mission = zone.Mission;
        var killerSession = present.FirstOrDefault(session => session.PlayerEntityId == killer);
        if (victim.MonsterParamId == ShadowStatues.MonsterId)
        {
            mission.StatueDestroyed();
            return;
        }
        if (TypeOf(victim.MonsterParamId) is BossMonsterType or UncappedMonsterType)
        {
            if (killerSession is not null) mission.BossKilled(ParticipantOf(killerSession));
            return;
        }
        zone.Runner.RecordKill(victim.MonsterParamId);
        if (killerSession is not null) mission.CreditKill(ParticipantOf(killerSession));
    }

    private bool IsSiegeUnit(MonsterEntity monster) =>
        monster.MonsterParamId != ShadowStatues.MonsterId
        && !monster.IsDead
        && _isShadowUnit(monster.MonsterParamId);

    private List<StatueState> StatuesOf(Zone zone, DateTimeOffset now) =>
        OnMap(zone)
            .Where(monster => monster.MonsterParamId == ShadowStatues.MonsterId)
            .OrderBy(monster => monster.RegionId)
            .Select(monster =>
            {
                var (x, y) = monster.GetInterpolatedPosition(now);
                return new StatueState(monster.EntityId, x, y, monster.CurrentHp, monster.MaxHp);
            })
            .ToList();

    private ShadowStatueGauge[] GaugesOf(Zone zone, InGameSessionState[] present, DateTimeOffset now)
    {
        var statues = StatuesOf(zone, now);
        var players = present.Select(session => ((int)session.PlayerX, (int)session.PlayerY)).ToArray();
        var attackers = OnMap(zone)
            .Where(IsSiegeUnit)
            .Select(unit => _battleTargetOf(unit.EntityId))
            .OfType<ushort>()
            .GroupBy(target => target)
            .ToDictionary(group => group.Key, group => group.Count());
        var bars = Math.Max(1, _tables().HealthCountOf(zone.Plan.MapId));
        return statues
            .Select(statue => ShadowStatueGauge.Of(statue, bars, ShadowStatues.PlayersNear(statue, players), attackers.GetValueOrDefault(statue.EntityId)))
            .ToArray();
    }

    private void ClearMap(Zone zone)
    {
        foreach (var monster in OnMap(zone).ToArray())
        {
            if (_monsters.Remove(monster.EntityId)) zone.Outbox.Add((Opcode.S2C_ENTITY_DESPAWN, _despawnFrame(monster.EntityId)));
        }
    }

    private static async Task SendOutAsync(InGameSessionState session)
    {
        if (session.Character is not { } character || session.RequestZoneChange is not { } request) return;
        var home = RestartArrival.NormalizeReturnMap(character.Home);
        await request(new ZoneChangeRequest(home, TeleportArrival.ChangeZoneType, null, TimeSpan.Zero), default).ConfigureAwait(false);
    }

    private async Task FlushAsync(Zone zone, InGameSessionState[] sessions)
    {
        if (zone.Outbox.Count == 0) return;
        var frames = zone.Outbox.ToArray();
        zone.Outbox.Clear();
        foreach (var session in sessions.Where(session => session.MapId == zone.Plan.MapId && session.ClientReady))
        {
            foreach (var (opcode, payload) in frames)
            {
                if (!await SendAsync(session, opcode, payload).ConfigureAwait(false)) break;
            }
        }
    }

    private async Task<bool> SendAsync(InGameSessionState session, Opcode opcode, byte[] payload)
    {
        try
        {
            await session.SendFrameAsync(opcode, payload, (byte)Random.Shared.Next(1, 256), default).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is ObjectDisposedException or IOException)
        {
            Console.WriteLine($"{_logTag} [shadow] frame 0x{(ushort)opcode:X4} not delivered to 0x{session.PlayerEntityId:X4}: {ex.GetType().Name}");
            return false;
        }
    }

    private static ShadowParticipant ParticipantOf(InGameSessionState session) =>
        new(session.Character!.CharacterId, session.Character.Name, session.Guild?.GuildName ?? string.Empty);

    private IEnumerable<MonsterEntity> OnMap(Zone zone) =>
        _monsters.GetByScene(zone.Plan.MapId, SharedScene).Where(monster => !monster.IsDead);

    private int SpawnAt(Zone zone, int monsterId, int regionId)
    {
        var points = _geometry.TryGetValue(zone.Plan.MapId, out var geometry)
            ? geometry.PointsOfType(ZoneGeometry.PointTypeRegionSpawn).Where(point => point.Id == regionId).ToArray()
            : Array.Empty<ZonePoint>();
        int x = DefaultSpawnX, y = DefaultSpawnY;
        if (points.Length != 0)
        {
            ZonePoint point;
            lock (_rng) point = points[_rng.Next(points.Length)];
            x = point.X;
            y = point.Y;
        }
        if (_monsters.Spawn(monsterId, zone.Plan.MapId, (ushort)x, (ushort)y, null, null, regionId, SharedScene) is not { } monster) return 0;
        zone.Outbox.Add((Opcode.NPC_SPAWN, _spawnFrame(monster)));
        return monster.EntityId;
    }

    private int TypeOf(int monsterId) => _monsters.Param(monsterId)?.Type ?? 0;

    private (MonsterEntity Monster, ushort Killer)[] TakeKills()
    {
        lock (_killGate)
        {
            var kills = _kills.ToArray();
            _kills.Clear();
            return kills;
        }
    }

    private void OnMonsterKilled(MonsterEntity monster, ushort killer)
    {
        if (!OwnsMap(monster.MapId)) return;
        lock (_killGate) _kills.Add((monster, killer));
    }
}
