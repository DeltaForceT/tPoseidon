using System.Buffers.Binary;

namespace tPoseidon.Core.LibConfig;

public sealed class Md3WalkableLayer
{
    public required int MapSizeX { get; init; }
    public required int MapSizeY { get; init; }
    public required int CellsX { get; init; }
    public required int CellsY { get; init; }
    public required int CellWorldUnits { get; init; }
    public required byte[] Cells { get; init; }

    public bool IsWalkable(int cellX, int cellY)
    {
        if ((uint)cellX >= (uint)CellsX) return false;
        if ((uint)cellY >= (uint)CellsY) return false;
        return Cells[cellY * CellsX + cellX] != 0;
    }
}

public sealed record Md3RangeObject(
    int Type,
    int Id,
    int Destination,
    int X1, int Y1,
    int X2, int Y2,
    int Options);

public sealed record Md3PointObject(
    int Id,
    int Type,
    int MapId,
    int X, int Y,
    int Options);

public sealed class Md3Map
{
    public required int MapSizeX { get; init; }
    public required int MapSizeY { get; init; }
        public Md3WalkableLayer? WalkableLayer { get; init; }
    public required IReadOnlyList<Md3RangeObject> Ranges { get; init; }
    public required IReadOnlyList<Md3PointObject> Points { get; init; }
}

public static class Md3MapReader
{
    private const int Md3Signature = 0x5F4E444D;

    public static Md3WalkableLayer? ReadWalkableLayer(ReadOnlySpan<byte> data)
    {
        var map = ReadMap(data);
        return map?.WalkableLayer;
    }

        public static Md3Map? ReadMap(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (tPoseidon.Core.Common.ClientFiles.Locate(path) is not { } found) return null;
        var data = File.ReadAllBytes(found);
        return ReadMap(data);
    }

    public static Md3Map? ReadMap(ReadOnlySpan<byte> data)
    {
        if (data.Length < 96) return null;
        if (BinaryPrimitives.ReadInt32LittleEndian(data) != Md3Signature) return null;

        var mapSizeX = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(4 + 7 * 4, 4));
        var mapSizeY = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(4 + 8 * 4, 4));
        var layerCount = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(4 + 16 * 4, 4));
        var rangeCount = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(4 + 17 * 4, 4));
        var pointCount = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(4 + 18 * 4, 4));
        var effectCount = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(4 + 19 * 4, 4));

        if (mapSizeX <= 0 || mapSizeY <= 0) return null;
        if (mapSizeX > 100_000 || mapSizeY > 100_000) return null;
        if (layerCount < 0 || rangeCount < 0 || pointCount < 0 || effectCount < 0) return null;
        if (layerCount > 64 || rangeCount > 100_000 || pointCount > 100_000 || effectCount > 100_000) return null;

        var off = 96;
        Md3WalkableLayer? walkable = null;

        for (var li = 0; li < layerCount; li++)
        {
            if (off + 20 > data.Length) return null;
            var type = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(off, 4));
            var x = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(off + 4, 4));
            var y = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(off + 8, 4));
            var bppX = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(off + 12, 4));
            var bppY = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(off + 16, 4));
            off += 20;

            if (x <= 0 || y <= 0 || x > 8000 || y > 8000) return null;
            var bodyLen = x * y;
            if (off + bodyLen > data.Length) return null;

            if (walkable is null && type == 1 && bppX > 0 && bppY > 0 && bppX == bppY)
            {
                var cells = new byte[bodyLen];
                data.Slice(off, bodyLen).CopyTo(cells);
                walkable = new Md3WalkableLayer
                {
                    MapSizeX = mapSizeX,
                    MapSizeY = mapSizeY,
                    CellsX = x,
                    CellsY = y,
                    CellWorldUnits = bppX,
                    Cells = cells,
                };
            }

            off += bodyLen;
        }

        if (off + rangeCount * 32 > data.Length) return null;
        var ranges = new Md3RangeObject[rangeCount];
        for (var i = 0; i < rangeCount; i++)
        {
            var b = data.Slice(off, 32);
            ranges[i] = new Md3RangeObject(
                Type:        BinaryPrimitives.ReadInt32LittleEndian(b.Slice(0, 4)),
                Id:          BinaryPrimitives.ReadInt32LittleEndian(b.Slice(4, 4)),
                Destination: BinaryPrimitives.ReadInt32LittleEndian(b.Slice(8, 4)),
                X1:          BinaryPrimitives.ReadInt32LittleEndian(b.Slice(12, 4)),
                Y1:          BinaryPrimitives.ReadInt32LittleEndian(b.Slice(16, 4)),
                X2:          BinaryPrimitives.ReadInt32LittleEndian(b.Slice(20, 4)),
                Y2:          BinaryPrimitives.ReadInt32LittleEndian(b.Slice(24, 4)),
                Options:     BinaryPrimitives.ReadInt32LittleEndian(b.Slice(28, 4)));
            off += 32;
        }

        if (off + pointCount * 24 > data.Length) return null;
        var points = new Md3PointObject[pointCount];
        for (var i = 0; i < pointCount; i++)
        {
            var b = data.Slice(off, 24);
            points[i] = new Md3PointObject(
                Id:      BinaryPrimitives.ReadInt32LittleEndian(b.Slice(0, 4)),
                Type:    BinaryPrimitives.ReadInt32LittleEndian(b.Slice(4, 4)),
                MapId:   BinaryPrimitives.ReadInt32LittleEndian(b.Slice(8, 4)),
                X:       BinaryPrimitives.ReadInt32LittleEndian(b.Slice(12, 4)),
                Y:       BinaryPrimitives.ReadInt32LittleEndian(b.Slice(16, 4)),
                Options: BinaryPrimitives.ReadInt32LittleEndian(b.Slice(20, 4)));
            off += 24;
        }



        return new Md3Map
        {
            MapSizeX = mapSizeX,
            MapSizeY = mapSizeY,
            WalkableLayer = walkable,
            Ranges = ranges,
            Points = points,
        };
    }
}
