using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.World;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Warp;
using tPoseidon.Core.Net.Dialogs;

internal sealed class ScenarioController : IDisposable
{
    public const int MaxRuns = 3;
    public const long FrameMilliseconds = 100;
    public const ushort DefaultSpawnX = 600;
    public const ushort DefaultSpawnY = 400;
    public const int HarconMonsterType = 8;
    public const int BossMonsterType = 7;
    public const int UncappedMonsterType = 10;
    public const int MinimumCastLevel = 1;

    private sealed record SkillCast(ushort CasterEntityId, int SkillId, int Level);

    private sealed record OutboxEntry(Opcode Opcode, byte[] Payload, SkillCast? Cast = null);

    private sealed class Run
    {
        public required ushort MapId { get; init; }
        public required ushort Key { get; init; }
        public MonsterScenarioRun Runner { get; set; } = null!;
        public required DateTimeOffset StartedAt { get; init; }
        public long TickedUntil { get; set; }
        public List<OutboxEntry> Outbox { get; } = new();
    }

    private sealed class Host : IScenarioHost
    {
        private readonly ScenarioController _owner;
        private readonly Run _run;

        public Host(ScenarioController owner, Run run)
        {
            _owner = owner;
            _run = run;
        }

        public int AliveMonsters => _owner._monsters.GetByScene(_run.MapId, _run.Key).Count(monster => _owner.TypeOf(monster.MonsterParamId) != HarconMonsterType);

        public int AliveOf(int monsterId) => _owner._monsters.GetByScene(_run.MapId, _run.Key).Count(monster => monster.MonsterParamId == monsterId);

        public int AliveWithInstanceKey(int instanceKey) =>
            _owner._monsters.All.Count(monster => monster.MapId == _run.MapId && monster.InstanceKey == instanceKey && _owner.TypeOf(monster.MonsterParamId) != HarconMonsterType);

        public int ZonePlayers => _owner._sessions().Count(session => session.MapId == _run.MapId && session.ClientReady);

        public int MonsterTypeOf(int monsterId) => _owner.TypeOf(monsterId);

        public void Spawn(int monsterId, int regionId) => _owner.SpawnAt(_run, monsterId, regionId);

        public int SpawnCaster(int monsterId, int regionId) => _owner.SpawnAt(_run, monsterId, regionId);

        public void CastAllMapSkill(int casterEntityId, ScenarioStep scenarioStep) =>
            _run.Outbox.Add(new OutboxEntry(
                Opcode.S2C_SCENARIO_ALL_MAP_SKILL,
                ScenarioFrames.AllMapSkill((uint)scenarioStep.MonsterId, (uint)scenarioStep.AllMapSkillId, scenarioStep.SkillComment, _owner._text),
                casterEntityId != 0 && scenarioStep.AllMapSkillId != 0
                    ? new SkillCast((ushort)casterEntityId, scenarioStep.AllMapSkillId, Math.Max(scenarioStep.Value, MinimumCastLevel))
                    : null));

        public void Announce(string comment) =>
            _run.Outbox.Add(new OutboxEntry(Opcode.S2C_SCENARIO_NOTICE, ScenarioFrames.Notice(comment, _owner._text)));
    }

    private readonly Func<ScenarioTables> _tables;
    private readonly MonsterManager _monsters;
    private readonly IReadOnlyDictionary<ushort, ZoneGeometry> _geometry;
    private readonly Func<ushort, IReadOnlyList<BossSpawnTableEntry>> _bossTable;
    private readonly Func<IReadOnlyCollection<InGameSessionState>> _sessions;
    private readonly Func<MonsterEntity, byte[]> _spawnFrame;
    private readonly Func<ushort, byte[]> _despawnFrame;
    private readonly Action<ushort, ushort, int, int> _castOnPlayer;
    private readonly Encoding _text;
    private readonly string _logTag;
    private readonly Dictionary<(ushort Map, ushort Key), Run> _runs = new();
    private readonly HashSet<(ushort Map, ushort Key)> _ended = new();
    private readonly HashSet<InGameSessionState> _leaving = new();
    private readonly object _killGate = new();
    private readonly List<MonsterEntity> _kills = new();
    private readonly Random _rng = new();

