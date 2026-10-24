using System.Buffers.Binary;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.LibConfig.Tests;

public sealed class Md3ZoneGeometryReaderTests
{
    private const int HeaderSlots = 23;
    private const int ZoneRectFirstSlot = 5;
    private const int LayerCountSlot = 16;
    private const int RegionCountSlot = 17;
    private const int PointCountSlot = 18;

    private static byte[] MapFile(
        (int Type, int Width, int Height, int CellSize, byte[] Body)[] layers,
        int[][] regions,
        int maxX = 63,
        int maxY = 47,
        int[][]? points = null)
    {
        var bytes = new List<byte>();
        void Int(int value)
        {
            Span<byte> buffer = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
            bytes.AddRange(buffer.ToArray());
        }

        bytes.AddRange("MDN_"u8.ToArray());
        var header = new int[HeaderSlots];
        header[ZoneRectFirstSlot + 2] = maxX;
        header[ZoneRectFirstSlot + 3] = maxY;
        header[LayerCountSlot] = layers.Length;
        header[RegionCountSlot] = regions.Length;
        header[PointCountSlot] = points?.Length ?? 0;
        foreach (var slot in header) Int(slot);

        foreach (var layer in layers)
        {
            Int(layer.Type);
            Int(layer.Width);
            Int(layer.Height);
            Int(layer.CellSize);
            Int(layer.CellSize);
            bytes.AddRange(layer.Body);
        }
        foreach (var region in regions)
        {
            foreach (var field in region) Int(field);
        }
        foreach (var point in points ?? Array.Empty<int[]>())
        {
            foreach (var field in point) Int(field);
        }
        return bytes.ToArray();
    }

    private static byte[] Cells(int count, params (int Index, byte Value)[] marked)
    {
        var cells = new byte[count];
        foreach (var (index, value) in marked) cells[index] = value;
        return cells;
    }

    [Fact]
    public void ReadsTheAttributeLayerTheZoneRectAndTheRegions()
    {
        var file = MapFile(
            new[]
            {
                (Type: 3, Width: 2, Height: 2, CellSize: 16, Body: Cells(4)),
                (Type: 1, Width: 4, Height: 3, CellSize: 16, Body: Cells(12, (5, ZoneGeometry.CellBlocked))),
            },
            new[]
            {
                new[] { 0, ZoneGeometry.RegionTypePortal, 111, 10, 20, 30, 40, 0 },
                new[] { 4, ZoneGeometry.RegionTypeMonsterMovement, 0, 5, 6, 25, 26, 0 },
            });

        var geometry = Md3ZoneGeometryReader.Read(file);

        Assert.NotNull(geometry);
        Assert.Equal((0, 0, 63, 47), (geometry.MinX, geometry.MinY, geometry.MaxX, geometry.MaxY));
        Assert.Equal((4, 3), (geometry.CellsX, geometry.CellsY));
        Assert.True(geometry.IsBlocked(1, 1));
        Assert.False(geometry.IsBlocked(0, 0));
        Assert.Equal(new ZoneRegion(0, ZoneGeometry.RegionTypePortal, 111, 10, 20, 30, 40), geometry.Regions[0]);
        Assert.Equal(
            new ZoneRegion(4, ZoneGeometry.RegionTypeMonsterMovement, 0, 5, 6, 25, 26),
            geometry.FindRegion(ZoneGeometry.RegionTypeMonsterMovement, 4));
        Assert.Null(geometry.FindRegion(ZoneGeometry.RegionTypeMonsterMovement, 0));
    }

    [Fact]
    public void ReadsThePointRecordsThatFollowTheRegions()
    {
        var file = MapFile(
            new[] { (Type: 1, Width: 4, Height: 3, CellSize: 16, Body: Cells(12)) },
            new[] { new[] { 4, ZoneGeometry.RegionTypeMonsterMovement, 0, 5, 6, 25, 26, 0 } },
            points: new[]
            {
                new[] { 0, ZoneGeometry.PointTypeEntry, 33, 100, 200, 0 },
                new[] { 4, ZoneGeometry.PointTypeRegionSpawn, 0, 12, 14, 0 },
                new[] { 1, ZoneGeometry.PointTypeRestart, 0, 300, 400, 0 },
            });

        var geometry = Md3ZoneGeometryReader.Read(file);

        Assert.NotNull(geometry);
        Assert.Equal(
            new[]
            {
                new ZonePoint(0, ZoneGeometry.PointTypeEntry, 33, 100, 200),
                new ZonePoint(4, ZoneGeometry.PointTypeRegionSpawn, 0, 12, 14),
                new ZonePoint(1, ZoneGeometry.PointTypeRestart, 0, 300, 400),
            },
            geometry.Points);
        Assert.Equal(
            new[] { new ZonePoint(1, ZoneGeometry.PointTypeRestart, 0, 300, 400) },
            geometry.PointsOfType(ZoneGeometry.PointTypeRestart));
    }

    [Fact]
    public void PointCountBeyondTheFileHasNoGeometry()
    {
        var file = MapFile(
            new[] { (Type: 1, Width: 4, Height: 3, CellSize: 16, Body: Cells(12)) },
            Array.Empty<int[]>());
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(4 + PointCountSlot * 4, 4), 2);

        Assert.Null(Md3ZoneGeometryReader.Read(file));
    }

    [Fact]
    public void PortalOverlapIsStrict()
    {
        var file = MapFile(
            new[] { (Type: 1, Width: 4, Height: 3, CellSize: 16, Body: Cells(12)) },
            new[] { new[] { 0, ZoneGeometry.RegionTypePortal, 111, 10, 20, 30, 40, 0 } });

        var geometry = Md3ZoneGeometryReader.Read(file);

        Assert.NotNull(geometry);
        Assert.True(geometry.OverlapsRegionOfType(ZoneGeometry.RegionTypePortal, 29, 39, 60, 60));
        Assert.False(geometry.OverlapsRegionOfType(ZoneGeometry.RegionTypePortal, 30, 20, 60, 60));
        Assert.False(geometry.OverlapsRegionOfType(ZoneGeometry.RegionTypeMonsterMovement, 0, 0, 60, 60));
    }

    [Fact]
    public void FileWithoutAnAttributeLayerHasNoGeometry()
    {
        var file = MapFile(
            new[] { (Type: 3, Width: 2, Height: 2, CellSize: 16, Body: Cells(4)) },
            Array.Empty<int[]>());

        Assert.Null(Md3ZoneGeometryReader.Read(file));
    }

    [Fact]
    public void WrongSignatureHasNoGeometry()
    {
        var file = MapFile(
            new[] { (Type: 1, Width: 2, Height: 2, CellSize: 16, Body: Cells(4)) },
            Array.Empty<int[]>());
        file[0] = (byte)'X';

        Assert.Null(Md3ZoneGeometryReader.Read(file));
    }
}
