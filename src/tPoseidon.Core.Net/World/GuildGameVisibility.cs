namespace tPoseidon.Core.Net.World;

public static class GuildGameVisibility
{
    public static bool Hides(bool subjectObserves, bool viewerObserves) => subjectObserves && !viewerObserves;
}
