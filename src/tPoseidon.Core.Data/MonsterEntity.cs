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
    private int _hp;
    private ushort _curX;
    private ushort _curY;

    public const int StatCount = 12;

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
    public required int MaxHp { get; init; }
    public required int Level { get; init; }

    public ushort SpawnPointId { get; init; }
    public int RegionId { get; init; }
    public bool IsBoss { get; init; }
    public MonsterAType Atype { get; init; } = MonsterAType.HarconMonster;
    public int AiViewRange { get; init; }
    public DateTimeOffset SpawnedAt { get; init; } = DateTimeOffset.UtcNow;

        public DateTimeOffset NextMoveAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset NextStrikeAt { get; set; }

    private DateTimeOffset _heldUntil;
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

    public bool TryAnnounceFirst001E(ushort attackerEntityId)
    {
        if (attackerEntityId == 0) return false;
        lock (_attackerLock)
        {
            return _announcedEngagements.Add(attackerEntityId);
        }
    }

    private ushort _moveFromX;
    private ushort _moveFromY;
    private DateTimeOffset _moveStartedAt;
    private DateTimeOffset _moveEndsAt;
    private (int X, int Y)[] _movePoints = [];
    private bool _pathCompleteAnnounced = true;

    public bool TryAnnouncePathComplete(DateTimeOffset now)
    {
        lock (_lock)
        {
            if (_pathCompleteAnnounced) return false;
            if (_moveEndsAt <= _moveStartedAt) return false;
            if (now < _moveEndsAt) return false;
            _pathCompleteAnnounced = true;
            return true;
        }
    }

    public ushort CurrentX
    {
        get { lock (_lock) return _curX; }
    }

    public ushort CurrentY
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
            _pathCompleteAnnounced = false;
            _curX = (ushort)Math.Clamp(toX, 0, ushort.MaxValue);
            _curY = (ushort)Math.Clamp(toY, 0, ushort.MaxValue);
            return (fromX, fromY);
        }
    }

    public int CurrentHp
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

    private ushort _ownerEntityId;
    private int _hpWhenOwned;

    public ushort OwnerEntityId
    {
        get { lock (_lock) return _ownerEntityId; }
    }

    public int HpWhenOwned
    {
        get { lock (_lock) return _hpWhenOwned; }
    }

    public void TakeOwner(ushort ownerEntityId, int hpWhenOwned)
    {
        lock (_lock)
        {
            _ownerEntityId = ownerEntityId;
            _hpWhenOwned = hpWhenOwned;
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
        get { lock (_lock) return _hp <= 0; }
    }

    public void InitHp(int initialHp)
    {
        lock (_lock) _hp = initialHp;
    }

    public void RaiseHp(int hp)
    {
        lock (_lock)
        {
            if (_hp <= 0) return;
            _hp = Math.Clamp(Math.Max(hp, _hp), 0, MaxHp);
        }
    }

    public int ApplyDamage(int amount)
    {
        if (amount <= 0) return 0;
        lock (_lock)
        {
            if (_hp <= 0) return 0;
            var actual = Math.Min(amount, _hp);
            _hp -= actual;
            return actual;
        }
    }
}
