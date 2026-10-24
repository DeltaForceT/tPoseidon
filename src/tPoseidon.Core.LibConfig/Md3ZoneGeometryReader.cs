using System.Buffers.Binary;

namespace tPoseidon.Core.LibConfig;

public static class Md3ZoneGeometryReader
{
    private const int Signature = 0x5F4E444D;
    private const int HeaderSize = 96;
    private const int FirstHeaderSlotOffset = 4;
    private const int HeaderSlotSize = 4;

    private const int ZoneRectFirstSlot = 5;
    private const int LayerCountSlot = 16;
    private const int RegionCountSlot = 17;
    private const int PointCountSlot = 18;

    private const int LayerHeaderSize = 20;
    private const int AttributeLayerType = 1;
    private const int GroundLayerType = 3;
    private const int RegionRecordSize = 32;
    private const int PointRecordSize = 24;

    private const int MaxLayers = 64;
    private const int MaxLayerSide = 8000;
    private const int MaxRegions = 100_000;
    private const int MaxPoints = 100_000;

    public static ZoneGeometry? Read(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (tPoseidon.Core.Common.ClientFiles.Locate(path) is not { } found) return null;
        return Read(File.ReadAllBytes(found));
    }

    public static ZoneGeometry? Read(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderSize) return null;
        if (BinaryPrimitives.ReadInt32LittleEndian(data) != Signature) return null;

        var layerCount = HeaderSlot(data, LayerCountSlot);
        var regionCount = HeaderSlot(data, RegionCountSlot);
        if (layerCount < 0 || layerCount > MaxLayers) return null;
        if (regionCount < 0 || regionCount > MaxRegions) return null;
        var pointCount = HeaderSlot(data, PointCountSlot);
        if (pointCount < 0 || pointCount > MaxPoints) return null;

        var offset = HeaderSize;
        byte[]? attributes = null;
        byte[]? groundTypes = null;
        var groundCellsX = 0;
        var cellsX = 0;
        var cellsY = 0;
        for (var layer = 0; layer < layerCount; layer++)
        {
            if (offset + LayerHeaderSize > data.Length) return null;
            var type = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset, 4));
            var width = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset + 4, 4));
            var height = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset + 8, 4));
            var cellWidth = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset + 12, 4));
            var cellHeight = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset + 16, 4));
            offset += LayerHeaderSize;

            if (width <= 0 || height <= 0 || width > MaxLayerSide || height > MaxLayerSide) return null;
            var bodyLength = width * height;
            if (offset + bodyLength > data.Length) return null;

            if (attributes is null
                && type == AttributeLayerType
                && cellWidth == ZoneGeometry.CellSize
                && cellHeight == ZoneGeometry.CellSize)
            {
                attributes = data.Slice(offset, bodyLength).ToArray();
                cellsX = width;
                cellsY = height;
            }
            else if (groundTypes is null
                && type == GroundLayerType
                && cellWidth == ZoneGeometry.CellSize
                && cellHeight == ZoneGeometry.CellSize)
            {
                groundTypes = data.Slice(offset, bodyLength).ToArray();
                groundCellsX = width;
            }

            offset += bodyLength;
        }

        if (attributes is null) return null;
        if (offset + regionCount * RegionRecordSize > data.Length) return null;

        var regions = new ZoneRegion[regionCount];
        for (var i = 0; i < regionCount; i++)
        {
            var record = data.Slice(offset + i * RegionRecordSize, RegionRecordSize);
            regions[i] = new ZoneRegion(
                Id: BinaryPrimitives.ReadInt32LittleEndian(record.Slice(0, 4)),
                Type: BinaryPrimitives.ReadInt32LittleEndian(record.Slice(4, 4)),
                Param: BinaryPrimitives.ReadInt32LittleEndian(record.Slice(8, 4)),
                X1: BinaryPrimitives.ReadInt32LittleEndian(record.Slice(12, 4)),
                Y1: BinaryPrimitives.ReadInt32LittleEndian(record.Slice(16, 4)),
                X2: BinaryPrimitives.ReadInt32LittleEndian(record.Slice(20, 4)),
                Y2: BinaryPrimitives.ReadInt32LittleEndian(record.Slice(24, 4)));
        }

        var pointsOffset = offset + regionCount * RegionRecordSize;
        if (pointsOffset + pointCount * PointRecordSize > data.Length) return null;
        var points = new ZonePoint[pointCount];
        for (var i = 0; i < pointCount; i++)
        {
            var record = data.Slice(pointsOffset + i * PointRecordSize, PointRecordSize);
            points[i] = new ZonePoint(
                Id: BinaryPrimitives.ReadInt32LittleEndian(record.Slice(0, 4)),
                Type: BinaryPrimitives.ReadInt32LittleEndian(record.Slice(4, 4)),
                Param: BinaryPrimitives.ReadInt32LittleEndian(record.Slice(8, 4)),
                X: BinaryPrimitives.ReadInt32LittleEndian(record.Slice(12, 4)),
                Y: BinaryPrimitives.ReadInt32LittleEndian(record.Slice(16, 4)));
        }

        return new ZoneGeometry
        {
            MinX = HeaderSlot(data, ZoneRectFirstSlot),
            MinY = HeaderSlot(data, ZoneRectFirstSlot + 1),
            MaxX = HeaderSlot(data, ZoneRectFirstSlot + 2),
            MaxY = HeaderSlot(data, ZoneRectFirstSlot + 3),
            CellsX = cellsX,
            CellsY = cellsY,
            CellAttributes = attributes,
            GroundTypes = groundTypes is not null && groundCellsX == cellsX && groundTypes.Length == attributes.Length ? groundTypes : Array.Empty<byte>(),
            Regions = regions,
            Points = points,
        };
    }

    private static int HeaderSlot(ReadOnlySpan<byte> data, int slot)
        => BinaryPrimitives.ReadInt32LittleEndian(
            data.Slice(FirstHeaderSlotOffset + slot * HeaderSlotSize, HeaderSlotSize));
}
