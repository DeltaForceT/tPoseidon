using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Monsters;

public interface IScenarioHost
{
    int AliveMonsters { get; }
    int AliveOf(int monsterId);
    int AliveWithInstanceKey(int instanceKey);
    int ZonePlayers { get; }
    int MonsterTypeOf(int monsterId);
    void Spawn(int monsterId, int regionId);
    int SpawnCaster(int monsterId, int regionId);
    void CastAllMapSkill(int casterEntityId, ScenarioStep scenarioStep);
    void Announce(string comment);
}

public sealed class MonsterScenarioRun
{
    public const int FirstStepIndex = 1;
    public const long MillisecondsPerSecond = 1000;
    public const long PendingRefillSlowMilliseconds = 5000;
    public const long PendingRefillFastMilliseconds = 1000;
    public const long PendingConditionMilliseconds = 5000;
    public const int LiveMonsterCap = 20;
    public const int ShadowBossMonsterType = 7;
    public const int UncappedMonsterType = 10;

    private enum Verdict
    {
        Unconditional,
        Passed,
        Waiting,
    }

    private readonly Dictionary<int, ScenarioStep> _byId;
    private readonly IScenarioHost _host;
    private readonly List<ScenarioStep> _pending = new();
    private readonly Dictionary<int, int> _killsByMonster = new();
    private int _index = FirstStepIndex;
    private long _lastCondition;
    private long _lastFast;
    private long _lastSlow;

    public MonsterScenarioRun(IReadOnlyList<ScenarioStep> steps, IScenarioHost host)
    {
        ArgumentNullException.ThrowIfNull(steps);
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _byId = steps.ToDictionary(step => step.Id);
    }

    public int StepIndex => _index;
    public int PendingCount => _pending.Count;
    public bool Finished => !_byId.ContainsKey(_index);

    public void RecordKill(int monsterId) => _killsByMonster[monsterId] = _killsByMonster.GetValueOrDefault(monsterId) + 1;

    public void Tick(long elapsedMilliseconds)
    {
        CheckPendingConditions(elapsedMilliseconds);
        RefillPending(elapsedMilliseconds);
        RunStep(elapsedMilliseconds);
    }

    private void RunStep(long elapsed)
    {
        if (!_byId.TryGetValue(_index, out var step) || step.TimeSeconds * MillisecondsPerSecond >= elapsed) return;
        if (Evaluate(step, true) == Verdict.Unconditional)
        {
            switch (step.Result)
            {
                case ScenarioResults.RefillEveryFiveSeconds or ScenarioResults.RefillEverySecond:
                    _pending.Add(step);
                    break;
                case ScenarioResults.RemoveRefill:
                    var standing = _pending.FindIndex(entry => entry.RegionId == step.RegionId && entry.MonsterId == step.MonsterId);
                    if (standing >= 0) _pending.RemoveAt(standing);
                    break;
                default:
                    SpawnMany(step, step.ResultValue);
                    break;
            }
            if (step.Comment.Length != 0) _host.Announce(step.Comment);
        }
        _index++;
    }

    private void RefillPending(long elapsed)
    {
        if (Due(ref _lastFast, elapsed, PendingRefillFastMilliseconds)) Refill(ScenarioResults.RefillEverySecond);
        if (Due(ref _lastSlow, elapsed, PendingRefillSlowMilliseconds)) Refill(ScenarioResults.RefillEveryFiveSeconds);
    }

    private void Refill(int result)
    {
        foreach (var entry in _pending.Where(entry => entry.Result == result).ToArray())
        {
            SpawnMany(entry, entry.ResultValue - _host.AliveOf(entry.MonsterId));
        }
    }

    private void CheckPendingConditions(long elapsed)
    {
        if (!Due(ref _lastCondition, elapsed, PendingConditionMilliseconds)) return;
        foreach (var entry in _pending.ToArray())
        {
            if (Evaluate(entry, false) != Verdict.Passed) continue;
            _pending.Remove(entry);
            if (entry.Comment.Length != 0) _host.Announce(entry.Comment);
        }
    }

    private static bool Due(ref long last, long elapsed, long period)
    {
        if (elapsed - last <= period) return false;
        last = elapsed;
        return true;
    }

    private Verdict Evaluate(ScenarioStep step, bool queueWhenWaiting)
    {
        bool passed;
        switch (step.Condition)
        {
            case ScenarioConditions.AliveAtLeast:
                passed = step.Value <= _host.AliveMonsters;
                break;
            case ScenarioConditions.AliveBelow:
                passed = step.Value > _host.AliveMonsters;
                break;
            case ScenarioConditions.KillsAtLeast:
                passed = step.Value <= _killsByMonster.Values.Sum();
                if (passed) _killsByMonster.Clear();
                break;
            case ScenarioConditions.TargetAliveAtLeast:
                passed = step.Value <= _host.AliveWithInstanceKey(step.CheckMonsterId);
                break;
            case ScenarioConditions.TargetAliveBelow:
                passed = step.Value > _host.AliveWithInstanceKey(step.CheckMonsterId);
                break;
            case ScenarioConditions.TargetKillsAtLeast:
                passed = step.Value <= _killsByMonster.GetValueOrDefault(step.CheckMonsterId);
                if (passed) _killsByMonster.Clear();
                break;
            case ScenarioConditions.PlayersAboveAlive:
                passed = step.Value <= _host.ZonePlayers - _host.AliveMonsters;
                break;
            case ScenarioConditions.SpawnPerPlayer:
                SpawnMany(step, step.ResultValue * _host.ZonePlayers);
                return Verdict.Passed;
            case ScenarioConditions.CastAllMapSkill:
                var caster = step.AllMapSkillId == 0 ? 0 : _host.SpawnCaster(step.MonsterId, step.RegionId);
                _host.CastAllMapSkill(caster, step);
                return Verdict.Passed;
            default:
                return Verdict.Unconditional;
        }
        if (passed)
        {
            SpawnMany(step, step.ResultValue);
            return Verdict.Passed;
        }
        if (queueWhenWaiting) _pending.Add(step);
        return Verdict.Waiting;
    }

    private void SpawnMany(ScenarioStep step, int count)
    {
        var capped = _host.MonsterTypeOf(step.MonsterId) is not (ShadowBossMonsterType or UncappedMonsterType);
        for (var spawned = 0; spawned < count; spawned++)
        {
            if (capped && _host.AliveMonsters >= LiveMonsterCap) return;
            _host.Spawn(step.MonsterId, step.RegionId);
        }
    }
}
