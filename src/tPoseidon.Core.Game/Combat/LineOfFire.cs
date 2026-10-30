using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Combat;

public static class LineOfFire
{
    public const int StepLength = 16;

    public const int Unchecked = 0;
    public const int GunMode = 1;
    public const int TolerantMode = 2;
    public const int LevelGroundMode = 3;

    private const int GunTolerance = 3;
    private const int TolerantTolerance = 4;
    private const int LevelGroundTolerance = 3;

    public static bool IsClear(ZoneGeometry geometry, int mode, int startX, int startY, int endX, int endY)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        if (mode == Unchecked) return true;

        var fromX = unchecked((short)startX);
        var fromY = unchecked((short)startY);
        var toX = unchecked((short)endX);
        var toY = unchecked((short)endY);
        var dx = unchecked((short)(toX - fromX));
        var dy = unchecked((short)(toY - fromY));
        var highest = HighestAlong(geometry, fromX, fromY, toX, toY, dx, dy);
        return Passes(
            mode,
            geometry.HeightAtPosition(fromX, fromY),
            geometry.HeightAtPosition(toX, toY),
            highest);
    }

    public static bool Passes(int mode, int startHeight, int endHeight, int highestHeight) => mode switch
    {
        GunMode => !RisesAbove(startHeight, endHeight, highestHeight, GunTolerance),
        TolerantMode => !RisesAbove(startHeight, endHeight, highestHeight, TolerantTolerance),
        LevelGroundMode => startHeight == endHeight && highestHeight - startHeight <= LevelGroundTolerance,
        _ => false,
    };

    private static bool RisesAbove(int startHeight, int endHeight, int highestHeight, int tolerance)
        => startHeight < highestHeight && 2 * highestHeight - startHeight - endHeight > tolerance;

    private static byte HighestAlong(ZoneGeometry geometry, short fromX, short fromY, short toX, short toY, short dx, short dy)
    {
        byte highest = 0;
        var x = fromX;
        var y = fromY;
        if (dx == 0)
        {
            while (y != toY)
            {
                if (dy < 0)
                {
                    y = unchecked((short)(y - StepLength));
                    if (toY > y) break;
                }
                else
                {
                    y = unchecked((short)(y + StepLength));
                    if (toY < y) break;
                }
                highest = Math.Max(highest, geometry.HeightAtPosition(x, y));
            }
            return highest;
        }

        if (Math.Abs((int)dy) <= Math.Abs((int)dx))
        {
            var step = dx < 0 ? -StepLength : StepLength;
            while (dx < 0 ? x > toX : x < toX)
            {
                x = unchecked((short)(x + step));
                y = unchecked((short)(dy * (x - fromX) / dx + fromY));
                highest = Math.Max(highest, geometry.HeightAtPosition(x, y));
            }
            return highest;
        }

        var rowStep = dy < 0 ? -StepLength : StepLength;
        while (dy < 0 ? y > toY : y < toY)
        {
            y = unchecked((short)(y + rowStep));
            x = unchecked((short)(dx * (y - fromY) / dy + fromX));
            highest = Math.Max(highest, geometry.HeightAtPosition(x, y));
        }
        return highest;
    }
}
