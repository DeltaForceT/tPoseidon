namespace tPoseidon.Core.Data;

public sealed class InMemoryCoupleStore : ICoupleStore
{
    private readonly object _gate = new();
    private readonly Dictionary<ulong, Couple> _couples = new();
    private readonly Dictionary<ulong, CoupleChar> _charRecords = new();
    private readonly Dictionary<(ulong CoupleCode, ulong CharId), CoupleJoin> _joins = new();
    private ulong _nextCoupleCode = 1;

    public Couple CreateCouple(ulong charAId, ulong charBId, bool charASex, bool charBSex)
    {
        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            var couple = new Couple
            {
                CoupleCode = _nextCoupleCode++,
                RecentUpdateAt = now,
                CreatedAt = now,
            };
            _couples[couple.CoupleCode] = couple;
            _joins[(couple.CoupleCode, charAId)] = new CoupleJoin
            {
                CoupleCode = couple.CoupleCode,
                CharacterId = charAId,
                CharSex = charASex,
            };
            _joins[(couple.CoupleCode, charBId)] = new CoupleJoin
            {
                CoupleCode = couple.CoupleCode,
                CharacterId = charBId,
                CharSex = charBSex,
            };
            foreach (var charId in new[] { charAId, charBId })
            {
                if (_charRecords.TryGetValue(charId, out var existing))
                {
                    _charRecords[charId] = existing with { CoupleCode = couple.CoupleCode };
                }
                else
                {
                    _charRecords[charId] = new CoupleChar
                    {
                        CharacterId = charId,
                        CoupleCode = couple.CoupleCode,
                        CreatedAt = now,
                    };
                }
            }
            return couple;
        }
    }

    public Couple? FindCouple(ulong coupleCode)
    {
        lock (_gate) { return _couples.TryGetValue(coupleCode, out var c) ? c : null; }
    }

    public Couple? FindCoupleForCharacter(ulong characterId)
    {
        lock (_gate)
        {
            if (!_charRecords.TryGetValue(characterId, out var rec)) return null;
            return _couples.TryGetValue(rec.CoupleCode, out var c) ? c : null;
        }
    }

    public Couple UpdateCoupleHeader(Couple couple)
    {
        ArgumentNullException.ThrowIfNull(couple);
        lock (_gate)
        {
            var record = couple with { RecentUpdateAt = DateTimeOffset.UtcNow };
            _couples[couple.CoupleCode] = record;
            return record;
        }
    }

    public bool DissolveCouple(ulong coupleCode)
    {
        lock (_gate)
        {
            if (!_couples.Remove(coupleCode)) return false;
            var keys = _joins.Keys.Where(k => k.CoupleCode == coupleCode).ToArray();
            foreach (var k in keys) _joins.Remove(k);
            return true;
        }
    }

    public CoupleChar? FindCharRecord(ulong characterId)
    {
        lock (_gate) { return _charRecords.TryGetValue(characterId, out var rec) ? rec : null; }
    }

    public CoupleChar IncrementCounter(ulong characterId, CoupleCharCounter counter)
    {
        lock (_gate)
        {
            var rec = _charRecords.TryGetValue(characterId, out var existing)
                ? existing
                : new CoupleChar { CharacterId = characterId, CoupleCode = 0, CreatedAt = DateTimeOffset.UtcNow };
            rec = counter switch
            {
                CoupleCharCounter.Couple => rec with { CoupleCount = (short)(rec.CoupleCount + 1) },
                CoupleCharCounter.CoupleDelete => rec with { CoupleDeleteCount = (short)(rec.CoupleDeleteCount + 1) },
                CoupleCharCounter.Marriage => rec with { MarriageCount = (short)(rec.MarriageCount + 1) },
                CoupleCharCounter.Divorce => rec with { DivorceCount = (short)(rec.DivorceCount + 1) },
                CoupleCharCounter.WeddingIn => rec with { WeddingInCount = (short)(rec.WeddingInCount + 1) },
                _ => rec,
            };
            _charRecords[characterId] = rec;
            return rec;
        }
    }

    public IReadOnlyList<CoupleJoin> GetJoins(ulong coupleCode)
    {
        lock (_gate)
        {
            return _joins.Values.Where(j => j.CoupleCode == coupleCode).ToArray();
        }
    }

    public bool UpdateDivorceState(ulong coupleCode, ulong characterId, byte divorceState)
    {
        lock (_gate)
        {
            if (!_joins.TryGetValue((coupleCode, characterId), out var j)) return false;
            _joins[(coupleCode, characterId)] = j with { DivorceState = divorceState };
            return true;
        }
    }

    public bool SetEventBitmask(ulong coupleCode, ulong characterId, long bitmask)
    {
        lock (_gate)
        {
            if (!_joins.TryGetValue((coupleCode, characterId), out var j)) return false;
            _joins[(coupleCode, characterId)] = j with { EventBitmask = bitmask };
            return true;
        }
    }
}