    public ScenarioController(
        Func<ScenarioTables> tables, MonsterManager monsters, IReadOnlyDictionary<ushort, ZoneGeometry> geometry,
        Func<ushort, IReadOnlyList<BossSpawnTableEntry>> bossTable, Func<IReadOnlyCollection<InGameSessionState>> sessions,
        Func<MonsterEntity, byte[]> spawnFrame, Func<ushort, byte[]> despawnFrame,
        Action<ushort, ushort, int, int> castOnPlayer, Encoding text, string logTag)
    {
        _tables = tables ?? throw new ArgumentNullException(nameof(tables));
        _monsters = monsters ?? throw new ArgumentNullException(nameof(monsters));
        _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        _bossTable = bossTable ?? throw new ArgumentNullException(nameof(bossTable));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _spawnFrame = spawnFrame ?? throw new ArgumentNullException(nameof(spawnFrame));
        _despawnFrame = despawnFrame ?? throw new ArgumentNullException(nameof(despawnFrame));
        _castOnPlayer = castOnPlayer ?? throw new ArgumentNullException(nameof(castOnPlayer));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
        _monsters.MonsterKilled += OnMonsterKilled;
    }

    public void Dispose() => _monsters.MonsterKilled -= OnMonsterKilled;

    public async Task TickAsync(DateTimeOffset now)
    {
        var sessions = _sessions().Where(session => session.ClientReady && InstancedMaps.Contains(session.MapId)).ToArray();
        var present = sessions.Select(session => (session.MapId, session.InstanceKey)).ToHashSet();
        _ended.RemoveWhere(key => !present.Contains(key));
        _leaving.RemoveWhere(leaver => !sessions.Contains(leaver));
        foreach (var gone in _runs.Keys.Where(key => !present.Contains(key)).ToArray()) await StopAsync(_runs[gone], sessions).ConfigureAwait(false);
        foreach (var session in sessions)
        {
            if (_runs.ContainsKey((session.MapId, session.InstanceKey)) || _ended.Contains((session.MapId, session.InstanceKey))) continue;
            await StartAsync(session, now, sessions).ConfigureAwait(false);
        }
        foreach (var kill in TakeKills()) await HandleKillAsync(kill, sessions).ConfigureAwait(false);
        foreach (var run in _runs.Values.ToArray())
        {
            var elapsed = (long)(now - run.StartedAt).TotalMilliseconds;
            for (var frame = run.TickedUntil + FrameMilliseconds; frame <= elapsed; frame += FrameMilliseconds)
            {
                run.Runner.Tick(frame);
                run.TickedUntil = frame;
            }
            await FlushAsync(run, sessions).ConfigureAwait(false);
        }
    }

    private async Task StartAsync(InGameSessionState owner, DateTimeOffset now, InGameSessionState[] sessions)
    {
        var tables = _tables();
        if (tables.TableOf(owner.MapId, _rng.Next()) is not { } tableName || !tables.Steps.TryGetValue(tableName, out var steps)) return;
        if (_runs.Count >= MaxRuns)
        {
            Console.WriteLine($"{_logTag} [scenario] map {owner.MapId}: {_runs.Count} of {MaxRuns} runs active, none started for 0x{owner.PlayerEntityId:X4}");
            return;
        }
        var run = new Run { MapId = owner.MapId, Key = owner.InstanceKey, StartedAt = now };
        run.Runner = new MonsterScenarioRun(steps, new Host(this, run));
        _runs[(run.MapId, run.Key)] = run;
        Console.WriteLine($"{_logTag} [scenario] map {run.MapId}: run for 0x{owner.PlayerEntityId:X4} on {tableName} ({_runs.Count} of {MaxRuns})");
        foreach (var entry in _bossTable(run.MapId))
        {
            if (entry.Region == 0 || TypeOf(entry.MonsterId) != HarconMonsterType) continue;
            foreach (var point in PointsOf(run.MapId, entry.Region)) SpawnAtPoint(run, entry.MonsterId, entry.Region, point.X, point.Y);
        }
        await FlushAsync(run, sessions).ConfigureAwait(false);
    }

