using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Monsters;

public static class DropPlacement
{
    public const int ItemFirstRing = 4;
    public const int ItemReach = 6;
    public const int MoneyFirstRing = 7;
    public const int MoneyReach = 10;

    private const int LegsPerRing = 2;

    private static readonly (int X, int Y)[] Directions = { (0, -1), (-1, 0), (0, 1), (1, 0) };

    public static (ushort X, ushort Y) Place(
        ZoneGeometry? geometry,
        ushort fromX,
        ushort fromY,
        int firstRing,
        int reach,
        ISet<(int X, int Y)> takenCells,
        Random rng)
    {
        ArgumentNullException.ThrowIfNull(takenCells);
        ArgumentNullException.ThrowIfNull(rng);
        if (geometry is null) return (fromX, fromY);

        var cell = FindFreeCell(
            geometry, ZoneGeometry.CellOf(fromX), ZoneGeometry.CellOf(fromY), firstRing, reach, takenCells, rng);
        if (cell is not { } found) return (fromX, fromY);

        takenCells.Add(found);
        return ((ushort)ZoneGeometry.CentreOf(found.X), (ushort)ZoneGeometry.CentreOf(found.Y));
    }

    public static (int X, int Y)? FindFreeCell(
        ZoneGeometry geometry,
        int cellX,
        int cellY,
        int firstRing,
        int reach,
        ISet<(int X, int Y)> takenCells,
        Random rng)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(takenCells);
        ArgumentNullException.ThrowIfNull(rng);

        var width = geometry.CellsX;
        var height = geometry.CellsY;
        if (width < 1 || height < 1) return null;

        var x = cellX;
        var y = cellY;
        var direction = rng.Next(Directions.Length);
        var rings = Math.Min(reach, Math.Max(width, height));
        var half = rings / 2;
        var left = Math.Max(0, cellX - half);
        var right = Math.Min(width, cellX + half);
        var top = Math.Max(0, cellY - half);
        var bottom = Math.Min(height, cellY + half);

        var ring = 1;
        for (; ring < firstRing; ring++)
        {
            for (var leg = 0; leg < LegsPerRing; leg++)
            {
                for (var step = 0; step < ring; step++)
                {
                    x = (x + Directions[direction].X) % width;
                    y = (y + Directions[direction].Y) % height;
                }
                direction = (direction + 1) % Directions.Length;
            }
        }

        var skipped = rng.Next(Math.Max(1, rings * (rings - 1) / 2));
        for (; ring < rings; ring++)
        {
            for (var leg = 0; leg < LegsPerRing; leg++)
            {
                for (var step = 0; step < ring; step++)
                {
                    if (skipped > 0)
                    {
                        skipped--;
                    }
                    else if (x >= left && x < right && y >= top && y < bottom
                        && geometry.AttributeAt(x, y) != ZoneGeometry.CellBlocked
                        && !takenCells.Contains((x, y)))
                    {
                        return (x, y);
                    }
                    x = (x + Directions[direction].X) % width;
                    y = (y + Directions[direction].Y) % height;
                }
                direction = (direction + 1) % Directions.Length;
            }
        }
        return null;
    }
}
