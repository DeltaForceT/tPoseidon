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
        (int X, int Y) current,
        int margin,
        Func<Task<bool>> tryWalk,
        Func<PortalDestination, Task<bool>> warp)
    {
        ArgumentNullException.ThrowIfNull(lookup);
        ArgumentNullException.ThrowIfNull(tryWalk);
        ArgumentNullException.ThrowIfNull(warp);
        if (await tryWalk().ConfigureAwait(false)) return PortalMoveOutcome.Walking;
        var destination = lookup.FindDestinationWithin(map, current.X, current.Y, margin);
        if (destination is null) return PortalMoveOutcome.NoPortal;
        return await warp(destination).ConfigureAwait(false) ? PortalMoveOutcome.Warped : PortalMoveOutcome.WarpRefused;
    }
}
