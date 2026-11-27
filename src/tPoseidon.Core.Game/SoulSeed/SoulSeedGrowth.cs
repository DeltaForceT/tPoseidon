using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.SoulSeed;

public sealed record SoulSeedRecord(bool Planted, int SeedItemId, DateTimeOffset StartAt, DateTimeOffset LastUpdateAt, IReadOnlyList<short> SavedPoints);

public sealed record SoulSeedBuff(int Group, int MillisecondsLeft, int Stacks, IReadOnlyList<int> Values);

public sealed record SoulSeedEventOutcome(
    bool Processed,
    bool Granted,
    int Counter,
    bool Changed,
    bool BuffChanged,
    bool Complete,
    IReadOnlyList<SoulSeedRecord> Backups)
{
    public static SoulSeedEventOutcome Skipped { get; } = new(false, false, 0, false, false, false, Array.Empty<SoulSeedRecord>());
}

public static class SoulSeedRules
{
    public const int TypeCount = 8;
    public const int NormalTypes = 4;
    public const int PointsPerLevel = 1000;
    public const int BuffPointStep = 50;
    public const int HighestNormalLevel = 5;
    public const int HighestSpecialLevel = 1;
    public const int HighestSpecialLevelSum = 4;
    public const int CompleteLevel = 6;
    public const int CompleteLevelSum = 14;
    public const int EventTypeCount = 55;
    public const int EventMaskBits = 64;
    public const int BuffMilliseconds = 600000;
    public const int MaxBuffStacks = 5;
    public const int BuffValueSlots = 12;
    public const int NoBuffGroup = -1;
    public const double BuffRatioPerStack = 0.019999999552965164;
    public static readonly int[][] BuffStats =
    [
        [0, 1, 2],
        [4, 5, 3],
        [6, 7, 8],
        [9, 10, 11],
    ];

    public static int BuffValue(int baseStat, int stacks)
    {
        var ratio = (float)(stacks * BuffRatioPerStack);
        return (int)(baseStat * (double)ratio);
    }
    public const int SeedSubType = 72;
    public const int SummonGuardianSkill = 8003;
    public const int GuardianSlotsFullCode = 10;
    public const int MonsterKillEvent = 10;
    public const int MonsterKillLevelGap = 20;
    public const int LevelUpEvent = 0;
    public const int TmLevelUpEvent = 1;
    public const int StatPointEventBase = 2;
    public const int StatPointEventGroups = 4;
    public const int StatPointEventGroupSize = 3;
    public const int HitLevelGap = 20;
    public const int CriticalLandedEvent = 13;
    public const int HitMissedEvent = 14;
    public const int HitBlockedEvent = 15;
    public const int MissKind = 1;
    public const int BlockKind = 2;
    public const int CriticalKind = 3;
    public const int StrongCriticalKind = 4;
    public const uint KindMask = 0x7F;
    public const int BossKillEvent = 0x0B;
    public const int ItemUseEvent = 0x0C;
    public const int PlayerDeathEvent = 0x10;
    public const int DrillFoundEvent = 0x19;
    public const int DrillFailedEvent = 0x1A;
    public const int DrillEndedEvent = 0x1B;
    public const int MyCampOpenEvent = 0x23;
    public const int CrazyDrillStartEvent = 0x1C;
    public const int PlayerShopBuyEvent = 0x1E;
    public const int ProfileViewEvent = 0x1F;
    public const int MoneyPickupEvent = 0x16;
    public const int RefineEvent = 0x17;
    public const int GrowthCompoundEvent = 0x18;
    public const int ExchangeEvent = 0x21;
    public const int CompoundEvent = 0x22;
    public const int ShopTradeEvent = 0x20;
    public const int BulletSpentEvent = 0x11;
    public const int ThrowItemSpentEvent = 0x12;
    public const int ThrowCardSpentEvent = 0x13;
    public const int BulletSubTypeFirst = 56;
    public const int BulletSubTypeLast = 59;
    public const int ThrowItemSubType = 39;
    public const int ThrowCardSubType = 60;
    public const int HpPotionEvent = 0x14;
    public const int MpPotionEvent = 0x15;
    public const int CardIdentifyDealEvent = 44;
    public const int SecretCardEventBase = 51;
    public const int AngelReturnEvent = 42;
    public const int SecretCardFirstItem = 26500;
    public const int SecretCardLastItem = 26515;
    public const int SecretCardKinds = 4;
    public const byte RemoveAfterUse = 0;
    public const byte DecrementAfterUse = 2;

