namespace tPoseidon.Core.Data;

public sealed class InMemoryMemoryPortStore : IMemoryPortStore
{
    private readonly object _gate = new();
    private readonly Dictionary<(ulong Char, byte Type, byte Slot), MemoryPort> _entries = new();

    public MemoryPort Upsert(MemoryPort entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (_gate)
        {
            var record = entry with { CreatedAt = entry.CreatedAt == default ? DateTimeOffset.UtcNow : entry.CreatedAt };
            _entries[(entry.CharacterId, entry.PortType, entry.SlotNum)] = record;
            return record;
        }
    }

    public bool TryInsert(MemoryPort entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (_gate)
        {
            return _entries.TryAdd((entry.CharacterId, entry.PortType, entry.SlotNum), entry with { CreatedAt = DateTimeOffset.UtcNow });
        }
    }

    public MemoryPort? Find(ulong characterId, byte portType, byte slotNum)
    {
        lock (_gate)
        {
            return _entries.TryGetValue((characterId, portType, slotNum), out var rec) ? rec : null;
        }
    }

    public IReadOnlyList<MemoryPort> GetForCharacter(ulong characterId)
    {
        lock (_gate)
        {
            return _entries.Values.Where(e => e.CharacterId == characterId)
                .OrderBy(e => e.PortType)
                .ThenBy(e => e.SlotNum)
                .ToArray();
        }
    }

    public IReadOnlyList<MemoryPort> GetForCharacterByType(ulong characterId, byte portType)
    {
        lock (_gate)
        {
            return _entries.Values.Where(e => e.CharacterId == characterId && e.PortType == portType)
                .OrderBy(e => e.SlotNum)
                .ToArray();
        }
    }

    public bool Delete(ulong characterId, byte portType, byte slotNum)
    {
        lock (_gate)
        {
            return _entries.Remove((characterId, portType, slotNum));
        }
    }

    public int DeleteAllForCharacter(ulong characterId)
    {
        lock (_gate)
        {
            var keys = _entries.Keys.Where(k => k.Char == characterId).ToArray();
            foreach (var k in keys) _entries.Remove(k);
            return keys.Length;
        }
    }
}

public sealed class InMemoryPkInfoStore : IPkInfoStore
{
    private readonly object _gate = new();
    private readonly Dictionary<ulong, PkInfo> _byChar = new();
    private readonly List<PkInfo> _log = new();

    public PkInfo GetOrCreate(ulong characterId)
    {
        lock (_gate)
        {
            if (_byChar.TryGetValue(characterId, out var rec)) return rec;
            var fresh = new PkInfo { CharacterId = characterId, LatestUpdateAt = DateTimeOffset.UtcNow };
            _byChar[characterId] = fresh;
            return fresh;
        }
    }

    public PkInfo IncrementKill(ulong characterId)
    {
        lock (_gate)
        {
            var rec = _byChar.TryGetValue(characterId, out var existing)
                ? existing
                : new PkInfo { CharacterId = characterId };
            rec = rec with
            {
                KillTodayCount = rec.KillTodayCount + 1,
                KillTotalCount = rec.KillTotalCount + 1,
                LatestUpdateAt = DateTimeOffset.UtcNow,
            };
            _byChar[characterId] = rec;
            return rec;
        }
    }

    public PkInfo IncrementDeath(ulong characterId)
    {
        lock (_gate)
        {
            var rec = _byChar.TryGetValue(characterId, out var existing)
                ? existing
                : new PkInfo { CharacterId = characterId };
            rec = rec with
            {
                DeathTodayCount = rec.DeathTodayCount + 1,
                DeathTotalCount = rec.DeathTotalCount + 1,
                LatestUpdateAt = DateTimeOffset.UtcNow,
            };
            _byChar[characterId] = rec;
            return rec;
        }
    }

    public PkInfo Save(PkInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        lock (_gate)
        {
            var saved = info with { LatestUpdateAt = DateTimeOffset.UtcNow };
            _byChar[info.CharacterId] = saved;
            return saved;
        }
    }

    public bool ResetTodayCounts(ulong characterId)
    {
        lock (_gate)
        {
            if (!_byChar.TryGetValue(characterId, out var rec)) return false;
            _byChar[characterId] = rec with
            {
                KillTodayCount = 0,
                DeathTodayCount = 0,
                LatestUpdateAt = DateTimeOffset.UtcNow,
            };
            return true;
        }
    }

    public int ResetAllTodayCounts()
    {
        lock (_gate)
        {
            var keys = _byChar.Keys.ToArray();
            foreach (var k in keys)
            {
                _byChar[k] = _byChar[k] with
                {
                    KillTodayCount = 0,
                    DeathTodayCount = 0,
                    LatestUpdateAt = DateTimeOffset.UtcNow,
                };
            }
            return keys.Length;
        }
    }

    public void AppendLog(PkInfo snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_gate) { _log.Add(snapshot); }
    }

    public IReadOnlyList<PkInfo> GetLog(ulong characterId, int limit = 50)
    {
        lock (_gate)
        {
            return _log.Where(e => e.CharacterId == characterId)
                .OrderByDescending(e => e.LatestUpdateAt)
                .Take(limit)
                .ToArray();
        }
    }
}

public sealed class InMemoryUniqueCharDutyInfoStore : IUniqueCharDutyInfoStore
{
    private readonly object _gate = new();
    private readonly Dictionary<ulong, UniqueCharDutyInfo> _byChar = new();

    public UniqueCharDutyInfo GetOrCreate(ulong characterId)
    {
        lock (_gate)
        {
            if (_byChar.TryGetValue(characterId, out var rec)) return rec;
            var fresh = new UniqueCharDutyInfo { CharacterId = characterId };
            _byChar[characterId] = fresh;
            return fresh;
        }
    }

    public UniqueCharDutyInfo Upsert(UniqueCharDutyInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        lock (_gate)
        {
            _byChar[info.CharacterId] = info;
            return info;
        }
    }

    public bool Delete(ulong characterId)
    {
        lock (_gate) { return _byChar.Remove(characterId); }
    }
}

public sealed class InMemoryCharProfileStore : ICharProfileStore
{
    private readonly object _gate = new();
    private readonly Dictionary<ulong, CharProfile> _byChar = new();

    public CharProfile GetOrCreate(ulong characterId)
    {
        lock (_gate)
        {
            if (_byChar.TryGetValue(characterId, out var rec)) return rec;
            var fresh = new CharProfile { CharacterId = characterId, UpdatedAt = DateTimeOffset.UtcNow };
            _byChar[characterId] = fresh;
            return fresh;
        }
    }

    public CharProfile Upsert(CharProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        lock (_gate)
        {
            var record = profile with { UpdatedAt = DateTimeOffset.UtcNow };
            _byChar[profile.CharacterId] = record;
            return record;
        }
    }

    public bool Delete(ulong characterId)
    {
        lock (_gate) { return _byChar.Remove(characterId); }
    }
}
