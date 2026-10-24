namespace tPoseidon.Core.LibConfig;

public readonly record struct ZoneRegion(int Id, int Type, int Param, int X1, int Y1, int X2, int Y2);

public readonly record struct ZonePoint(int Id, int Type, int Param, int X, int Y);

public sealed class ZoneGeometry
{
    public const int CellSize = 16;
    public const int CellCentreOffset = 8;

    public const int RegionTypePortal = 1;
    public const int RegionTypeMonsterMovement = 2;

    public const int PointTypeEntry = 1;
    public const int PointTypeRestart = 2;
    public const int PointTypeSpawn = 3;
    public const int PointTypeRegionSpawn = 6;
    public const int PointTypeTeleportArrival = 7;

    public const byte CellBlocked = 1;
    public const byte CellConditionallyBlocked = 2;
    public const byte CellNoDestination = 3;

    public required int MinX { get; init; }
    public required int MinY { get; init; }
    public required int MaxX { get; init; }
    public required int MaxY { get; init; }
    public required int CellsX { get; init; }
    public required int CellsY { get; init; }
    public required byte[] CellAttributes { get; init; }
    public required IReadOnlyList<ZoneRegion> Regions { get; init; }
    public IReadOnlyList<ZonePoint> Points { get; init; } = Array.Empty<ZonePoint>();
    public byte[] GroundTypes { get; init; } = Array.Empty<byte>();

    public bool HasGroundTypes => GroundTypes.Length == CellsX * CellsY && GroundTypes.Length > 0;

    public byte GroundTypeAt(int cellX, int cellY) => GroundTypes[cellY * CellsX + cellX];

    public IReadOnlyList<ZonePoint> PointsOfType(int type)
    {
        var matches = new List<ZonePoint>();
        foreach (var point in Points)
        {
            if (point.Type == type) matches.Add(point);
        }
        return matches;
    }

    public static int CellOf(int worldCoordinate) => worldCoordinate / CellSize;

    public static int CentreOf(int cell) => cell * CellSize + CellCentreOffset;

    public bool ContainsCell(int cellX, int cellY)
        => (uint)cellX < (uint)CellsX && (uint)cellY < (uint)CellsY;

    public byte AttributeAt(int cellX, int cellY) => CellAttributes[cellY * CellsX + cellX];

    public bool IsBlocked(int cellX, int cellY)
    {
        var attribute = AttributeAt(cellX, cellY);
        return attribute == CellBlocked || attribute == CellConditionallyBlocked;
    }

    public bool IsFreeDestination(int cellX, int cellY)
    {
        var attribute = AttributeAt(cellX, cellY);
        return attribute != CellBlocked
            && attribute != CellConditionallyBlocked
            && attribute != CellNoDestination;
    }

    public ZoneRegion? FindRegion(int type, int id)
    {
        foreach (var region in Regions)
        {
            if (region.Type == type && region.Id == id) return region;
        }
        return null;
    }

    public bool OverlapsRegionOfType(int type, int x1, int y1, int x2, int y2)
    {
        foreach (var region in Regions)
        {
            if (region.Type != type) continue;
            if (region.X1 < x2 && x1 < region.X2 && region.Y1 < y2 && y1 < region.Y2) return true;
        }
        return false;
    }
}
