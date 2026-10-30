namespace tPoseidon.Core.Game.ShadowWorld;

public readonly record struct StatueState(ushort EntityId, int X, int Y, long CurrentHp, long MaxHp);

public static class ShadowStatues
{
    public const int MonsterId = 2659;
    public const int StatueCount = 5;
    public const int FirstStatueRegion = 1;
    public const int NearbyPlayerRadius = 300;

    public static int PlayersNear(StatueState statue, IEnumerable<(int X, int Y)> players) =>
        players.Count(player => DistanceSquared(statue.X, statue.Y, player.X, player.Y) <= (long)NearbyPlayerRadius * NearbyPlayerRadius);

    private static long DistanceSquared(int ax, int ay, int bx, int by)
    {
        long dx = ax - bx;
        long dy = ay - by;
        return dx * dx + dy * dy;
    }
}
