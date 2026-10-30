namespace tPoseidon.Core.Data;

public sealed class MonsterEntity
{
    private readonly object _lock = new();
    private readonly Dictionary<int, int> _resistLoss = new();

    public int ResistLoss(int element)
    {
        lock (_lock) return _resistLoss.GetValueOrDefault(element);
    }

    public void AddResistLoss(int element, int amount)
    {
        lock (_lock)
        {
            var left = _resistLoss.GetValueOrDefault(element) + amount;
            if (left == 0) _resistLoss.Remove(element);
            else _resistLoss[element] = left;
        }
    }
    private long _hp;
    private ushort _curX;
    private ushort _curY;

    public const int StatCount = 12;
    public const int FullStatPercent = 100;

    private int _statScalePercent = FullStatPercent;

    public int StatScalePercent
    {
        get => Volatile.Read(ref _statScalePercent);
        set => Volatile.Write(ref _statScalePercent, value);
    }

    private int[] _statDeltas = new int[StatCount];

    public int StatDelta(int stat) => (uint)stat < StatCount ? Volatile.Read(ref _statDeltas)[stat] : 0;

    public void SetStatDeltas(IReadOnlyList<int> deltas)
    {
        ArgumentNullException.ThrowIfNull(deltas);
        if (deltas.Count != StatCount) throw new ArgumentException($"Expected {StatCount} stat deltas.", nameof(deltas));
        Volatile.Write(ref _statDeltas, deltas.ToArray());
    }

    public required ushort EntityId { get; init; }
    public required ushort MapId { get; init; }
    public required int MonsterParamId { get; init; }
    public required string Name { get; init; }
    public required ushort SpawnX { get; init; }
    public required ushort SpawnY { get; init; }
    public required long MaxHp { get; init; }
    public Func<long>? DamageLimit { get; init; }
    public IMonsterDamageObserver? DamageObserver { get; init; }
    public required int Level { get; init; }

    public ushort SpawnPointId { get; init; }
    public int RegionId { get; init; }
    public ushort InstanceKey { get; init; }
    public bool IsBoss { get; init; }
    public MonsterAType Atype { get; init; } = MonsterAType.HarconMonster;
    public int AiViewRange { get; init; }

    private DateTimeOffset _heldUntil;
    private DateTimeOffset _rootedUntil;
    private float _slowRatio;

    public const float WalkSlowStep = 100f;
    public const float RunSlowStep = 160f;

    public float SlowRatio
    {
        get { lock (_lock) return _slowRatio; }
        set { lock (_lock) _slowRatio = value; }
    }

    public float WalkSpeedOf(float tableWalkSpeed)
    {
        lock (_lock) return tableWalkSpeed - WalkSlowStep * _slowRatio;
    }

    public float RunSpeedOf(float tableRunSpeed)
    {
        lock (_lock) return tableRunSpeed - RunSlowStep * _slowRatio;
    }

    public bool IsHeld(DateTimeOffset now)
    {
        lock (_lock) return now < _heldUntil;
    }

    public bool IsRooted(DateTimeOffset now)
    {
        lock (_lock) return now < _heldUntil || now < _rootedUntil;
    }

    public (ushort X, ushort Y) RootUntil(DateTimeOffset until, DateTimeOffset now)
    {
        lock (_lock)
        {
            if (until > _rootedUntil) _rootedUntil = until;
            var (x, y) = GetInterpolatedPosition(now);
            InitPosition(x, y);
            return (x, y);
        }
    }

    public (ushort X, ushort Y) HoldUntil(DateTimeOffset until, DateTimeOffset now)
    {
        lock (_lock)
        {
            if (until > _heldUntil) _heldUntil = until;
            var (x, y) = GetInterpolatedPosition(now);
            InitPosition(x, y);
            return (x, y);
        }
    }

    private readonly HashSet<ushort> _attackerIds = new();
    private readonly HashSet<ushort> _announcedEngagements = new();
    private readonly object _attackerLock = new();
    private ushort _lastAttackerEntityId;

    public bool IsInCombat
    {
        get { lock (_attackerLock) return _attackerIds.Count > 0; }
    }

    public ushort PrimaryAttackerEntityId
    {
        get
        {
            lock (_attackerLock)
            {
                if (_lastAttackerEntityId != 0 && _attackerIds.Contains(_lastAttackerEntityId))
                    return _lastAttackerEntityId;
                foreach (var id in _attackerIds) return id;
                return 0;
            }
        }
    }

    public IReadOnlyCollection<ushort> SnapshotAttackers()
    {
        lock (_attackerLock) return _attackerIds.ToArray();
    }

