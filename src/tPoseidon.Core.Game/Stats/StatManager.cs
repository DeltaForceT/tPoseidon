using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Progression;

namespace tPoseidon.Core.Game.Stats;

public sealed class StatManager
{
    private readonly object _lock = new();
    private uint _money;
    private const ulong DeathExpLossNumerator = 2;
    private const ulong DeathExpLossDenominator = 100;
    private const uint RestartHpPerCent = 30;
    private const uint PerCent = 100;

    private ulong _exp;
    private ulong _deathExpLoss;
    private uint _tmExp;
    private uint _hp;
    private uint _mp;
    private ushort _level;
    private ushort _tmLevel;
    private ushort _tmPoint;

    private readonly LevelExpTable? _expTable;
    private readonly Func<ServerRates.CapsSection> _capsProvider;

    public StatManager(
        Character snapshot,
        LevelExpTable? expTable = null,
        Func<ServerRates.CapsSection>? capsProvider = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        CharacterId = snapshot.CharacterId;
        _money = snapshot.Money;
        _exp = snapshot.Exp;
        _tmExp = snapshot.TmExp;
        _hp = snapshot.CurrentHp;
        _mp = snapshot.CurrentMp;
        _level = snapshot.Level;
        _tmLevel = snapshot.TmLevel;
        _tmPoint = snapshot.TmPoint;
        _expTable = expTable;
        _capsProvider = capsProvider ?? (() => new ServerRates.CapsSection());
    }

    public ulong CharacterId { get; }

    public uint Money { get { lock (_lock) return _money; } }
    public ulong Exp { get { lock (_lock) return _exp; } }
    public uint TmExp { get { lock (_lock) return _tmExp; } }
    public uint CurrentHp { get { lock (_lock) return _hp; } }
    public uint CurrentMp { get { lock (_lock) return _mp; } }
    public ushort Level { get { lock (_lock) return _level; } }
    public ushort TmLevel { get { lock (_lock) return _tmLevel; } }
    public ushort TmPoint { get { lock (_lock) return _tmPoint; } }

    public event Action<MoneyChangedEvent>? MoneyChanged;
    public event Action<ExpChangedEvent>? ExpChanged;
    public event Action<TmExpChangedEvent>? TmExpChanged;
    public event Action<HealthChangedEvent>? HealthChanged;
    public event Action<ManaChangedEvent>? ManaChanged;
    public event Action<LevelChangedEvent>? LevelUp;
    public event Action<TmLevelChangedEvent>? TmLevelUp;

    public void GrantMoney(int delta, StatChangeSource source = StatChangeSource.Combat)
    {
        if (delta == 0) return;
        uint newTotal;
        lock (_lock)
        {
            newTotal = (uint)Math.Max(0, (long)_money + delta);
            if (newTotal == _money) return;
            _money = newTotal;
        }
        MoneyChanged?.Invoke(new MoneyChangedEvent { CharacterId = CharacterId, NewTotal = newTotal, Delta = delta, Source = source });
    }

    public void GrantExp(int delta, StatChangeSource source = StatChangeSource.Combat)
    {
        if (delta == 0) return;
        ulong newTotal;
        ushort oldLevel;
        ushort newLevel;
        lock (_lock)
        {
            oldLevel = _level;
            newTotal = (ulong)Math.Max(0L, (long)_exp + delta);
            if (newTotal == _exp) return;
            _exp = newTotal;

            if (_expTable is not null)
            {
                var caps = _capsProvider();
                while (_level < caps.MaxLevel)
                {
                    var threshold = _expTable.ExpToNextLevel(_level);
                    if (threshold is null || threshold == 0) break;
                    if (_exp < threshold.Value) break;
                    _exp -= threshold.Value;
                    _level = (ushort)(_level + 1);
                }
                newTotal = _exp;
            }
            newLevel = _level;
        }
        ExpChanged?.Invoke(new ExpChangedEvent { CharacterId = CharacterId, NewTotal = newTotal, Delta = delta, Source = source });
        if (newLevel != oldLevel)
        {
            LevelUp?.Invoke(new LevelChangedEvent { CharacterId = CharacterId, NewLevel = newLevel });
        }
    }

