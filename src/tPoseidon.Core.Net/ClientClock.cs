namespace tPoseidon.Core.Net;

public static class ClientClock
{
    public static ulong FileTime(DateTime time)
    {
        var local = time.Kind == DateTimeKind.Utc ? time.ToLocalTime() : time;
        return (ulong)DateTime.SpecifyKind(local, DateTimeKind.Utc).ToFileTimeUtc();
    }

    public static ulong FileTime(DateTimeOffset time) => FileTime(time.UtcDateTime);
}
