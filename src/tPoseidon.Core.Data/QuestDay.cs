namespace tPoseidon.Core.Data;

public static class QuestDay
{
    public static (DateTime StartUtc, DateTime EndUtc) Bounds(DateTime referenceUtc, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        var utc = DateTime.SpecifyKind(referenceUtc, DateTimeKind.Utc);
        var localDay = TimeZoneInfo.ConvertTimeFromUtc(utc, zone).Date;
        var nextDay = localDay.AddDays(1);
        return (ToUtc(localDay, zone), ToUtc(nextDay, zone));
    }

    public static bool Contains(DateTimeOffset completedAt, DateTime referenceUtc, TimeZoneInfo zone)
    {
        var (start, end) = Bounds(referenceUtc, zone);
        var at = completedAt.UtcDateTime;
        return at >= start && at < end;
    }

    private static DateTime ToUtc(DateTime local, TimeZoneInfo zone)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(unspecified)) unspecified = unspecified.AddHours(1);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, zone);
    }
}
