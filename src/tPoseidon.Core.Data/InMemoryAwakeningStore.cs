namespace tPoseidon.Core.Data;

public sealed class InMemoryAwakeningStore : IAwakeningStore
{
    private readonly object _gate = new();
    private readonly Dictionary<ulong, CharAwakening> _byChar = new();
    private readonly List<CharAwakeningLevelUp> _levelUpLog = new();

    public CharAwakening GetOrCreate(ulong characterId)
    {
        lock (_gate)
        {
            if (_byChar.TryGetValue(characterId, out var rec)) return rec;
            var fresh = new CharAwakening { CharacterId = characterId, CreatedAt = DateTimeOffset.UtcNow };
            _byChar[characterId] = fresh;
            return fresh;
        }
    }

    public CharAwakening Upsert(CharAwakening awakening)
    {
        ArgumentNullException.ThrowIfNull(awakening);
        lock (_gate)
        {
            _byChar[awakening.CharacterId] = awakening;
            return awakening;
        }
    }

    public bool MarkLevelUp(ulong characterId, int charLevel)
    {
        lock (_gate)
        {
            if (_levelUpLog.Any(e => e.CharacterId == characterId && e.CharLevel == charLevel)) return false;
            _levelUpLog.Add(new CharAwakeningLevelUp
            {
                CharacterId = characterId,
                CharLevel = charLevel,
                LevelUpAt = DateTimeOffset.UtcNow,
            });
            return true;
        }
    }

    public IReadOnlyList<CharAwakeningLevelUp> GetLevelUpLog(ulong characterId)
    {
        lock (_gate)
        {
            return _levelUpLog.Where(e => e.CharacterId == characterId)
                .OrderBy(e => e.CharLevel)
                .ToArray();
        }
    }

    public bool Delete(ulong characterId)
    {
        lock (_gate) { return _byChar.Remove(characterId); }
    }
}

public sealed class InMemorySoulGuardianStore : ISoulGuardianStore
{
    private readonly object _gate = new();
    private readonly Dictionary<ulong, CharSoulGuardian> _guardians = new();
    private readonly Dictionary<(ulong CharId, byte Slot), CharSoulGuardianSlot> _slots = new();
    private ulong _nextGuardianUid = 1;

    public CharSoulGuardian CreateGuardian(ulong characterId, short guardianType, byte slotNum, string name, byte fType)
    {
        ArgumentNullException.ThrowIfNull(name);
        lock (_gate)
        {
            var guardian = new CharSoulGuardian
            {
                GuardianUid = _nextGuardianUid++,
                CharacterId = characterId,
                GuardianType = guardianType,
                SlotNum = slotNum,
                GuardianName = name,
                FType = fType,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            _guardians[guardian.GuardianUid] = guardian;
            return guardian;
        }
    }

    public CharSoulGuardian? FindGuardian(ulong guardianUid)
    {
        lock (_gate) { return _guardians.TryGetValue(guardianUid, out var g) ? g : null; }
    }

    public IReadOnlyList<CharSoulGuardian> GetGuardiansForCharacter(ulong characterId)
    {
        lock (_gate)
        {
            return _guardians.Values.Where(g => g.CharacterId == characterId)
                .OrderBy(g => g.GuardianUid)
                .ToArray();
        }
    }

    public CharSoulGuardian UpdateGuardian(CharSoulGuardian guardian)
    {
        ArgumentNullException.ThrowIfNull(guardian);
        lock (_gate)
        {
            _guardians[guardian.GuardianUid] = guardian;
            return guardian;
        }
    }

    public bool DeleteGuardian(ulong guardianUid)
    {
        lock (_gate)
        {
            if (!_guardians.Remove(guardianUid)) return false;
            var slotKeys = _slots.Where(kv => kv.Value.GuardianUid == guardianUid).Select(kv => kv.Key).ToArray();
            foreach (var k in slotKeys) _slots[k] = _slots[k] with { GuardianUid = null };
            return true;
        }
    }

    public void SetSlot(ulong characterId, byte slotNum, ulong? guardianUid)
    {
        lock (_gate)
        {
            _slots[(characterId, slotNum)] = new CharSoulGuardianSlot
            {
                CharacterId = characterId,
                SlotNum = slotNum,
                GuardianUid = guardianUid,
            };
        }
    }

    public IReadOnlyList<CharSoulGuardianSlot> GetSlots(ulong characterId)
    {
        lock (_gate)
        {
            return _slots.Values.Where(s => s.CharacterId == characterId)
                .OrderBy(s => s.SlotNum)
                .ToArray();
        }
    }
}

public sealed class InMemorySoulSeedStore : ISoulSeedStore
{
    private readonly object _gate = new();
    private readonly Dictionary<ulong, CharSoulSeed> _byChar = new();

    public CharSoulSeed GetOrCreate(ulong characterId, int seedItemIndex)
    {
        lock (_gate)
        {
            if (_byChar.TryGetValue(characterId, out var rec)) return rec;
            var now = DateTimeOffset.UtcNow;
            var fresh = new CharSoulSeed
            {
                CharacterId = characterId,
                SeedItemIndex = seedItemIndex,
                StartAt = now,
                LastUpdateAt = now,
            };
            _byChar[characterId] = fresh;
            return fresh;
        }
    }

    public CharSoulSeed Upsert(CharSoulSeed soulSeed)
    {
        ArgumentNullException.ThrowIfNull(soulSeed);
        lock (_gate)
        {
            var record = soulSeed with { LastUpdateAt = DateTimeOffset.UtcNow };
            _byChar[soulSeed.CharacterId] = record;
            return record;
        }
    }

    public bool Delete(ulong characterId)
    {
        lock (_gate) { return _byChar.Remove(characterId); }
    }
}
