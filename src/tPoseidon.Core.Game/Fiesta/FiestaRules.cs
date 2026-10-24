using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Fiesta;

public readonly record struct FiestaEntryRecord(byte Count, DateTime? Slot);

public readonly record struct FiestaWindow(FiestaSlotEntry Row, DateTime SlotStart, TimeSpan Duration, TimeSpan Elapsed);

public enum FiestaTicketRefusal
{
    None,
    MapRefuses,
    NoLevelBand,
    Closed,
    DailyLimit,
}

public static class FiestaRules
{
    public const int TicketEffect = 46;
    public const int FiestaPortType = 1;
    public const int DailyEntries = 2;
    public const int NoPortal = -1;
    public const byte PortalOpenedState = 2;
    public const uint MapRefusedCode = 4;
    public const uint ClosedCode = 7;
    public const uint DailyLimitCode = 9;
    public const int WarpRefusedMapOption = 0x80;
    public const int LowestReturnZone = 1;
    public const int HighestReturnZone = 999;

    public static FiestaWindow? Open(FiestaSlotEntry row, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.InYear != 0 && row.InYear != now.Year) return null;
        if (row.InMonth != 0 && row.InMonth != now.Month) return null;
        if (row.InDay != 0 && row.InDay != now.Day) return null;
        var minute = now.Hour * 60 + now.Minute;
        if (minute < row.InMinuteOfDay || minute >= row.OutMinuteOfDay) return null;
        var start = now.Date.AddMinutes(row.InMinuteOfDay);
        return new FiestaWindow(row, start, TimeSpan.FromMinutes(row.OutMinuteOfDay - row.InMinuteOfDay), now - start);
    }

    public static FiestaWindow? OpenInZone(IEnumerable<FiestaSlotEntry> rows, int zoneId, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(rows);
        foreach (var row in rows.Where(row => row.ZoneId == zoneId))
        {
            if (Open(row, now) is { } window) return window;
        }
        return null;
    }

    public static FiestaWindow? AnyOpen(IEnumerable<FiestaSlotEntry> rows, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(rows);
        foreach (var row in rows)
        {
            if (Open(row, now) is { } window) return window;
        }
        return null;
    }

    public static bool PortalOpens(FiestaWindow window) =>
        window.Row.PortalIndex != NoPortal && window.Elapsed >= TimeSpan.FromMinutes(window.Row.PortalOpenMinute);

    public static bool IsFiestaZone(IEnumerable<FiestaSlotEntry> rows, int zoneId)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return rows.Any(row => row.ZoneId == zoneId);
    }

    public static FiestaEntryRecord Today(FiestaEntryRecord record, DateTime now) =>
        record.Slot is { } slot && slot.Date == now.Date ? record : new FiestaEntryRecord(0, record.Slot);

    public static bool TicketAllowed(FiestaEntryRecord record, DateTime slotStart, DateTime now)
    {
        var today = Today(record, now);
        if (today.Slot == slotStart) return true;
        return today.Count < DailyEntries;
    }

    public static FiestaEntryRecord Entered(FiestaEntryRecord record, DateTime slotStart, DateTime now)
    {
        var today = Today(record, now);
        return today.Slot == slotStart ? today : new FiestaEntryRecord((byte)Math.Min(byte.MaxValue, today.Count + 1), slotStart);
    }

    public static LevelPortBand? BandFor(LevelPortEntry port, int level)
    {
        ArgumentNullException.ThrowIfNull(port);
        return port.Bands.FirstOrDefault(band => band.MinLevel <= level && level <= band.MaxLevel);
    }

    public static (FiestaTicketRefusal Refusal, LevelPortBand? Band) Ticket(
        LevelPortEntry port, int level, int mapOption, IReadOnlyList<FiestaSlotEntry> slots, FiestaEntryRecord record, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(port);
        ArgumentNullException.ThrowIfNull(slots);
        if ((mapOption & WarpRefusedMapOption) != 0) return (FiestaTicketRefusal.MapRefuses, null);
        if (BandFor(port, level) is not { } band) return (FiestaTicketRefusal.NoLevelBand, null);
        if (port.PortType == FiestaPortType)
        {
            if (AnyOpen(slots, now) is not { } window) return (FiestaTicketRefusal.Closed, null);
            if (!TicketAllowed(record, window.SlotStart, now)) return (FiestaTicketRefusal.DailyLimit, null);
        }
        return (FiestaTicketRefusal.None, band);
    }

    public static int ReturnZone(int fiestaReturnZone, int saveMap) =>
        fiestaReturnZone is >= LowestReturnZone and <= HighestReturnZone ? fiestaReturnZone : saveMap;
}
