namespace tPoseidon.Core.LibConfig;

public sealed record NpcTimerEntry(
    int TimerId, string Comment, int StartMinute, int EndMinute, int StartPhase, int EndPhase, int WeekdayMask, int SubType)
{
    public const string TableName = "Npc_TimeTable";
    public const int NoPhase = -1;
    public const int MinutesPerDay = 1440;
    private const int MinutesPerHour = 60;

    public bool UsesDayPhase => StartPhase != NoPhase && EndPhase != NoPhase;

    public bool IsOpen(DateTime local, int? phase, int phases)
    {
        if (WeekdayMask != 0 && (WeekdayMask & (1 << (int)local.DayOfWeek)) == 0) return false;
        if (UsesDayPhase) return phase is { } current && phases > 0 && InPhaseRange(current, phases);
        var minute = local.Hour * MinutesPerHour + local.Minute;
        if (StartMinute == EndMinute) return true;
        return StartMinute < EndMinute ? minute >= StartMinute && minute < EndMinute : minute >= StartMinute || minute < EndMinute;
    }

    private bool InPhaseRange(int phase, int phases)
    {
        var length = ((EndPhase - StartPhase) % phases + phases) % phases;
        var offset = ((phase - StartPhase) % phases + phases) % phases;
        return offset < (length == 0 ? phases : length);
    }

    public static int ParseWeekdays(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var mask = 0;
        foreach (var part in text.Split([',', '/', ' '], StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.Length == 2 && (part[0] == 'w' || part[0] == 'W') && part[1] is >= '1' and <= '7') mask |= 1 << (part[1] - '1');
        }
        return mask;
    }
}
