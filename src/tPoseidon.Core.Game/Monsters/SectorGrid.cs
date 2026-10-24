using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Monsters;

public readonly struct SectorGrid
{
    public const int SectorWidth = 600;
    public const int SectorHeight = 500;

    private const int SectorSizeLimit = 10922;
    private const int SmallestSectorCountPerAxis = 4;

    public SectorGrid(ZoneGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        Columns = SectorsAlong(Extent(geometry.CellsX), SectorWidth);
        Rows = SectorsAlong(Extent(geometry.CellsY), SectorHeight);
    }

    public int Columns { get; }

    public int Rows { get; }

    public (int Column, int Row) SectorOf(int x, int y)
        => (Index(x, SectorWidth, Columns), Index(y, SectorHeight, Rows));

    public static readonly (int Column, int Row)[] NeighbourOffsets =
    {
        (0, 0), (-1, 0), (-1, -1), (0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1),
    };

    public bool IsSingleSector => Columns == 1 && Rows == 1;

    public IReadOnlyList<(int Column, int Row)> Neighbourhood((int Column, int Row) sector)
    {
        var near = new List<(int Column, int Row)>(NeighbourOffsets.Length);
        foreach (var (columnOffset, rowOffset) in NeighbourOffsets)
        {
            var column = sector.Column + columnOffset;
            var row = sector.Row + rowOffset;
            if (column < 0 || column >= Columns || row < 0 || row >= Rows) continue;
            near.Add((column, row));
        }
        return near;
    }

    public static bool Sees((int Column, int Row) viewer, (int Column, int Row) target)
        => Math.Abs(viewer.Column - target.Column) <= 1 && Math.Abs(viewer.Row - target.Row) <= 1;

    public bool Sees(int viewerX, int viewerY, int x, int y) => Sees(SectorOf(viewerX, viewerY), SectorOf(x, y));

    public (IReadOnlyList<(int Column, int Row)> Leaving, IReadOnlyList<(int Column, int Row)> Entering) Change(
        (int Column, int Row) from, (int Column, int Row) to)
    {
        var before = Neighbourhood(from);
        var after = Neighbourhood(to);
        return (before.Where(sector => !Sees(to, sector)).ToList(), after.Where(sector => !Sees(from, sector)).ToList());
    }

    public HashSet<(int Column, int Row)> SectorsWatchedFrom(IEnumerable<(int X, int Y)> positions)
    {
        ArgumentNullException.ThrowIfNull(positions);
        var watched = new HashSet<(int Column, int Row)>();
        foreach (var (x, y) in positions) watched.UnionWith(Neighbourhood(SectorOf(x, y)));
        return watched;
    }

    private static int Extent(int cells) => cells * ZoneGeometry.CellSize + ZoneGeometry.CellCentreOffset;

    private static int SectorsAlong(int extent, int sectorSize)
        => sectorSize < SectorSizeLimit && sectorSize * SmallestSectorCountPerAxis < extent
            ? extent / sectorSize
            : 1;

    private static int Index(int coordinate, int sectorSize, int count)
        => count == 1 ? 0 : Math.Clamp(coordinate / sectorSize, 0, count - 1);
}
