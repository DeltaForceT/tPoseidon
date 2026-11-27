namespace tPoseidon.Core.Game.Quests;

public static class MonsterHuntClock
{
    public static int ElapsedMs(bool frozen, DateTimeOffset lastTick, DateTimeOffset now)
    {
        if (frozen || lastTick == default) return 0;
        return (int)Math.Clamp((now - lastTick).TotalMilliseconds, 0, int.MaxValue);
    }
}
