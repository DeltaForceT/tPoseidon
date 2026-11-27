using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Fiesta;

public sealed class FiestaAnnouncer
{
    public const byte StartedState = 1;
    public const byte EndedState = 3;
    public const byte FiveMinuteWarningState = 4;
    public const byte TenMinuteWarningState = 5;
    public const int TenMinutes = 10;
    public const int FiveMinutes = 5;
    public const int MinutesPerDay = 1440;

    private sealed class RowState
    {
        public bool Active;
        public bool FiveMinuteSent;
        public bool Warned;
    }

    private readonly Dictionary<int, RowState> _rows = new();

    public byte? Step(IReadOnlyList<FiestaSlotEntry> slots, DateTime now, Func<int, bool> zoneHosted)
    {
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(zoneHosted);
        var minute = now.Hour * 60 + now.Minute;
        byte state = 0;
        foreach (var row in slots)
        {
            if (!_rows.TryGetValue(row.Id, out var flags)) _rows[row.Id] = flags = new RowState();
            if (!flags.Active) StepInactive(row, flags, now, minute, zoneHosted, ref state);
            else StepActive(row, flags, now, minute, zoneHosted, ref state);
        }
        return state == 0 ? null : state;
    }

    private static void StepInactive(FiestaSlotEntry row, RowState flags, DateTime now, int minute, Func<int, bool> zoneHosted, ref byte state)
    {
        if (!DateMatches(row.InYear, row.InMonth, row.InDay, now)) return;
        var start = row.InMinuteOfDay;
        if (!flags.Warned && (start == minute + TenMinutes || (start < TenMinutes && start + MinutesPerDay - TenMinutes == minute)))
        {
            flags.Warned = true;
            state = TenMinuteWarningState;
            return;
        }
        if (!flags.FiveMinuteSent && (start == minute + FiveMinutes || (start < FiveMinutes && start + MinutesPerDay - FiveMinutes == minute)))
        {
            flags.Warned = true;
            flags.FiveMinuteSent = true;
            state = FiveMinuteWarningState;
            return;
        }
        if (!InWindow(start, row.OutMinuteOfDay, minute)) return;
        flags.Active = true;
        flags.FiveMinuteSent = false;
        flags.Warned = true;
        if (zoneHosted(row.ZoneId) && state == 0) state = StartedState;
    }

    private static void StepActive(FiestaSlotEntry row, RowState flags, DateTime now, int minute, Func<int, bool> zoneHosted, ref byte state)
    {
        if (!DateMatches(row.OutYear, row.OutMonth, row.OutDay, now)) return;
        var start = row.InMinuteOfDay;
        var end = row.OutMinuteOfDay;
        if (!((end <= start && minute < start) || end <= minute)) return;
        flags.Active = false;
        flags.Warned = false;
        if (zoneHosted(row.ZoneId) && state == 0) state = EndedState;
    }

    private static bool InWindow(int start, int end, int minute) =>
        start < end ? start <= minute && minute < end : minute >= start || minute < end;

    private static bool DateMatches(int year, int month, int day, DateTime now) =>
        (year == 0 || year == now.Year) && (month == 0 || month == now.Month) && (day == 0 || day == now.Day);
}
