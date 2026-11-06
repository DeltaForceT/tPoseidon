namespace tPoseidon.Core.Game.Monsters;

public static class ClientSpeed
{
    public const double Unit = 4.0;

    public static byte Quarter(float speed) => unchecked((byte)(int)(speed / Unit));

    public static float Drawn(float speed) => Quarter(speed) * (float)Unit;
}
