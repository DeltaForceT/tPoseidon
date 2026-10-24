using System.Buffers.Binary;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.LibConfig.Tests;

public class Md3MapReaderTests
{
    [Fact]
    public void ReadWalkableLayer_ReturnsNullOnTooSmallBuffer()
    {
        Assert.Null(Md3MapReader.ReadWalkableLayer(new byte[10]));
    }

    [Fact]
    public void ReadWalkableLayer_ReturnsNullOnWrongSignature()
    {
        var buf = new byte[200];
        Assert.Null(Md3MapReader.ReadWalkableLayer(buf));
    }

    [Fact]
    public void ReadWalkableLayer_ParsesSyntheticHeaderAndLayer()
    {
        const int cellsX = 6;
        const int cellsY = 4;
        const int cellUnits = 16;
        const int mapW = cellsX * cellUnits;
        const int mapH = cellsY * cellUnits;

        var buf = new byte[96 + 20 + cellsX * cellsY];
        buf[0] = (byte)'M'; buf[1] = (byte)'D'; buf[2] = (byte)'N'; buf[3] = (byte)'_';

        WriteI32(buf, 4 + 7 * 4, mapW);
        WriteI32(buf, 4 + 8 * 4, mapH);
        WriteI32(buf, 4 + 16 * 4, 1);

        WriteI32(buf, 96, 1);
        WriteI32(buf, 96 + 4, cellsX);
        WriteI32(buf, 96 + 8, cellsY);
        WriteI32(buf, 96 + 12, cellUnits);
        WriteI32(buf, 96 + 16, cellUnits);

        for (var i = 0; i < cellsX * cellsY; i++)
        {
            buf[96 + 20 + i] = (i % 2 == 0) ? (byte)1 : (byte)0;
        }

        var layer = Md3MapReader.ReadWalkableLayer(buf);
        Assert.NotNull(layer);
        Assert.Equal(mapW, layer!.MapSizeX);
        Assert.Equal(mapH, layer.MapSizeY);
        Assert.Equal(cellsX, layer.CellsX);
        Assert.Equal(cellsY, layer.CellsY);
        Assert.Equal(cellUnits, layer.CellWorldUnits);
        Assert.Equal(cellsX * cellsY, layer.Cells.Length);
        Assert.True(layer.IsWalkable(0, 0));
        Assert.False(layer.IsWalkable(1, 0));
        Assert.False(layer.IsWalkable(-1, 0));
        Assert.False(layer.IsWalkable(0, -1));
        Assert.False(layer.IsWalkable(cellsX, 0));
        Assert.False(layer.IsWalkable(0, cellsY));
    }

    [Fact]
    public void ReadWalkableLayer_ReturnsNullWhenLayerCountIsZero()
    {
        var buf = new byte[96];
        buf[0] = (byte)'M'; buf[1] = (byte)'D'; buf[2] = (byte)'N'; buf[3] = (byte)'_';
        WriteI32(buf, 4 + 7 * 4, 100);
        WriteI32(buf, 4 + 8 * 4, 100);
        WriteI32(buf, 4 + 16 * 4, 0);
        Assert.Null(Md3MapReader.ReadWalkableLayer(buf));
    }

