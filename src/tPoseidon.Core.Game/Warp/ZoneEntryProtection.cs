namespace tPoseidon.Core.Game.Warp;

public static class ZoneEntryProtection
{
    public static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(3000);

    public static DateTimeOffset Until(DateTimeOffset current, DateTimeOffset now)
    {
        var entry = now + Duration;
        return current > entry ? current : entry;
    }
}
