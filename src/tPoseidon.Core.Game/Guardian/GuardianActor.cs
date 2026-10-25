namespace tPoseidon.Core.Game.Guardian;

public sealed class GuardianActor
{
    public required ushort EntityId { get; init; }
    public required ushort MapId { get; init; }
    public required ulong GuardianUid { get; init; }
    public GuardianTargetMarks Marks { get; } = new();
    public GuardianCastClock Clock { get; } = new();
    public GuardianOrderCycle OrderCycle { get; } = new();
    public Skills.SkillCooldowns Cooldowns { get; } = new();
    public DateTimeOffset TickedAt { get; set; }
    public DateTimeOffset BusyUntil { get; set; }

    public int FromX { get; private set; }
    public int FromY { get; private set; }
    public int ToX { get; private set; }
    public int ToY { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset ArrivesAt { get; private set; }
    public DateTimeOffset NextFollowAt { get; set; }
    public byte SpeedQuarter { get; set; }

    public void PlaceAt(int x, int y, DateTimeOffset now)
    {
        FromX = ToX = x;
        FromY = ToY = y;
        StartedAt = ArrivesAt = now;
    }

    public void MoveTo(int fromX, int fromY, int toX, int toY, DateTimeOffset now, TimeSpan travel)
    {
        FromX = fromX;
        FromY = fromY;
        ToX = toX;
        ToY = toY;
        StartedAt = now;
        ArrivesAt = now + travel;
    }

    public (int X, int Y) PositionAt(DateTimeOffset now)
    {
        if (now >= ArrivesAt || ArrivesAt <= StartedAt) return (ToX, ToY);
        var progress = (now - StartedAt).TotalMilliseconds / (ArrivesAt - StartedAt).TotalMilliseconds;
        return ((int)Math.Round(FromX + (ToX - FromX) * progress), (int)Math.Round(FromY + (ToY - FromY) * progress));
    }
}