    public static IEnumerable<int> StatPointEvents(IReadOnlyList<short> amounts)
    {
        ArgumentNullException.ThrowIfNull(amounts);
        for (var group = 0; group < StatPointEventGroups; group++)
        {
            var points = 0;
            for (var slot = 0; slot < StatPointEventGroupSize; slot++) points += amounts[group * StatPointEventGroupSize + slot];
            for (var point = 0; point < points; point++) yield return StatPointEventBase + group;
        }
    }

    public static int? DrillFinishEvent(byte result) => result switch
    {
        1 => DrillEndedEvent,
        2 or 9 => DrillFailedEvent,
        3 or 8 => DrillFoundEvent,
        _ => null,
    };

    public static IReadOnlyList<int> RefineEvents(int newLevel) => Enumerable.Repeat(RefineEvent, Math.Max(newLevel, 0)).ToArray();

    public static IReadOnlyList<int> ItemSpentEvents(int subType, int spent, int left)
    {
        var spentEvent = subType switch
        {
            ThrowCardSubType => ThrowCardSpentEvent,
            ThrowItemSubType => ThrowItemSpentEvent,
            >= BulletSubTypeFirst and <= BulletSubTypeLast when left > 0 => BulletSpentEvent,
            _ => 0,
        };
        if (spentEvent == 0 || spent <= 0) return [];
        return left > 0 ? Enumerable.Repeat(spentEvent, spent).ToArray() : [spentEvent];
    }

    public static int? CriticalLandedByPlayer(int attackerLevel, int targetLevel, uint kind) =>
        attackerLevel - targetLevel < HitLevelGap && (kind & KindMask) is CriticalKind or StrongCriticalKind ? CriticalLandedEvent : null;

    public static int? HitAvoidedByPlayer(int victimLevel, int attackerLevel, uint kind) =>
        victimLevel - attackerLevel >= HitLevelGap ? null
        : kind == MissKind ? HitMissedEvent
        : kind == BlockKind ? HitBlockedEvent
        : null;

    private static readonly Dictionary<byte, int> QuestEvents = new()
    {
        [Quests.QuestTypes.Normal] = 6,
        [Quests.QuestTypes.Scenario] = 7,
        [Quests.QuestTypes.Monster] = 8,
        [Quests.QuestTypes.Party] = 9,
    };

    private static readonly Dictionary<int, int> CardIdentifyComboEvents = new()
    {
        [3] = 45,
        [5] = 46,
        [7] = 47,
        [9] = 48,
        [10] = 49,
        [11] = 50,
    };

    public static int? CardIdentifyComboEvent(int combo) => CardIdentifyComboEvents.TryGetValue(combo, out var eventType) ? eventType : null;

    public static int? SecretCardEvent(int itemId) =>
        itemId is >= SecretCardFirstItem and <= SecretCardLastItem ? SecretCardEventBase + (itemId - SecretCardFirstItem) % SecretCardKinds : null;

    public static int? QuestEvent(byte questType) => QuestEvents.TryGetValue(questType, out var eventType) ? eventType : null;

    public static bool CountsKill(int killerLevel, int monsterLevel) => killerLevel - monsterLevel < MonsterKillLevelGap;

    public static byte UseFlag(int stack) => stack < 2 ? RemoveAfterUse : DecrementAfterUse;

    public static int GuardianKind(IReadOnlyList<int> sums, Func<int, int> random)
    {
        ArgumentNullException.ThrowIfNull(sums);
        ArgumentNullException.ThrowIfNull(random);
        if (sums.All(sum => sum == sums[0])) return random(NormalTypes);
        var kind = 0;
        for (var k = 1; k < NormalTypes; k++)
        {
            if (sums[k] > sums[kind]) kind = k;
        }
        return kind;
    }

    public static byte RareType(IReadOnlyList<int> combinedLevels, int baseLevelSum, int specialLevelSum)
    {
        ArgumentNullException.ThrowIfNull(combinedLevels);
        byte rare = 0;
        if (combinedLevels.All(level => level == combinedLevels[0]))
        {
            if (baseLevelSum is >= 8 and <= 11) rare = 4;
            else if (baseLevelSum > 11) rare = 3;
        }
        if (baseLevelSum > 12) rare = 2;
        if (baseLevelSum > 12 && specialLevelSum > 3) rare = 1;
        return rare;
    }
}

