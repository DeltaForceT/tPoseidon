namespace tPoseidon.Core.LibConfig;

public sealed record ShadowTimerEntry(int TimerId, string Comment, int StartHour, int StartMinute, int EndHour, int EndMinute, int WorldMapRegion = 0)
{
    public const string TableName = "Npc_TimeTable";
    public const int ShadowSubType = 3;
    private const int MinutesPerHour = 60;

    public int StartOfDay => StartHour * MinutesPerHour + StartMinute;

    public int EndOfDay => EndHour * MinutesPerHour + EndMinute;

    public bool IsOpen(DateTime local)
    {
        var minute = local.Hour * MinutesPerHour + local.Minute;
        var start = StartOfDay;
        var end = EndOfDay;
        return start <= end ? minute >= start && minute < end : minute >= start || minute < end;
    }
}
