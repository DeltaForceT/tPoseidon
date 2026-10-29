namespace tPoseidon.Core.LibConfig;

public static class PortalArrivalRule
{
    public const ushort FallbackX = 500;
    public const ushort FallbackY = 400;

    public static (ushort X, ushort Y) Resolve(
        (ushort X, ushort Y)? fromSourceMap,
        IReadOnlyList<(ushort X, ushort Y)> entryPoints,
        Random rng)
    {
        ArgumentNullException.ThrowIfNull(entryPoints);
        ArgumentNullException.ThrowIfNull(rng);
        if (fromSourceMap is { } direct) return direct;
        if (entryPoints.Count > 0) return entryPoints[rng.Next(entryPoints.Count)];
        return (FallbackX, FallbackY);
    }
}
