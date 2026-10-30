using System.Buffers.Binary;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Hosting.ClientScan;

public sealed class Md3Document
{
    public const int Signature = 0x5F4E444D;
    public const int HeaderBytes = 96;
    public const int LayerHeaderBytes = 20;
    public const int RangeBytes = 32;
    public const int PointBytes = 24;

    private const int LayerCountOffset = 4 + 16 * 4;
    private const int RangeCountOffset = 4 + 17 * 4;
    private const int PointCountOffset = 4 + 18 * 4;
    private const int MaxLayerSide = 8000;
    private const int MaxLayers = 64;
    private const int MaxRecords = 100_000;

    private readonly byte[] _header;
    private readonly byte[] _layers;
    private readonly byte[] _tail;

    private Md3Document(byte[] header, byte[] layers, List<Md3RangeObject> ranges, List<Md3PointObject> points, byte[] tail)
    {
        _header = header;
        _layers = layers;
        Ranges = ranges;
        Points = points;
        _tail = tail;
    }

    public List<Md3RangeObject> Ranges { get; }

    public List<Md3PointObject> Points { get; }

    public static Md3Document? Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderBytes || BinaryPrimitives.ReadInt32LittleEndian(data) != Signature)
        {
            return null;
        }

        var layerCount = BinaryPrimitives.ReadInt32LittleEndian(data[LayerCountOffset..]);
        var rangeCount = BinaryPrimitives.ReadInt32LittleEndian(data[RangeCountOffset..]);
        var pointCount = BinaryPrimitives.ReadInt32LittleEndian(data[PointCountOffset..]);
        if (layerCount is < 0 or > MaxLayers || rangeCount is < 0 or > MaxRecords || pointCount is < 0 or > MaxRecords)
        {
            return null;
        }

        var offset = HeaderBytes;
        for (var layer = 0; layer < layerCount; layer++)
        {
            if (offset + LayerHeaderBytes > data.Length)
            {
                return null;
            }

            var columns = BinaryPrimitives.ReadInt32LittleEndian(data[(offset + 4)..]);
            var rows = BinaryPrimitives.ReadInt32LittleEndian(data[(offset + 8)..]);
            if (columns is <= 0 or > MaxLayerSide || rows is <= 0 or > MaxLayerSide)
            {
                return null;
            }

            offset += LayerHeaderBytes + columns * rows;
            if (offset > data.Length)
            {
                return null;
            }
        }

        var rangesAt = offset;
        var pointsAt = rangesAt + rangeCount * RangeBytes;
        var tailAt = pointsAt + pointCount * PointBytes;
        if (tailAt > data.Length)
        {
            return null;
        }

        var ranges = new List<Md3RangeObject>(rangeCount);
        for (var i = 0; i < rangeCount; i++)
        {
            var record = data.Slice(rangesAt + i * RangeBytes, RangeBytes);
            ranges.Add(new Md3RangeObject(Int(record, 0), Int(record, 1), Int(record, 2), Int(record, 3), Int(record, 4), Int(record, 5), Int(record, 6), Int(record, 7)));
        }

        var points = new List<Md3PointObject>(pointCount);
        for (var i = 0; i < pointCount; i++)
        {
            var record = data.Slice(pointsAt + i * PointBytes, PointBytes);
            points.Add(new Md3PointObject(Int(record, 0), Int(record, 1), Int(record, 2), Int(record, 3), Int(record, 4), Int(record, 5)));
        }

        return new Md3Document(
            data[..HeaderBytes].ToArray(),
            data[HeaderBytes..rangesAt].ToArray(),
            ranges,
            points,
            data[tailAt..].ToArray());
    }

    public byte[] ToBytes()
    {
        var result = new byte[HeaderBytes + _layers.Length + Ranges.Count * RangeBytes + Points.Count * PointBytes + _tail.Length];
        _header.CopyTo(result, 0);
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(RangeCountOffset), Ranges.Count);
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(PointCountOffset), Points.Count);
        var offset = HeaderBytes;
        _layers.CopyTo(result, offset);
        offset += _layers.Length;
        foreach (var range in Ranges)
        {
            WriteInts(result.AsSpan(offset, RangeBytes), range.Type, range.Id, range.Destination, range.X1, range.Y1, range.X2, range.Y2, range.Options);
            offset += RangeBytes;
        }

        foreach (var point in Points)
        {
            WriteInts(result.AsSpan(offset, PointBytes), point.Id, point.Type, point.MapId, point.X, point.Y, point.Options);
            offset += PointBytes;
        }

        _tail.CopyTo(result, offset);
        return result;
    }

    private static int Int(ReadOnlySpan<byte> record, int index) => BinaryPrimitives.ReadInt32LittleEndian(record[(index * sizeof(int))..]);

    private static void WriteInts(Span<byte> record, params int[] values)
    {
        for (var i = 0; i < values.Length; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(record[(i * sizeof(int))..], values[i]);
        }
    }
}