    public void GrantTmExp(int delta, StatChangeSource source = StatChangeSource.Combat)
    {
        if (delta == 0) return;
        uint newTotal;
        ushort oldTmLevel;
        ushort newTmLevel;
        ushort newTmPoint;
        lock (_lock)
        {
            oldTmLevel = _tmLevel;
            newTotal = (uint)Math.Max(0L, (long)_tmExp + delta);
            if (newTotal == _tmExp) return;
            _tmExp = newTotal;

            if (_expTable is not null)
            {
                var caps = _capsProvider();
                while (_tmLevel < caps.MaxTmLevel)
                {
                    var threshold = _expTable.TmXpToNextLevel(_tmLevel);
                    if (threshold is null || threshold == 0) break;
                    if (_tmExp < threshold.Value) break;
                    _tmExp -= (uint)threshold.Value;
                    _tmLevel = (ushort)(_tmLevel + 1);
                    _tmPoint = (ushort)Math.Min(ushort.MaxValue, _tmPoint + 1);
                }
                newTotal = _tmExp;
            }
            newTmLevel = _tmLevel;
            newTmPoint = _tmPoint;
        }
        TmExpChanged?.Invoke(new TmExpChangedEvent { CharacterId = CharacterId, NewTotal = newTotal, Delta = delta, Source = source });
        if (newTmLevel != oldTmLevel)
        {
            TmLevelUp?.Invoke(new TmLevelChangedEvent { CharacterId = CharacterId, NewTmLevel = newTmLevel, NewTmPoint = newTmPoint });
        }
    }

    public void SetHp(uint newHp)
    {
        lock (_lock)
        {
            if (newHp == _hp) return;
            _hp = newHp;
        }
        HealthChanged?.Invoke(new HealthChangedEvent { CharacterId = CharacterId, NewHp = newHp });
    }

    public uint TakeHit(int damage)
    {
        if (damage <= 0) return CurrentHp;
        lock (_lock)
        {
            _hp = (uint)Math.Max(0, (long)_hp - damage);
            return _hp;
        }
    }

    public ulong DeathExpLoss { get { lock (_lock) return _deathExpLoss; } }

    public static uint RestartHp(uint maxHp) => maxHp * RestartHpPerCent / PerCent;

    public ulong Die() => Die(losesExp: true);

    public ulong Die(bool losesExp)
    {
        lock (_lock)
        {
            _hp = 0;
            _deathExpLoss = losesExp ? _exp * DeathExpLossNumerator / DeathExpLossDenominator : 0;
            _exp = _exp > _deathExpLoss ? _exp - _deathExpLoss : 0;
            return _exp;
        }
    }

    public void Resurrect(uint maxHp, uint maxMp, float hpRatio, float mpRatio, float expRatio)
    {
        lock (_lock)
        {
            _hp = (uint)((double)maxHp * hpRatio);
            var mpFloor = (uint)((double)maxMp * mpRatio);
            if (_mp < mpFloor) _mp = mpFloor;
            _exp += (ulong)((double)_deathExpLoss * expRatio);
            _deathExpLoss = 0;
        }
    }

    public ulong GiveBackDeathExp(float ratio)
    {
        lock (_lock)
        {
            _exp += (ulong)((double)_deathExpLoss * ratio);
            return _exp;
        }
    }

    public uint Restart(uint maxHp)
    {
        lock (_lock)
        {
            _hp = RestartHp(maxHp);
            _deathExpLoss = 0;
            return _hp;
        }
    }

