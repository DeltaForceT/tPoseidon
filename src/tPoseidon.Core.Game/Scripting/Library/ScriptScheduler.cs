using tPoseidon.Core.Game.Scripting.Lang;

namespace tPoseidon.Core.Game.Scripting.Library;

public sealed class ScriptScheduler
{
    private readonly IReadOnlyList<ScriptTimerEntry> _timers;
    private readonly DateTimeOffset[] _nextInterval;
    private DateTimeOffset _lastMinute = DateTimeOffset.MinValue;

    public ScriptScheduler(IReadOnlyList<ScriptTimerEntry> timers, DateTimeOffset start)
    {
        _timers = timers ?? throw new ArgumentNullException(nameof(timers));
        _nextInterval = new DateTimeOffset[timers.Count];
        for (var i = 0; i < timers.Count; i++)
        {
            if (timers[i].Timer.Declaration.Kind == TimerKind.Interval) _nextInterval[i] = start + timers[i].Timer.Declaration.Interval;
        }
        _lastMinute = Truncate(start);
    }

    public IReadOnlyList<ScriptTimerEntry> Collect(DateTimeOffset now)
    {
        var due = new List<ScriptTimerEntry>();
        var minute = Truncate(now);
        var minuteAdvanced = minute > _lastMinute;
        for (var i = 0; i < _timers.Count; i++)
        {
            var entry = _timers[i];
            switch (entry.Timer.Declaration.Kind)
            {
                case TimerKind.Interval:
                    if (now >= _nextInterval[i])
                    {
                        due.Add(entry);
                        _nextInterval[i] = now + entry.Timer.Declaration.Interval;
                    }
                    break;
                case TimerKind.DailyAt:
                    if (minuteAdvanced && minute.TimeOfDay == entry.TimeOfDay) due.Add(entry);
                    break;
                default:
                    if (minuteAdvanced && entry.Cron!.Matches(minute)) due.Add(entry);
                    break;
            }
        }
        if (minuteAdvanced) _lastMinute = minute;
        return due;
    }

    private static DateTimeOffset Truncate(DateTimeOffset time) =>
        new(time.Year, time.Month, time.Day, time.Hour, time.Minute, 0, time.Offset);
}