    public bool AddAttacker(ushort attackerEntityId)
    {
        if (attackerEntityId == 0) return false;
        lock (_attackerLock)
        {
            _lastAttackerEntityId = attackerEntityId;
            return _attackerIds.Add(attackerEntityId);
        }
    }

    public bool RemoveAttacker(ushort attackerEntityId)
    {
        lock (_attackerLock)
        {
            if (_lastAttackerEntityId == attackerEntityId) _lastAttackerEntityId = 0;
            _announcedEngagements.Remove(attackerEntityId);
            return _attackerIds.Remove(attackerEntityId);
        }
    }

    public void ClearAttackers()
    {
        lock (_attackerLock)
        {
            _attackerIds.Clear();
            _announcedEngagements.Clear();
            _lastAttackerEntityId = 0;
        }
    }

    private ushort _moveFromX;
    private ushort _moveFromY;
    private DateTimeOffset _moveStartedAt;
    private DateTimeOffset _moveEndsAt;
    private (int X, int Y)[] _movePoints = [];

    public ushort DestinationX
    {
        get { lock (_lock) return _curX; }
    }

    public ushort DestinationY
    {
        get { lock (_lock) return _curY; }
    }

    public (ushort x, ushort y) GetInterpolatedPosition(DateTimeOffset now)
    {
        lock (_lock)
        {
            if (_moveEndsAt <= _moveStartedAt) return (_curX, _curY);
            var totalMs = (_moveEndsAt - _moveStartedAt).TotalMilliseconds;
            var elapsedMs = (now - _moveStartedAt).TotalMilliseconds;
            if (elapsedMs >= totalMs) return (_curX, _curY);
            if (elapsedMs <= 0) return (_moveFromX, _moveFromY);
            var left = elapsedMs / totalMs * PathLength(_moveFromX, _moveFromY, _movePoints);
            double lastX = _moveFromX;
            double lastY = _moveFromY;
            foreach (var (pointX, pointY) in _movePoints)
            {
                var leg = Distance(lastX, lastY, pointX, pointY);
                if (left < leg)
                {
                    var part = left / leg;
                    return (
                        (ushort)Math.Clamp((int)(lastX + (pointX - lastX) * part), 0, ushort.MaxValue),
                        (ushort)Math.Clamp((int)(lastY + (pointY - lastY) * part), 0, ushort.MaxValue));
                }
                left -= leg;
                lastX = pointX;
                lastY = pointY;
            }
            return (_curX, _curY);
        }
    }

    private static double PathLength(int fromX, int fromY, IReadOnlyList<(int X, int Y)> points)
    {
        var length = 0.0;
        double lastX = fromX;
        double lastY = fromY;
        foreach (var (pointX, pointY) in points)
        {
            length += Distance(lastX, lastY, pointX, pointY);
            lastX = pointX;
            lastY = pointY;
        }
        return length;
    }