    public ExpGain AddExp(int gain, LevelExpChart chart)
    {
        ArgumentNullException.ThrowIfNull(chart);
        lock (_lock)
        {
            if (_level >= LevelExpChart.LastLevel) return new ExpGain(ExpGainKind.Withheld, 0, _level, _exp);

            var exp = (long)_exp + gain;
            var nextExp = chart.NextExp(_level);
            if (exp < nextExp)
            {
                _exp = (ulong)Math.Max(0L, exp);
                return new ExpGain(ExpGainKind.Added, 0, _level, _exp);
            }

            var level = _level + 1;
            while (true)
            {
                exp -= nextExp;
                nextExp = chart.NextExp(level);
                if (nextExp == 0)
                {
                    _exp = (ulong)Math.Max(0L, exp);
                    return new ExpGain(ExpGainKind.Withheld, 0, _level, _exp);
                }
                if (exp < nextExp) break;
                level++;
                if (level >= LevelExpChart.LastLevel)
                {
                    level = LevelExpChart.LastLevel;
                    exp = 0;
                    break;
                }
            }

            var gained = level - _level;
            _level = (ushort)level;
            _exp = (ulong)Math.Max(0L, exp);
            return new ExpGain(ExpGainKind.LevelUp, gained, _level, _exp);
        }
    }

    public TmGain AddTmExp(int gain, TmLevelChart chart)
    {
        ArgumentNullException.ThrowIfNull(chart);
        lock (_lock)
        {
            if (_tmLevel >= TmLevelChart.LastLevel) return new TmGain(false, Array.Empty<TmLevelStep>(), _tmExp);

            var tmExp = (long)_tmExp + gain;
            var nextTmExp = chart.NextTmExp(_tmLevel);
            var levelUps = new List<TmLevelStep>();
            while (nextTmExp <= tmExp)
            {
                _tmLevel = (ushort)(_tmLevel + 1);
                tmExp -= nextTmExp;
                nextTmExp = chart.NextTmExp(_tmLevel);
                if (nextTmExp < 1)
                {
                    _tmExp = (uint)Math.Max(0L, tmExp);
                    return new TmGain(true, levelUps, _tmExp);
                }
                if (_tmLevel >= TmLevelChart.LastLevel)
                {
                    _tmLevel = TmLevelChart.LastLevel;
                    tmExp = 0;
                }
                _tmPoint = (ushort)(_tmPoint + 1);
                levelUps.Add(new TmLevelStep(_tmLevel, (uint)Math.Max(0L, tmExp), _tmPoint));
            }

            _tmExp = (uint)Math.Max(0L, tmExp);
            return new TmGain(levelUps.Count == 0, levelUps, _tmExp);
        }
    }

    public (uint Hp, uint Mp) RestoreVitals(int hpAmount, uint maxHp, int mpAmount, uint maxMp)
    {
        lock (_lock)
        {
            if (hpAmount != 0) _hp = (uint)Math.Clamp((long)_hp + hpAmount, 0, maxHp);
            if (mpAmount != 0) _mp = (uint)Math.Clamp((long)_mp + mpAmount, 0, maxMp);
            return (_hp, _mp);
        }
    }

    public void SetVitals(uint hp, uint mp)
    {
        lock (_lock)
        {
            _hp = hp;
            _mp = mp;
        }
    }

    public ushort SpendTmPoints(int points)
    {
        lock (_lock)
        {
            _tmPoint = (ushort)Math.Max(0, _tmPoint - Math.Max(0, points));
            return _tmPoint;
        }
    }

    public void SetMp(uint newMp)
    {
        lock (_lock)
        {
            if (newMp == _mp) return;
            _mp = newMp;
        }
        ManaChanged?.Invoke(new ManaChangedEvent { CharacterId = CharacterId, NewMp = newMp });
    }

    public void ApplyQuestRewards(int gelder, int exp, int tmxp)
    {
        if (gelder > 0) GrantMoney(gelder, StatChangeSource.Quest);
        if (exp > 0) GrantExp(exp, StatChangeSource.Quest);
        if (tmxp > 0) GrantTmExp(tmxp, StatChangeSource.Quest);
    }