public sealed class SoulSeedGrowth
{
    private readonly short[] _saved = new short[SoulSeedRules.TypeCount];
    private readonly short[] _session = new short[SoulSeedRules.TypeCount];
    private readonly short[] _counters = new short[SoulSeedRules.EventTypeCount];
    private readonly int[] _buffValues = new int[SoulSeedRules.BuffValueSlots];
    private readonly object _gate = new();

    public bool Planted { get; private set; }
    public bool Complete { get; private set; }
    public int SeedItemId { get; private set; }
    public DateTimeOffset StartAt { get; private set; }
    public DateTimeOffset LastUpdateAt { get; private set; }
    public ulong EventMask { get; private set; }
    public int BuffGroup { get; private set; } = SoulSeedRules.NoBuffGroup;
    public int BuffMillisecondsLeft { get; private set; }
    public int BuffStacks { get; private set; }

    public IReadOnlyList<short> SessionPoints
    {
        get { lock (_gate) return _session.ToArray(); }
    }

    public IReadOnlyList<short> Counters
    {
        get { lock (_gate) return _counters.ToArray(); }
    }

    public SoulSeedRecord Record()
    {
        lock (_gate) return new SoulSeedRecord(Planted, SeedItemId, StartAt, LastUpdateAt, _saved.ToArray());
    }

    public SoulSeedBuff Buff()
    {
        lock (_gate) return new SoulSeedBuff(BuffGroup, BuffMillisecondsLeft, BuffStacks, _buffValues.ToArray());
    }

    public int BuffValueOf(int stat)
    {
        lock (_gate) return stat >= 0 && stat < _buffValues.Length ? _buffValues[stat] : 0;
    }

    public bool TickBuff(int elapsedMilliseconds)
    {
        lock (_gate)
        {
            if (BuffMillisecondsLeft <= 0) return false;
            BuffMillisecondsLeft -= elapsedMilliseconds;
            if (BuffMillisecondsLeft >= 1) return false;
            BuffGroup = SoulSeedRules.NoBuffGroup;
            BuffMillisecondsLeft = 0;
            BuffStacks = 0;
            Array.Clear(_buffValues);
            return true;
        }
    }

    public int Total(int type)
    {
        lock (_gate) return TotalOf(type);
    }

    public int Level(int type)
    {
        lock (_gate) return TotalOf(type) / SoulSeedRules.PointsPerLevel;
    }

    public void Plant(int seedItemId, DateTimeOffset now)
    {
        lock (_gate)
        {
            Array.Clear(_saved);
            Array.Clear(_session);
            Array.Clear(_counters);
            Planted = true;
            Complete = false;
            SeedItemId = seedItemId;
            StartAt = now;
            LastUpdateAt = now;
        }
    }

    public void Restore(SoulSeedRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        lock (_gate)
        {
            Array.Clear(_session);
            Array.Clear(_counters);
            for (var i = 0; i < SoulSeedRules.TypeCount; i++) _saved[i] = i < record.SavedPoints.Count ? record.SavedPoints[i] : (short)0;
            Planted = record.Planted;
            SeedItemId = record.SeedItemId;
            StartAt = record.StartAt;
            LastUpdateAt = record.LastUpdateAt;
            Complete = CompleteNow();
        }
    }

    public bool RestoreUnlessLive(SoulSeedRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        lock (_gate)
        {
            if (Planted) return false;
        }
        Restore(record);
        return true;
    }

    public SoulSeedRecord? Flush()
    {
        lock (_gate) return Planted ? Backup() : null;
    }

    public void Clear()
    {
        lock (_gate)
        {
            Array.Clear(_saved);
            Array.Clear(_session);
            Array.Clear(_counters);
            Planted = false;
            Complete = false;
            SeedItemId = 0;
        }
    }

