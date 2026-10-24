namespace tPoseidon.Core.Net.World;

public sealed class WorldAreaOfInterest
{
    public const int DefaultRadius = 1500;

    private readonly int _radiusSquared;

    public WorldAreaOfInterest(int radius = DefaultRadius)
    {
        if (radius <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), radius, "AOI radius must be positive.");
        }
        Radius = radius;
        _radiusSquared = radius * radius;
    }

    public int Radius { get; }

    public bool IsInRange(int centerX, int centerY, int entityX, int entityY)
    {
        var dx = centerX - entityX;
        var dy = centerY - entityY;
        return ((long)dx * dx) + ((long)dy * dy) <= _radiusSquared;
    }
}
