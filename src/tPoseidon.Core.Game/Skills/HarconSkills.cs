using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Skills;

public sealed record HarconSlot(byte Slot, int X, int Y, int WaitMs, int HitMs)
{
    public int FireMs => WaitMs + HitMs;
}

public enum HarconStep
{
    Add,
    Fire,
}

public static class HarconSkills
{
    public const int Hurricane = 1510;
    public const int HurricaneStorm = 1511;
    public const int Pressure = 1513;
    public const int GiantPressure = 1514;
    public const int TimeBreak = 1516;
    public const int TimeBomb = 1517;
    public const int BrillantLight = 1519;
    public const int CronosImpact = 1521;
    public const int SlotCount = 8;
    public const int StepMs = 100;
    public const string PowerColumn = "AP";
    public const string RatioColumn = "Ratio";
    public const string HitRangeColumn = "HitRange";
    public const string HitCountColumn = "HitCnt";
    private const string WaitColumn = "WaitTime";
    private const string HitColumn = "HitTime";
    private const string XColumn = "XPos";
    private const string YColumn = "YPos";

    public static bool IsHarcon(int actionId) =>
        actionId is Hurricane or HurricaneStorm or Pressure or GiantPressure or TimeBreak or TimeBomb or BrillantLight or CronosImpact;

    public static IReadOnlyList<HarconSlot> Slots(SkillTableRow row, (int X, int Y) centre)
    {
        ArgumentNullException.ThrowIfNull(row);
        var slots = new List<HarconSlot>(SlotCount);
        for (var index = 0; index < SlotCount; index++)
        {
            var number = index + 1;
            var x = row.Amount(XColumn + number) ?? 0;
            var y = row.Amount(YColumn + number) ?? 0;
            var hit = row.Amount(HitColumn + number) ?? 0;
            if (x == 0 && y == 0 && hit == 0) continue;
            slots.Add(new HarconSlot((byte)index, centre.X + x, centre.Y + y, Math.Max(0, row.Amount(WaitColumn + number) ?? 0), Math.Max(0, hit)));
        }
        return slots;
    }

    public static IReadOnlyList<HarconSlot> Pick(IReadOnlyList<HarconSlot> slots, int hitCount, Func<int, int> randomBelow)
    {
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(randomBelow);
        var pool = slots.ToList();
        var count = Math.Clamp(hitCount, 0, pool.Count);
        for (var index = 0; index < count; index++)
        {
            var swap = index + randomBelow(pool.Count - index);
            (pool[index], pool[swap]) = (pool[swap], pool[index]);
        }
        return pool.Take(count).OrderBy(slot => slot.Slot).ToList();
    }
}

public sealed class HarconPlan
{
    private readonly HashSet<byte> _added = new();
    private readonly HashSet<byte> _fired = new();

    public HarconPlan(IReadOnlyList<HarconSlot> slots)
    {
        Slots = slots ?? throw new ArgumentNullException(nameof(slots));
        foreach (var slot in slots.Where(slot => slot.WaitMs == 0)) _added.Add(slot.Slot);
    }

    public IReadOnlyList<HarconSlot> Slots { get; }

    public IReadOnlyList<HarconSlot> Initial => Slots.Where(slot => slot.WaitMs == 0).ToList();

    public int LastMs => Slots.Count == 0 ? 0 : Slots.Max(slot => slot.FireMs);

    public bool Done => _fired.Count == Slots.Count;

    public IReadOnlyList<(HarconSlot Slot, HarconStep Step)> TakeDue(int elapsedMs)
    {
        var due = new List<(HarconSlot Slot, HarconStep Step)>();
        foreach (var slot in Slots)
        {
            if (!_added.Contains(slot.Slot) && elapsedMs >= slot.WaitMs)
            {
                _added.Add(slot.Slot);
                due.Add((slot, HarconStep.Add));
            }
            if (_added.Contains(slot.Slot) && !_fired.Contains(slot.Slot) && elapsedMs >= slot.FireMs)
            {
                _fired.Add(slot.Slot);
                due.Add((slot, HarconStep.Fire));
            }
        }
        return due;
    }
}
