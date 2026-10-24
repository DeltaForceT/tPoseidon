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
