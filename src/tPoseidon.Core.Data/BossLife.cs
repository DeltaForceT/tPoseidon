namespace tPoseidon.Core.Data;

public sealed class BossLife
{
    public const int ElementCount = 11;
    public const int ElementWireBase = 0x0B;
    public const int IdleRefillMs = 50000;
    public const int SummonAnnounceDelayMs = 2000;
    public const int SummonMapMonsterLimit = 0x200;
    public const int BarLossGuardMs = 2000;
    public const int HealSkillId = 0x13ED;
    public const int SkillRollModulus = 5;

    private readonly BossLifeProfile _profile;
    private readonly BossEventOutbox _events;
    private readonly Action<int, int> _resistSink;
    private readonly long[] _damageByElement = new long[ElementCount];
    private readonly int[] _growth = new int[ElementCount];
    private int _barsLeft;
    private int _idleMs;
    private int _summonTimerMs;
    private long _summonDueMs = -1;
    private int _guardMs;
    private int _healTimerMs;
    private int _skillTimerMs;

    public BossLife(BossLifeProfile profile, BossEventOutbox events, Action<int, int> resistSink)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(resistSink);
        _profile = profile;
        _events = events;
        _resistSink = resistSink;
        _barsLeft = profile.LifeCount;
    }

    public BossLifeProfile Profile => _profile;

    public int BarsLeft => Volatile.Read(ref _barsLeft);

    public bool Exhausted => BarsLeft == 0;

    public bool Guarded => _guardMs > 0;

    internal void RecordElementDamage(int elementIndex, long damage)
    {
        if ((uint)elementIndex >= ElementCount || Guarded) return;
        _damageByElement[elementIndex] += damage;
    }

    internal void Absorb(ref long hp, long maxHp, long damage, MonsterEntity owner)
    {
        hp -= damage;
        while (hp < 0)
        {
            if (_barsLeft < 1)
            {
                hp = 0;
                break;
            }
            hp += maxHp;
            _barsLeft--;
            GrowResist(owner);
            _guardMs = BarLossGuardMs;
        }
    }

    internal void ReportDeath(MonsterEntity owner) =>
        _events.Enqueue(new BossEvent(BossEventKind.Died, owner.EntityId, owner.MapId, 0, _profile.NotEarthQuake, string.Empty));

    internal void RefillWhenIdle(int elapsedMs, bool engaged)
    {
        if (engaged)
        {
            _idleMs = 0;
            return;
        }
        _idleMs += elapsedMs;
        if (_idleMs < IdleRefillMs) return;
        _idleMs = 0;
        if (_barsLeft >= _profile.LifeCount) return;
        _barsLeft = _profile.LifeCount;
        Array.Clear(_damageByElement);
        for (var element = 0; element < ElementCount; element++)
        {
            if (_growth[element] == 0) continue;
            _resistSink(element, -_growth[element]);
            _growth[element] = 0;
        }
    }

    internal bool TickSummon(MonsterEntity owner, long nowMs, int elapsedMs, bool engaged, int mapMonsterCount, int childCount, bool evenRoll)
    {
        var called = false;
        if (_summonDueMs >= 0 && nowMs >= _summonDueMs)
        {
            _summonDueMs = -1;
            called = true;
            _events.Enqueue(new BossEvent(
                BossEventKind.SummonCalled, owner.EntityId, owner.MapId, 0, _profile.NotEarthQuake, string.Empty));
        }
        _summonTimerMs -= elapsedMs;
        if (_summonTimerMs >= 0) return called;
        _summonTimerMs = _profile.SummonIntervalMs;
        if (evenRoll && engaged && BarsLeft <= _profile.SummonLeftLife
            && mapMonsterCount < SummonMapMonsterLimit && childCount < _profile.SummonTotalCount && _summonDueMs < 0)
        {
            _summonDueMs = nowMs + SummonAnnounceDelayMs;
        }
        return called;
    }

    internal void TickTimers(MonsterEntity owner, int elapsedMs, bool engaged, bool hpBelowMax, bool castsSkills, Func<int> roll)
    {
        if (_guardMs > 0) _guardMs = Math.Max(0, _guardMs - elapsedMs);
        TickHeal(owner, elapsedMs, hpBelowMax, roll);
        if (castsSkills) TickSkills(owner, elapsedMs, engaged, roll);
    }

    public ushort OwnerEntityId { get; set; }

    private void TickHeal(MonsterEntity owner, int elapsedMs, bool hpBelowMax, Func<int> roll)
    {
        _healTimerMs -= elapsedMs;
        if (_healTimerMs >= 0 || _profile.Heals is not { } heals) return;
        var fired = 0;
        for (var slot = 0; slot < heals.Count; slot++)
        {
            var heal = heals[slot];
            if (BarsLeft > heal.LeftLife || !hpBelowMax || heal.Level == 0 || (roll() & 1) == 0) continue;
            _events.Enqueue(new BossEvent(
                BossEventKind.HealCast, owner.EntityId, owner.MapId, 0, _profile.NotEarthQuake, string.Empty,
                SkillId: HealSkillId, SkillLevel: heal.Level));
            fired = slot;
            break;
        }
        _healTimerMs = heals.Count > 0 ? heals[fired].TimeMs : 0;
    }

    private void TickSkills(MonsterEntity owner, int elapsedMs, bool engaged, Func<int> roll)
    {
        _skillTimerMs -= elapsedMs;
        if (_skillTimerMs >= 0 || _profile.Skills is not { } skills) return;
        var fired = 0;
        for (var slot = 0; slot < skills.Count; slot++)
        {
            var skill = skills[slot];
            if (BarsLeft > skill.LeftLife || !engaged || skill.Level == 0 || roll() % SkillRollModulus != 0) continue;
            fired = slot;
            _events.Enqueue(new BossEvent(
                BossEventKind.SkillCast, owner.EntityId, owner.MapId, 0, _profile.NotEarthQuake, string.Empty,
                SkillId: skill.SkillId, SkillLevel: skill.Level));
            break;
        }
        _skillTimerMs = skills.Count > 0 ? skills[fired].TimeMs : 0;
    }

    private void GrowResist(MonsterEntity owner)
    {
        if (_profile.ResistGrowthMin == 0 && _profile.ResistGrowthMax == 0) return;
        var leading = 0;
        var snapshot = (long[])_damageByElement.Clone();
        Array.Clear(_damageByElement);
        for (var element = 0; element < ElementCount; element++)
        {
            if (snapshot[leading] < snapshot[element]) leading = element;
            Grow(element, _profile.ResistGrowthMin);
        }
        Grow(leading, _profile.ResistGrowthMax - _profile.ResistGrowthMin);
        _events.Enqueue(new BossEvent(
            BossEventKind.ResistGrown, owner.EntityId, owner.MapId, (ushort)(ElementWireBase + leading),
            _profile.NotEarthQuake, _profile.ResistGrowthSpeech));
    }

    private void Grow(int element, int amount)
    {
        _growth[element] += amount;
        _resistSink(element, amount);
    }
}