    public static AccPtsAtLevel RecomputeAccPts(byte ptsP, byte ptsM, byte ptsS, byte ptsC, ushort level)
    {
        int multiplier = Math.Max(0, level - 1);
        ushort p = (ushort)Math.Min(ushort.MaxValue, multiplier * ptsP);
        ushort m = (ushort)Math.Min(ushort.MaxValue, multiplier * ptsM);
        ushort s = (ushort)Math.Min(ushort.MaxValue, multiplier * ptsS);
        ushort c = (ushort)Math.Min(ushort.MaxValue, multiplier * ptsC);
        ushort attr = (ushort)Math.Min(ushort.MaxValue, multiplier * 4);
        return new AccPtsAtLevel
        {
            ApPts = p, AcPts = p, DxPts = p,
            MaxMpPts = m, MaPts = m, MdPts = m,
            MaxWtPts = s, DaPts = s, LkPts = s,
            MaxHpPts = c, DpPts = c, HvPts = c,
            AttrPts = attr,
        };
    }
}

public sealed record AccPtsAtLevel
{
    public ushort ApPts { get; init; }
    public ushort AcPts { get; init; }
    public ushort DxPts { get; init; }
    public ushort MaxMpPts { get; init; }
    public ushort MaPts { get; init; }
    public ushort MdPts { get; init; }
    public ushort MaxWtPts { get; init; }
    public ushort DaPts { get; init; }
    public ushort LkPts { get; init; }
    public ushort MaxHpPts { get; init; }
    public ushort DpPts { get; init; }
    public ushort HvPts { get; init; }
    public ushort AttrPts { get; init; }
}

public enum StatChangeSource
{
    Combat,
    Quest,
    Item,
    GM,
    Regen,
    Bank,
    Warehouse,
    Teleport,
    Compound,
    Exchange,
    Refine,
    Growth,
    Trade,
    Fortune,
    LookChange,
    EquipUpgrade,
    Marriage,
    CardBattle,
}

public sealed record MoneyChangedEvent
{
    public required ulong CharacterId { get; init; }
    public required uint NewTotal { get; init; }
    public required int Delta { get; init; }
    public StatChangeSource Source { get; init; } = StatChangeSource.Combat;
}

public sealed record ExpChangedEvent
{
    public required ulong CharacterId { get; init; }
    public required ulong NewTotal { get; init; }
    public required int Delta { get; init; }
    public StatChangeSource Source { get; init; } = StatChangeSource.Combat;
}

public sealed record TmExpChangedEvent
{
    public required ulong CharacterId { get; init; }
    public required uint NewTotal { get; init; }
    public required int Delta { get; init; }
    public StatChangeSource Source { get; init; } = StatChangeSource.Combat;
}

public sealed record HealthChangedEvent
{
    public required ulong CharacterId { get; init; }
    public required uint NewHp { get; init; }
}

public sealed record ManaChangedEvent
{
    public required ulong CharacterId { get; init; }
    public required uint NewMp { get; init; }
}

public sealed record LevelChangedEvent
{
    public required ulong CharacterId { get; init; }
    public required ushort NewLevel { get; init; }
}

public enum ExpGainKind
{
    Added,
    LevelUp,
    Withheld,
}

public readonly record struct ExpGain(ExpGainKind Kind, int LevelsGained, ushort Level, ulong Exp);

public readonly record struct TmLevelStep(ushort TmLevel, uint TmExp, ushort TmPoint);

public sealed record TmGain(bool SendsTotal, IReadOnlyList<TmLevelStep> LevelUps, uint TmExp);

public sealed record TmLevelChangedEvent
{
    public required ulong CharacterId { get; init; }
    public required ushort NewTmLevel { get; init; }
    public required ushort NewTmPoint { get; init; }
}