    private async Task StopAsync(Run run, InGameSessionState[] sessions)
    {
        _runs.Remove((run.MapId, run.Key));
        foreach (var monster in _monsters.All.Where(monster => monster.MapId == run.MapId && monster.InstanceKey == run.Key).ToArray())
        {
            if (_monsters.Remove(monster.EntityId)) run.Outbox.Add(new OutboxEntry(Opcode.S2C_ENTITY_DESPAWN, _despawnFrame(monster.EntityId)));
        }
        await FlushAsync(run, sessions).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} [scenario] map {run.MapId}: run of instance 0x{run.Key:X4} stopped");
    }

    private async Task HandleKillAsync(MonsterEntity victim, InGameSessionState[] sessions)
    {
        if (!_runs.TryGetValue((victim.MapId, victim.InstanceKey), out var run)) return;
        switch (TypeOf(victim.MonsterParamId))
        {
            case HarconMonsterType:
                run.Outbox.Add(new OutboxEntry(Opcode.S2C_SCENARIO_OUTCOME, ScenarioFrames.Outcome(false)));
                await FlushAsync(run, sessions).ConfigureAwait(false);
                _ended.Add((run.MapId, run.Key));
                await StopAsync(run, sessions).ConfigureAwait(false);
                await RewardAsync(run, sessions).ConfigureAwait(false);
                break;
            case BossMonsterType or UncappedMonsterType:
                run.Outbox.Add(new OutboxEntry(Opcode.S2C_SCENARIO_OUTCOME, ScenarioFrames.Outcome(true)));
                await FlushAsync(run, sessions).ConfigureAwait(false);
                await RewardAsync(run, sessions).ConfigureAwait(false);
                break;
            default:
                run.Runner.RecordKill(victim.MonsterParamId);
                break;
        }
    }

    private async Task RewardAsync(Run run, InGameSessionState[] sessions)
    {
        foreach (var session in sessions.Where(session => session.MapId == run.MapId && session.InstanceKey == run.Key))
        {
            if (ScenarioExit.ZoneOf(session.FiestaReturnZone) is not { } exitZone
                || session.RequestZoneChange is not { } requestZoneChange
                || !_leaving.Add(session))
            {
                continue;
            }
            Console.WriteLine($"{_logTag} [scenario] map {run.MapId}: 0x{session.PlayerEntityId:X4} leaves for zone {exitZone} in {ScenarioExit.Delay.TotalSeconds:0} s");
            await requestZoneChange(new ZoneChangeRequest(exitZone, TeleportArrival.ChangeZoneType, null, ScenarioExit.Delay), default).ConfigureAwait(false);
        }
    }

    private async Task FlushAsync(Run run, InGameSessionState[] sessions)
    {
        if (run.Outbox.Count == 0) return;
        var frames = run.Outbox.ToArray();
        run.Outbox.Clear();
        foreach (var session in sessions.Where(session => session.MapId == run.MapId && session.InstanceKey == run.Key))
        {
            foreach (var entry in frames)
            {
                try
                {
                    await session.SendFrameAsync(entry.Opcode, entry.Payload, (byte)Random.Shared.Next(1, 256), default).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is ObjectDisposedException or IOException)
                {
                    Console.WriteLine($"{_logTag} [scenario] frame 0x{(ushort)entry.Opcode:X4} not delivered to 0x{session.PlayerEntityId:X4}: {ex.GetType().Name}");
                    break;
                }
                if (entry.Cast is { } cast) _castOnPlayer(cast.CasterEntityId, session.PlayerEntityId, cast.SkillId, cast.Level);
            }
        }
    }

    private int SpawnAt(Run run, int monsterId, int regionId)
    {
        var points = PointsOf(run.MapId, regionId);
        if (points.Length == 0) return SpawnAtPoint(run, monsterId, regionId, DefaultSpawnX, DefaultSpawnY);
        ZonePoint point;
        lock (_rng) point = points[_rng.Next(points.Length)];
        return SpawnAtPoint(run, monsterId, regionId, point.X, point.Y);
    }

    private int SpawnAtPoint(Run run, int monsterId, int regionId, int x, int y)
    {
        if (_monsters.Spawn(monsterId, run.MapId, (ushort)x, (ushort)y, null, null, regionId, run.Key) is not { } monster) return 0;
        run.Outbox.Add(new OutboxEntry(Opcode.NPC_SPAWN, _spawnFrame(monster)));
        return monster.EntityId;
    }

    private ZonePoint[] PointsOf(ushort mapId, int regionId) =>
        _geometry.TryGetValue(mapId, out var geometry)
            ? geometry.PointsOfType(ZoneGeometry.PointTypeRegionSpawn).Where(point => point.Id == regionId).ToArray()
            : Array.Empty<ZonePoint>();

    private int TypeOf(int monsterId) => _monsters.Param(monsterId)?.Type ?? 0;

    private MonsterEntity[] TakeKills()
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
        if (monster.InstanceKey == 0) return;
        lock (_killGate) _kills.Add(monster);
    }
}