    public SoulSeedEventOutcome OnEvent(int eventType, int count, SoulSeedEventEntry? row, IReadOnlyList<int>? baseStats = null)
    {
        lock (_gate)
        {
            if (eventType is >= 0 and < SoulSeedRules.EventMaskBits) EventMask |= 1UL << eventType;
            if (!Planted || row is null || row.Rate <= 0 || eventType < 0 || eventType >= SoulSeedRules.EventTypeCount) return SoulSeedEventOutcome.Skipped;
            var counter = _counters[eventType] + count;
            if (counter / row.Rate < 1)
            {
                _counters[eventType] = (short)Math.Clamp(counter, short.MinValue, short.MaxValue);
                return new SoulSeedEventOutcome(true, false, counter, false, false, false, Array.Empty<SoulSeedRecord>());
            }
            _counters[eventType] = (short)Math.Clamp(counter - row.Rate, short.MinValue, short.MaxValue);
            var first = row.Special ? SoulSeedRules.NormalTypes : 0;
            var buffGroup = SoulSeedRules.NoBuffGroup;
            var changed = false;
            var complete = false;
            var backups = new List<SoulSeedRecord>();
            for (var t = 0; t < SoulSeedRules.NormalTypes && t < row.Points.Count; t++)
            {
                var type = first + t;
                if (row.Points[t] < 1 || !CanAdd(type, row.Special, row.MaximumLevel)) continue;
                for (var point = 0; point < row.Points[t]; point++)
                {
                    _session[type]++;
                    var total = TotalOf(type);
                    if (total % SoulSeedRules.BuffPointStep == 0) buffGroup = type % SoulSeedRules.NormalTypes;
                    if (total % SoulSeedRules.PointsPerLevel == 0) backups.Add(Backup());
                }
                changed = true;
                if (CompleteNow())
                {
                    Complete = true;
                    complete = true;
                }
            }
            var buffChanged = changed && buffGroup != SoulSeedRules.NoBuffGroup && ApplyBuff(buffGroup, baseStats);
            return new SoulSeedEventOutcome(true, true, counter, changed, buffChanged, complete, backups);
        }
    }

    private bool CanAdd(int type, bool special, int maximumLevel)
    {
        if (Complete) return false;
        var level = TotalOf(type) / SoulSeedRules.PointsPerLevel;
        if (!special)
        {
            if (maximumLevel != 0 && level >= maximumLevel) return false;
            return level <= SoulSeedRules.HighestNormalLevel;
        }
        var specialSum = 0;
        for (var i = SoulSeedRules.NormalTypes; i < SoulSeedRules.TypeCount; i++) specialSum += TotalOf(i) / SoulSeedRules.PointsPerLevel;
        return level <= SoulSeedRules.HighestSpecialLevel && specialSum <= SoulSeedRules.HighestSpecialLevelSum;
    }

    private bool CompleteNow()
    {
        var sum = 0;
        for (var i = 0; i < SoulSeedRules.NormalTypes; i++)
        {
            var level = TotalOf(i) / SoulSeedRules.PointsPerLevel;
            if (level >= SoulSeedRules.CompleteLevel) return true;
            sum += level;
        }
        return sum >= SoulSeedRules.CompleteLevelSum;
    }

    private SoulSeedRecord Backup()
    {
        for (var i = 0; i < SoulSeedRules.TypeCount; i++)
        {
            _saved[i] = (short)Math.Clamp(_saved[i] + _session[i], short.MinValue, short.MaxValue);
            _session[i] = 0;
        }
        return new SoulSeedRecord(Planted, SeedItemId, StartAt, LastUpdateAt, _saved.ToArray());
    }

    private bool ApplyBuff(int group, IReadOnlyList<int>? baseStats)
    {
        if (group != BuffGroup)
        {
            BuffGroup = SoulSeedRules.NoBuffGroup;
            BuffMillisecondsLeft = 0;
            BuffStacks = 0;
            Array.Clear(_buffValues);
        }
        if (BuffStacks < SoulSeedRules.MaxBuffStacks)
        {
            BuffGroup = group;
            BuffStacks++;
            foreach (var stat in SoulSeedRules.BuffStats[group])
            {
                _buffValues[stat] = baseStats is not null && stat < baseStats.Count ? SoulSeedRules.BuffValue(baseStats[stat], BuffStacks) : 0;
            }
        }
        BuffMillisecondsLeft = SoulSeedRules.BuffMilliseconds;
        return true;
    }

    private int TotalOf(int type) => _saved[type] + _session[type];
}
