using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Quiz;

public enum OxVerdict
{
    OffTheFloor,
    Right,
    Wrong,
}

public static class OxQuizRules
{
    public const byte CircleFloor = 2;
    public const byte CrossFloor = 3;
    public const byte CircleAnswer = 0;
    public const int WrongSideDelayMilliseconds = 2000;

    public static readonly TimeSpan WrongSideDelay = TimeSpan.FromMilliseconds(WrongSideDelayMilliseconds);

    public static byte FloorAt(ZoneGeometry geometry, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        return geometry.SecondaryCellAtPosition(x, y);
    }

    public static OxVerdict Judge(byte floor, byte answer)
    {
        if (floor != CircleFloor && floor != CrossFloor) return OxVerdict.OffTheFloor;
        var answeredCircle = answer == CircleAnswer;
        return (floor == CircleFloor) == answeredCircle ? OxVerdict.Right : OxVerdict.Wrong;
    }
}