    [Fact]
    public void ReadMap_ParsesRangesAndPointsAfterLayer()
    {
        const int cellsX = 4;
        const int cellsY = 4;
        const int cellUnits = 8;
        const int mapW = cellsX * cellUnits;
        const int mapH = cellsY * cellUnits;
        const int rangeCount = 2;
        const int pointCount = 3;

        var totalSize = 96
            + 20 + cellsX * cellsY
            + rangeCount * 32
            + pointCount * 24;
        var buf = new byte[totalSize];
        buf[0] = (byte)'M'; buf[1] = (byte)'D'; buf[2] = (byte)'N'; buf[3] = (byte)'_';

        WriteI32(buf, 4 + 7 * 4, mapW);
        WriteI32(buf, 4 + 8 * 4, mapH);
        WriteI32(buf, 4 + 16 * 4, 1);
        WriteI32(buf, 4 + 17 * 4, rangeCount);
        WriteI32(buf, 4 + 18 * 4, pointCount);
        WriteI32(buf, 4 + 19 * 4, 0);

        WriteI32(buf, 96, 1);
        WriteI32(buf, 96 + 4, cellsX);
        WriteI32(buf, 96 + 8, cellsY);
        WriteI32(buf, 96 + 12, cellUnits);
        WriteI32(buf, 96 + 16, cellUnits);
        for (var i = 0; i < cellsX * cellsY; i++) buf[96 + 20 + i] = 1;

        var rangeOff = 96 + 20 + cellsX * cellsY;

        WriteI32(buf, rangeOff + 0, 0);
        WriteI32(buf, rangeOff + 4, 100);
        WriteI32(buf, rangeOff + 8, 999);
        WriteI32(buf, rangeOff + 12, 10);
        WriteI32(buf, rangeOff + 16, 20);
        WriteI32(buf, rangeOff + 20, 30);
        WriteI32(buf, rangeOff + 24, 40);
        WriteI32(buf, rangeOff + 28, 0);

        WriteI32(buf, rangeOff + 32 + 0, 9);
        WriteI32(buf, rangeOff + 32 + 4, 200);
        WriteI32(buf, rangeOff + 32 + 8, 0);
        WriteI32(buf, rangeOff + 32 + 12, 50);
        WriteI32(buf, rangeOff + 32 + 16, 60);
        WriteI32(buf, rangeOff + 32 + 20, 70);
        WriteI32(buf, rangeOff + 32 + 24, 80);
        WriteI32(buf, rangeOff + 32 + 28, 0);

        var pointOff = rangeOff + rangeCount * 32;

        WriteI32(buf, pointOff + 0, 1);
        WriteI32(buf, pointOff + 4, 1);
        WriteI32(buf, pointOff + 8, 42);
        WriteI32(buf, pointOff + 12, 379);
        WriteI32(buf, pointOff + 16, 818);
        WriteI32(buf, pointOff + 20, 0);

        WriteI32(buf, pointOff + 24 + 0, 8001);
        WriteI32(buf, pointOff + 24 + 4, 4);
        WriteI32(buf, pointOff + 24 + 8, 0);
        WriteI32(buf, pointOff + 24 + 12, 500);
        WriteI32(buf, pointOff + 24 + 16, 600);
        WriteI32(buf, pointOff + 24 + 20, 0);

        WriteI32(buf, pointOff + 48 + 0, 9001);
        WriteI32(buf, pointOff + 48 + 4, 3);
        WriteI32(buf, pointOff + 48 + 8, 0);
        WriteI32(buf, pointOff + 48 + 12, 100);
        WriteI32(buf, pointOff + 48 + 16, 200);
        WriteI32(buf, pointOff + 48 + 20, 0);

        var map = Md3MapReader.ReadMap(buf);
        Assert.NotNull(map);
        Assert.Equal(mapW, map!.MapSizeX);
        Assert.Equal(mapH, map.MapSizeY);
        Assert.NotNull(map.WalkableLayer);

        Assert.Equal(2, map.Ranges.Count);
        Assert.Equal(0, map.Ranges[0].Type);
        Assert.Equal(999, map.Ranges[0].Destination);
        Assert.Equal((10, 20, 30, 40), (map.Ranges[0].X1, map.Ranges[0].Y1, map.Ranges[0].X2, map.Ranges[0].Y2));
        Assert.Equal(9, map.Ranges[1].Type);
        Assert.Equal(200, map.Ranges[1].Id);

        Assert.Equal(3, map.Points.Count);
        Assert.Equal((1, 1, 42, 379, 818), (map.Points[0].Id, map.Points[0].Type, map.Points[0].MapId, map.Points[0].X, map.Points[0].Y));
        Assert.Equal((8001, 4, 0, 500, 600), (map.Points[1].Id, map.Points[1].Type, map.Points[1].MapId, map.Points[1].X, map.Points[1].Y));
        Assert.Equal((9001, 3, 0, 100, 200), (map.Points[2].Id, map.Points[2].Type, map.Points[2].MapId, map.Points[2].X, map.Points[2].Y));
    }

    [Fact]
    public void ReadMap_ReturnsNullOnTruncatedRangesSection()
    {
        var buf = new byte[96 + 20];
        buf[0] = (byte)'M'; buf[1] = (byte)'D'; buf[2] = (byte)'N'; buf[3] = (byte)'_';
        WriteI32(buf, 4 + 7 * 4, 100);
        WriteI32(buf, 4 + 8 * 4, 100);
        WriteI32(buf, 4 + 16 * 4, 0);
        WriteI32(buf, 4 + 17 * 4, 5);
        Assert.Null(Md3MapReader.ReadMap(buf));
    }

    private static void WriteI32(byte[] buf, int off, int value) =>
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(off, 4), value);
}
