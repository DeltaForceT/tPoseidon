namespace tPoseidon.Core.Game.Drilling;

public static class ItemDetection
{
    public const int SampleStep = 16;
    public const int AlignStep = 32;
    public const int MostPositions = 50;
    public const double ScrollRatioAbove = 0.9;
    public const int ScrollDepth = 300;
    public const double DepthScale = 0.1;

    public static int DepthOf(int detectAbility, float depthRatio, float applyRatio) =>
        applyRatio > ScrollRatioAbove ? ScrollDepth : (int)(detectAbility * (double)depthRatio * DepthScale);

    public static IReadOnlyList<(ushort X, ushort Y)> Scan(
        int centreX,
        int centreY,
        int radius,
        int deepest,
        Func<int, int, bool> isOpenCell,
        Func<int, int, int?> laidDepthAt)
    {
        ArgumentNullException.ThrowIfNull(isOpenCell);
        ArgumentNullException.ThrowIfNull(laidDepthAt);
        var found = new List<(ushort X, ushort Y)>();
        var startX = centreX - radius;
        var startY = centreY - radius;
        startX = startX - startX % AlignStep + SampleStep;
        startY = startY - startY % AlignStep + SampleStep;
        var samples = radius * 2 / SampleStep;
        var y = startY;
        for (var row = 0; row < samples; row++)
        {
            var x = startX;
            for (var column = 0; column < samples; column++)
            {
                var cellX = x / SampleStep;
                var cellY = y / SampleStep;
                if (isOpenCell(cellX, cellY) && laidDepthAt(cellX, cellY) is { } depth && depth >= 0 && depth <= deepest)
                {
                    found.Add(((ushort)x, (ushort)y));
                    if (found.Count >= MostPositions) return found;
                }
                x += SampleStep;
            }
            y += SampleStep;
        }
        return found;
    }
}
