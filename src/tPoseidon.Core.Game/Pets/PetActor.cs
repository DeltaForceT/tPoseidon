namespace tPoseidon.Core.Game.Pets;

public sealed class PetActor
{
    public required ushort EntityId { get; init; }
    public required ulong ItemUid { get; init; }
    public required int CharacterType { get; init; }
    public required string Name { get; init; }
    public required ushort MapId { get; init; }
    public PetRecovery Recovery { get; init; }
    public PetSpeechState Speech { get; } = new();

    public int FromX { get; private set; }
    public int FromY { get; private set; }
    public int ToX { get; private set; }
    public int ToY { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset ArrivesAt { get; private set; }
    public DateTimeOffset NextFollowAt { get; set; }
    public DateTimeOffset NextEmoteAt { get; set; }
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

    public bool IsMoving(DateTimeOffset now) => now < ArrivesAt;

    public (int X, int Y) PositionAt(DateTimeOffset now)
    {
        if (now >= ArrivesAt || ArrivesAt <= StartedAt) return (ToX, ToY);
        var progress = (now - StartedAt).TotalMilliseconds / (ArrivesAt - StartedAt).TotalMilliseconds;
        return ((int)Math.Round(FromX + (ToX - FromX) * progress), (int)Math.Round(FromY + (ToY - FromY) * progress));
    }
}
