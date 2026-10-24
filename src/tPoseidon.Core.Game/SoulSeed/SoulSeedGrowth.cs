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
    public const int SeedSubType = 72;
    public const int SummonGuardianSkill = 8003;
    public const int GuardianSlotsFullCode = 10;
    public const int MonsterKillEvent = 10;
    public const int MonsterKillLevelGap = 20;
    public const int LevelUpEvent = 0;
    public const int TmLevelUpEvent = 1;
    public const int HpPotionEvent = 0x14;
    public const int MpPotionEvent = 0x15;
    public const byte RemoveAfterUse = 0;
    public const byte DecrementAfterUse = 2;

    private static readonly Dictionary<byte, int> QuestEvents = new()
    {
        [Quests.QuestTypes.Normal] = 6,
        [Quests.QuestTypes.Scenario] = 7,
        [Quests.QuestTypes.Monster] = 8,
        [Quests.QuestTypes.Party] = 9,
    };

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
        var allEqual = combinedLevels.All(level => level == combinedLevels[0]);
        if (allEqual && baseLevelSum >= 8 && baseLevelSum <= 11) return 4;
        if (allEqual && baseLevelSum > 11) return 3;
        if (baseLevelSum > 12) return specialLevelSum > 3 ? (byte)1 : (byte)2;
        return 0;
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

    public SoulSeedEventOutcome OnEvent(int eventType, int count, SoulSeedEventEntry? row)
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
            var backups = new List<SoulSeedRecord>();
            for (var t = 0; t < SoulSeedRules.NormalTypes && t < row.Points.Count; t++)
            {
                var type = first + t;
                for (var point = 0; point < row.Points[t]; point++)
                {
                    if (!CanAdd(type, row.Special, row.MaximumLevel)) break;
                    _session[type]++;
                    changed = true;
                    var total = TotalOf(type);
                    if (total % SoulSeedRules.BuffPointStep == 0) buffGroup = type % SoulSeedRules.NormalTypes;
                    if (total % SoulSeedRules.PointsPerLevel == 0) backups.Add(Backup());
                }
            }
            var complete = CompleteNow();
            if (complete) Complete = true;
            var buffChanged = changed && buffGroup != SoulSeedRules.NoBuffGroup && ApplyBuff(buffGroup);
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

    private bool ApplyBuff(int group)
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
        }
        BuffMillisecondsLeft = SoulSeedRules.BuffMilliseconds;
        return true;
    }

    private int TotalOf(int type) => _saved[type] + _session[type];
}
