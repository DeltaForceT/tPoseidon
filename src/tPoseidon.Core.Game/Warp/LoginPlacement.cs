using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Warp;

public static class LoginPlacement
{
    public static bool IsValid(ZoneGeometry? geometry, int x, int y)
    {
        if (x < 1 || y < 1) return false;
        if (geometry is null) return true;
        if (x >= geometry.CellsX * ZoneGeometry.CellSize || y >= geometry.CellsY * ZoneGeometry.CellSize) return false;
        return geometry.AttributeAt(ZoneGeometry.CellOf(x), ZoneGeometry.CellOf(y)) != ZoneGeometry.CellBlocked;
    }
}
