using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Net.World;

public enum PortalMoveOutcome
{
    Walking,
    Warped,
    WarpRefused,
    NoPortal,
}

public static class PortalMover
{
    public static async Task<PortalMoveOutcome> MoveAsync(
        IPortalLookup lookup,
        ushort map,
        (int X, int Y) standing,
        (int X, int Y) previous,
        Func<Task<bool>> tryWalk,
        Func<PortalDestination, Task<bool>> warp)
    {
        ArgumentNullException.ThrowIfNull(lookup);
        ArgumentNullException.ThrowIfNull(tryWalk);
        ArgumentNullException.ThrowIfNull(warp);
        if (await tryWalk().ConfigureAwait(false)) return PortalMoveOutcome.Walking;
        var destination = lookup.FindDestination(map, standing.X, standing.Y) ?? lookup.FindDestination(map, previous.X, previous.Y);
        if (destination is null) return PortalMoveOutcome.NoPortal;
        return await warp(destination).ConfigureAwait(false) ? PortalMoveOutcome.Warped : PortalMoveOutcome.WarpRefused;
    }
}
