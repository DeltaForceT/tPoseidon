using System.Collections.Concurrent;
using tPoseidon.Core.Data;

namespace tPoseidon.Core.Net.World;

public sealed class TimerNpcManager
{
    private readonly ConcurrentDictionary<int, TimerNpcEntry> _byTimerId = new();
    private readonly Action<string>? _log;

    public TimerNpcManager(Action<string>? log = null)
    {
        _log = log;
    }

    public int Count => _byTimerId.Count;

    public IReadOnlyCollection<TimerNpcEntry> All => _byTimerId.Values.ToArray();

    public void Register(int timerId, NpcEntity npc, int startHour, int endHour)
    {
        if (timerId <= 0) return;
        _byTimerId[timerId] = new TimerNpcEntry
        {
            TimerId = timerId,
            Npc = npc,
            StartHour = startHour,
            EndHour = endHour,
            IsActive = false,
        };
    }

    public TimerNpcEntry? Get(int timerId) =>
        _byTimerId.TryGetValue(timerId, out var v) ? v : null;

    public IReadOnlyList<TimerNpcEntry> EvaluateNow(DateTime now)
    {
        var hour = now.Hour;
        var changed = new List<TimerNpcEntry>();
        foreach (var entry in _byTimerId.Values)
        {
            var shouldBeActive = IsActiveAtHour(entry.StartHour, entry.EndHour, hour);
            if (shouldBeActive != entry.IsActive)
            {
                entry.IsActive = shouldBeActive;
                changed.Add(entry);
                _log?.Invoke($"TimerNpc: {entry.Npc.ChrType}@map{entry.Npc.MapId} TimerId={entry.TimerId} -> {(shouldBeActive ? "Active" : "UnActive")}");
            }
        }
        return changed;
    }

    private static bool IsActiveAtHour(int start, int end, int hour)
    {
        if (start == end) return false;
        if (start < end) return hour >= start && hour < end;
        return hour >= start || hour < end;
    }
}

public sealed class TimerNpcEntry
{
    public required int TimerId { get; init; }
    public required NpcEntity Npc { get; init; }
    public required int StartHour { get; init; }
    public required int EndHour { get; init; }
    public bool IsActive { get; set; }
}
