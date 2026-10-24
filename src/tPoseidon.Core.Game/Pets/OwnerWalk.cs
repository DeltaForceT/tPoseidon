namespace tPoseidon.Core.Game.Pets;

public readonly record struct OwnerWalk(int FromX, int FromY, IReadOnlyList<(int X, int Y)> Points, float Speed, DateTimeOffset StartedAt, DateTimeOffset ArrivesAt)
{
    public int ToX => Points[^1].X;

    public int ToY => Points[^1].Y;

    public static OwnerWalk Start(int fromX, int fromY, int toX, int toY, float speed, DateTimeOffset now) =>
        Along(fromX, fromY, [(toX, toY)], speed, now);

    public static OwnerWalk Along(int fromX, int fromY, IReadOnlyList<(int X, int Y)> points, float speed, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count == 0) throw new ArgumentException("A walk needs at least one point.", nameof(points));
        var length = 0.0;
        var (lastX, lastY) = (fromX, fromY);
        foreach (var (x, y) in points)
        {
            length += Distance(lastX, lastY, x, y);
            (lastX, lastY) = (x, y);
        }
        var travel = speed > 0 ? TimeSpan.FromMilliseconds(Math.Ceiling(length / speed * 1000.0)) : TimeSpan.Zero;
        return new OwnerWalk(fromX, fromY, points, speed, now, now + travel);
    }

    public (int X, int Y) PositionAt(DateTimeOffset now)
    {
        if (now >= ArrivesAt || ArrivesAt <= StartedAt || Speed <= 0) return (ToX, ToY);
        var left = Math.Max(0.0, (now - StartedAt).TotalMilliseconds) * Speed / 1000.0;
        var (lastX, lastY) = (FromX, FromY);
        foreach (var (x, y) in Points)
        {
            var leg = Distance(lastX, lastY, x, y);
            if (left < leg)
            {
                var part = left / leg;
                return ((int)Math.Round(lastX + (x - lastX) * part), (int)Math.Round(lastY + (y - lastY) * part));
            }
            left -= leg;
            (lastX, lastY) = (x, y);
        }
        return (ToX, ToY);
    }

    private static double Distance(int x1, int y1, int x2, int y2) => Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2));
}