    private static double Distance(double x1, double y1, double x2, double y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public void InitPosition(ushort x, ushort y)
    {
        lock (_lock)
        {
            _curX = x;
            _curY = y;
            _moveFromX = x;
            _moveFromY = y;
            _movePoints = [];
            _moveStartedAt = default;
            _moveEndsAt = default;
        }
    }

    public (ushort fromX, ushort fromY) MoveTo(ushort x, ushort y, DateTimeOffset now, int durationMs = 1000) =>
        MoveAlong([(x, y)], now, durationMs);

    public (ushort fromX, ushort fromY) MoveAlong(IReadOnlyList<(int X, int Y)> points, DateTimeOffset now, int durationMs)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count == 0) throw new ArgumentException("A move needs at least one point.", nameof(points));
        lock (_lock)
        {
            var (fromX, fromY) = GetInterpolatedPosition(now);
            var (toX, toY) = points[^1];
            _moveFromX = fromX;
            _moveFromY = fromY;
            _movePoints = points.ToArray();
            _moveStartedAt = now;
            _moveEndsAt = now.AddMilliseconds(durationMs);
            _curX = (ushort)Math.Clamp(toX, 0, ushort.MaxValue);
            _curY = (ushort)Math.Clamp(toY, 0, ushort.MaxValue);
            return (fromX, fromY);
        }
    }

    public long CurrentHp
    {
        get { lock (_lock) return _hp; }
    }

    private float _speed;
    private byte _moveType;

    public float Speed
    {
        get { lock (_lock) return _speed; }
    }

    public byte MoveType
    {
        get { lock (_lock) return _moveType; }
    }

    public void InitSpeed(float speed)
    {
        lock (_lock) _speed = speed;
    }

    public bool TakeOrder(byte moveType, float speed)
    {
        lock (_lock)
        {
            _moveType = moveType;
            if (_speed == speed) return false;
            _speed = speed;
            return true;
        }
    }

    public const ushort NoOwner = 0;

    public byte PvpMode { get; set; }

    private readonly Dictionary<ushort, long> _damageByAttacker = new();
    private ushort _ownerEntityId;
    private long _hpWhenOwned;

    public ushort OwnerEntityId
    {
        get { lock (_lock) return _ownerEntityId; }
    }

    public long HpWhenOwned
    {
        get { lock (_lock) return _hpWhenOwned; }
    }

    public void TakeOwner(ushort ownerEntityId, long hpWhenOwned)
    {
        lock (_lock)
        {
            _ownerEntityId = ownerEntityId;
            _hpWhenOwned = hpWhenOwned;
        }
    }

    public void RecordDamage(ushort attackerEntityId, long amount)
    {
        if (amount <= 0) return;
        lock (_lock)
        {
            _damageByAttacker[attackerEntityId] = _damageByAttacker.GetValueOrDefault(attackerEntityId) + amount;
        }
    }

    public IReadOnlyList<ushort> TopAttackers(int most)
    {
        lock (_lock)
        {
            return _damageByAttacker
                .OrderByDescending(entry => entry.Value)
                .Take(most)
                .Select(entry => entry.Key)
                .ToArray();
        }
    }

    public bool ReleaseOwner(ushort ownerEntityId)
    {
        lock (_lock)
        {
            if (_ownerEntityId == NoOwner || _ownerEntityId != ownerEntityId) return false;
            _ownerEntityId = NoOwner;
            return true;
        }
    }

    public bool IsDead
    {
        get { lock (_lock) return DeadLocked; }
    }

    public BossLife? Boss { get; private set; }

    public ushort HpBarsField => (ushort)(Boss?.BarsLeft ?? 0);

    public void AttachBoss(BossLifeProfile profile, BossEventOutbox events, Func<int, int> elementCodeOf)
    {
        ArgumentNullException.ThrowIfNull(elementCodeOf);
        Boss = new BossLife(profile, events, (index, amount) => AddResistLoss(elementCodeOf(index), -amount));
    }

    public void RecordElementDamage(int elementIndex, long damage)
    {
        lock (_lock) Boss?.RecordElementDamage(elementIndex, damage);
    }

    public bool TickBoss(long nowMs, int elapsedMs, bool engaged, int mapMonsterCount, int childCount, bool evenRoll)
    {
        lock (_lock)
        {
            if (Boss is not { } boss) return false;
            boss.RefillWhenIdle(elapsedMs, engaged);
            return boss.TickSummon(this, nowMs, elapsedMs, engaged, mapMonsterCount, childCount, evenRoll);
        }
    }

    public bool Guarded
    {
        get { lock (_lock) return Boss?.Guarded ?? false; }
    }

    public void TickBossTimers(int elapsedMs, bool engaged, bool castsSkills, Func<int> roll)
    {
        ArgumentNullException.ThrowIfNull(roll);
        lock (_lock) Boss?.TickTimers(this, elapsedMs, engaged, _hp < MaxHp, castsSkills, roll);
    }

    private bool DeadLocked => _hp <= 0 && (Boss?.Exhausted ?? true);

    private long ApplyBossDamage(BossLife boss, long amount)
    {
        var actual = Math.Min(amount, _hp + boss.BarsLeft * MaxHp);
        boss.Absorb(ref _hp, MaxHp, actual, this);
        if (DeadLocked) boss.ReportDeath(this);
        return actual;
    }

    public void InitHp(long initialHp)
    {
        lock (_lock) _hp = initialHp;
    }

    public void RaiseHp(long hp)
    {
        lock (_lock)
        {
            if (_hp <= 0) return;
            _hp = Math.Clamp(Math.Max(hp, _hp), 0L, MaxHp);
        }
    }

    public long ApplyDamage(long amount)
    {
        if (amount <= 0) return 0;
        if (DamageLimit is { } limit) amount = Math.Min(amount, limit());
        long actual;
        lock (_lock)
        {
            if (DeadLocked) return 0;
            if (Boss is { } boss)
            {
                if (boss.Guarded) return 0;
                actual = ApplyBossDamage(boss, amount);
            }
            else
            {
                actual = Math.Min(amount, _hp);
                _hp -= actual;
            }
        }
        DamageObserver?.Damaged(this, actual);
        return actual;
    }

    public long Heal(long amount)
    {
        lock (_lock)
        {
            if (_hp <= 0) return 0;
            _hp = Math.Min(_hp + amount, MaxHp);
            return _hp;
        }
    }
}
