namespace tPoseidon.Core.Data;

public sealed class InMemoryUniqueItemStore : IUniqueItemStore
{
    private readonly object _gate = new();
    private readonly Dictionary<(ulong CharUid, int MonsterId), DateTimeOffset> _bossKills = new();
    private readonly Dictionary<int, UniqueItemInfo> _info;
    private readonly Dictionary<ulong, UniqueHeldItem> _held = new();
    private readonly Dictionary<ulong, ulong> _holder = new();
    private readonly Dictionary<ulong, (int Medals, int Count, DateTimeOffset End)> _duties = new();
    private readonly Dictionary<int, int> _targets = new();
    private readonly Func<int, int> _roll;

    public InMemoryUniqueItemStore(IEnumerable<UniqueItemInfo> info)
        : this(info, Random.Shared.Next)
    {
    }

    public InMemoryUniqueItemStore(IEnumerable<UniqueItemInfo> info, Func<int, int> roll)
    {
        ArgumentNullException.ThrowIfNull(info);
        _roll = roll ?? throw new ArgumentNullException(nameof(roll));
        _info = info.ToDictionary(row => row.ItemIndex);
    }

    public IReadOnlyList<UniqueItemInfo> MasterRestart()
    {
        lock (_gate)
        {
            foreach (var row in _info.Values.ToList())
            {
                if (row.UpdateFlag == UniqueItemRules.Removed) _info.Remove(row.ItemIndex);
                else if (row.ServerCount > row.UserCount) _info[row.ItemIndex] = row with { ServerCount = row.UserCount };
            }
            return _info.Values.ToList();
        }
    }

    public bool AddCount(int itemIndex, int units, DateTimeOffset now)
    {
        lock (_gate)
        {
            if (!_info.TryGetValue(itemIndex, out var row) || row.UpdateFlag == UniqueItemRules.Removed) return false;
            var current = row.CurrentCount + units;
            if (!_targets.TryGetValue(itemIndex, out var target))
            {
                _targets[itemIndex] = UniqueItemRules.RandomTarget(row.BaseCount, _roll);
                _info[itemIndex] = row with { CurrentCount = current };
                return false;
            }
            if (target > current)
            {
                _info[itemIndex] = row with { CurrentCount = current };
                return false;
            }
            _targets[itemIndex] = UniqueItemRules.RandomTarget(row.BaseCount, _roll);
            var issued = row with { TotalCount = row.TotalCount + 1, ServerCount = row.ServerCount + 1, UpdateFlag = UniqueItemRules.Modified, CurrentCount = current };
            _info[itemIndex] = issued.LimitCount > issued.ServerCount ? issued with { CurrentCount = 0 } : issued;
            return true;
        }
    }

    public int? TargetOf(int itemIndex)
    {
        lock (_gate) return _targets.TryGetValue(itemIndex, out var target) ? target : null;
    }

    public UniquePickup? PickedUp(ulong charUid, ulong itemUid, int itemIndex, DateTimeOffset now)
    {
        lock (_gate)
        {
            if (!_info.TryGetValue(itemIndex, out var row)) return null;
            _info[itemIndex] = row with { UserCount = row.UserCount + 1 };
            if (!UniqueItemRules.CarriesDuty(row.UniqueType)) return new UniquePickup(row.UniqueType, null);
            var end = now + UniqueItemRules.DutyPeriod;
            _duties[charUid] = _duties.TryGetValue(charUid, out var duty) ? (duty.Medals, duty.Count + 1, end) : (0, 1, end);
            _held[itemUid] = new UniqueHeldItem(itemUid, itemIndex);
            _holder[itemUid] = charUid;
            return new UniquePickup(row.UniqueType, end);
        }
    }

    public bool BossKilled(ulong charUid, int monsterId, DateTimeOffset now)
    {
        lock (_gate)
        {
            _bossKills[(charUid, monsterId)] = now;
            if (!_duties.TryGetValue(charUid, out var duty) || !UniqueItemRules.SealCredited(monsterId, duty.End, now)) return false;
            _duties[charUid] = (duty.Medals + 1, duty.Count, duty.End);
            return true;
        }
    }

    public IReadOnlyList<BossKillRecord> LatestBossKills(ulong charUid)
    {
        lock (_gate)
        {
            return _bossKills.Where(entry => entry.Key.CharUid == charUid)
                .Select(entry => new BossKillRecord(entry.Key.MonsterId, entry.Value))
                .ToList();
        }
    }

    public UniqueDutyOutcome ApplyDuty(bool deprive, DateTimeOffset now)
    {
        lock (_gate)
        {
            var accomplished = new List<(ulong, DateTimeOffset)>();
            var failed = new List<ulong>();
            foreach (var (charUid, duty) in _duties.Where(entry => entry.Value.End <= now).ToList())
            {
                if (UniqueItemRules.DutyAccomplished(duty.Medals, duty.Count))
                {
                    var end = now + UniqueItemRules.DutyPeriod;
                    _duties[charUid] = (0, duty.Count, end);
                    accomplished.Add((charUid, end));
                    continue;
                }
                if (deprive) DepriveAllLocked(charUid);
                failed.Add(charUid);
            }
            return new UniqueDutyOutcome(accomplished, failed);
        }
    }

    public IReadOnlyList<UniqueHeldItem> DepriveAll(ulong charUid)
    {
        lock (_gate) return DepriveAllLocked(charUid);
    }

    public IReadOnlyList<UniqueHeldItem> DepriveSingle(ulong charUid, IReadOnlyList<UniqueHeldItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        lock (_gate)
        {
            var taken = items.Take(UniqueItemRules.MaxWithdrawnPerRequest)
                .Where(item => _held.TryGetValue(item.ItemUid, out var held) && held.ItemIndex == item.ItemIndex && _holder[item.ItemUid] == charUid)
                .Distinct()
                .ToList();
            foreach (var item in taken) Forget(item);
            if (taken.Count > 0 && _duties.TryGetValue(charUid, out var duty))
            {
                if (duty.Count > taken.Count) _duties[charUid] = (duty.Medals, duty.Count - taken.Count, duty.End);
                else _duties.Remove(charUid);
            }
            return taken;
        }
    }

    public IReadOnlyList<UniqueHeldItem> HeldBy(ulong charUid)
    {
        lock (_gate) return _held.Values.Where(item => _holder[item.ItemUid] == charUid).ToList();
    }

    public bool HasDuty(ulong charUid)
    {
        lock (_gate) return _duties.ContainsKey(charUid);
    }

    private List<UniqueHeldItem> DepriveAllLocked(ulong charUid)
    {
        var held = _held.Values.Where(item => _holder[item.ItemUid] == charUid).ToList();
        foreach (var item in held) Forget(item);
        if (held.Count > 0) _duties.Remove(charUid);
        return held;
    }

    private void Forget(UniqueHeldItem item)
    {
        _held.Remove(item.ItemUid);
        _holder.Remove(item.ItemUid);
        if (_info.TryGetValue(item.ItemIndex, out var row))
        {
            _info[item.ItemIndex] = row with { ServerCount = row.ServerCount - 1, UserCount = row.UserCount - 1 };
        }
    }
}
