using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Combat;

public readonly record struct TacticTarget(int Level, int Job, int Hp, int MaxHp);

public sealed class MonsterTactics
{
    public const int CastActionType = 4;
    public const int PassMs = 1000;
    public const int FreshArmedMs = 10;
    public const int ForcedPickOption = 1;
    public const int RatioCap = 100;
    public const float PercentScale = 0.01f;
    public const int OnceAfterFactor = 10;
    public const int EveryFactor = 11;

    private readonly IReadOnlyList<MonsterTacticEntry> _tactics;
    private readonly Dictionary<int, Entry> _active = new();
    private readonly Dictionary<int, int> _timedDueMs = new();
    private int _passMs;
    private int _battleMs;

    public MonsterTactics(IReadOnlyList<MonsterTacticEntry> tactics)
    {
        _tactics = tactics ?? throw new ArgumentNullException(nameof(tactics));
    }

    public bool Usable => _active.Count > 0;

    public bool Advance(int elapsedMs)
    {
        foreach (var entry in _active.Values)
        {
            if (!entry.Cooling) continue;
            entry.ElapsedMs += elapsedMs;
            if (entry.CooldownMs < entry.ElapsedMs)
            {
                entry.ElapsedMs = 0;
                entry.Cooling = false;
            }
        }
        _passMs += elapsedMs;
        _battleMs += elapsedMs;
        if (_passMs <= PassMs) return false;
        _passMs = 0;
        return true;
    }

    public void Rearm(int roll, TacticTarget? target, int ownerHp)
    {
        foreach (var tactic in _tactics)
        {
            var ratio = Math.Clamp(tactic.ActionRatio, 0, RatioCap);
            var fires = (IsTimed(tactic) ? TimedDue(tactic) : Factor(tactic, target, ownerHp)) && ratio > roll;
            if (_active.TryGetValue(tactic.Id, out var entry))
            {
                entry.Armed = fires && !entry.Cooling;
            }
            else if (fires)
            {
                _active[tactic.Id] = new Entry { Armed = true, CooldownMs = tactic.TimeSeconds * 1000 };
            }
        }
    }

    public MonsterTacticEntry? Pick(Func<int, int> randomBelow)
    {
        ArgumentNullException.ThrowIfNull(randomBelow);
        var eligible = _active
            .Where(pair => pair.Value.Armed && !pair.Value.Cooling && pair.Value.ElapsedMs <= FreshArmedMs)
            .Select(pair => (Entry: pair.Value, Tactic: _tactics.FirstOrDefault(tactic => tactic.Id == pair.Key)))
            .Where(pair => pair.Tactic is not null)
            .ToList();
        var sum = 0;
        foreach (var (entry, tactic) in eligible)
        {
            if ((tactic!.Option & ForcedPickOption) != 0 || IsTimed(tactic))
            {
                entry.Armed = false;
                entry.Cooling = true;
                if (IsTimed(tactic)) _timedDueMs[tactic.Id] = tactic.FactorType == EveryFactor ? DueMs(tactic) + tactic.FactorValue : int.MaxValue;
                return tactic;
            }
            sum = (short)(sum + tactic.RandomWeight);
        }
        if (sum <= 0) return null;
        var drawn = randomBelow(sum);
        var acc = 0;
        MonsterTacticEntry? chosen = null;
        foreach (var (entry, tactic) in eligible)
        {
            if (chosen is null && acc <= drawn && drawn < acc + tactic!.RandomWeight)
            {
                entry.Cooling = true;
                chosen = tactic;
            }
            entry.Armed = false;
            acc += tactic!.RandomWeight;
        }
        return chosen;
    }

    public static bool IsTimed(MonsterTacticEntry tactic) => tactic is { FactorType: OnceAfterFactor or EveryFactor, FactorValue: > 0 };

    private int DueMs(MonsterTacticEntry tactic) => _timedDueMs.TryGetValue(tactic.Id, out var due) ? due : tactic.FactorValue;

    private bool TimedDue(MonsterTacticEntry tactic) => _battleMs >= DueMs(tactic);

    public static bool Factor(MonsterTacticEntry tactic, TacticTarget? target, int ownerHp)
    {
        ArgumentNullException.ThrowIfNull(tactic);
        var value = tactic.FactorValue;
        return tactic.FactorType switch
        {
            8 => value < ownerHp,
            9 => ownerHp < value,
            1 when target is { } t => t.Level > value,
            2 when target is { } t => t.Level < value,
            3 when target is { } t => t.Job == value - 1,
            4 when target is { } t => t.Hp > value,
            5 when target is { } t => t.Hp < value,
            6 when target is { } t => t.Hp > Share(t.MaxHp, value),
            7 when target is { } t => t.Hp < Share(t.MaxHp, value),
            _ => false,
        };
    }

    private static short Share(int maxHp, int percent) => (short)(int)(unchecked(maxHp * percent) * (double)PercentScale);

    private sealed class Entry
    {
        public int ElapsedMs;
        public bool Armed;
        public bool Cooling;
        public int CooldownMs;
    }
}
