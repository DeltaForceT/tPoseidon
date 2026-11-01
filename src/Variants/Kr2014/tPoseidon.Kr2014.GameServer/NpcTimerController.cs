using tPoseidon.Core.LibConfig;

internal sealed class NpcTimerController
{
    private readonly Func<IReadOnlyList<NpcTimerEntry>> _timers;
    private readonly Func<DateTimeOffset, int?> _phaseAt;
    private readonly Func<int> _phases;
    private readonly Func<int, bool, Task> _setTimerNpc;
    private readonly Dictionary<int, bool> _open = new();

    public NpcTimerController(
        Func<IReadOnlyList<NpcTimerEntry>> timers,
        Func<DateTimeOffset, int?> phaseAt,
        Func<int> phases,
        Func<int, bool, Task> setTimerNpc)
    {
        _timers = timers;
        _phaseAt = phaseAt;
        _phases = phases;
        _setTimerNpc = setTimerNpc;
    }

    public async Task TickAsync(DateTimeOffset now)
    {
        var phase = _phaseAt(now);
        var phases = _phases();
        foreach (var timer in _timers())
        {
            var open = timer.IsOpen(now.LocalDateTime, phase, phases);
            if (_open.TryGetValue(timer.TimerId, out var was) && was == open) continue;
            _open[timer.TimerId] = open;
            await _setTimerNpc(timer.TimerId, open).ConfigureAwait(false);
        }
    }
}
