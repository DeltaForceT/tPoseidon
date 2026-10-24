using System.IO.Compression;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.WorldSnapshot;

public static class ZoneGeometrySnapshot
{
    public static ZoneGeometryDto ToDto(ZoneGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        return new ZoneGeometryDto
        {
            MinX = geometry.MinX,
            MinY = geometry.MinY,
            MaxX = geometry.MaxX,
            MaxY = geometry.MaxY,
            CellsX = geometry.CellsX,
            CellsY = geometry.CellsY,
            CellAttributesGzip = Compress(geometry.CellAttributes),
            GroundTypesGzip = geometry.HasGroundTypes ? Compress(geometry.GroundTypes) : null,
            Regions = geometry.Regions.Select(r => new ZoneRegionDto
            {
                Id = r.Id,
                Type = r.Type,
                Param = r.Param,
                X1 = r.X1,
                Y1 = r.Y1,
                X2 = r.X2,
                Y2 = r.Y2,
            }).ToList(),
            Points = geometry.Points.Select(p => new ZonePointDto
            {
                Id = p.Id,
                Type = p.Type,
                Param = p.Param,
                X = p.X,
                Y = p.Y,
            }).ToList(),
        };
    }

    public static ZoneGeometry FromDto(ZoneGeometryDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var attributes = Decompress(dto.CellAttributesGzip);
        if (dto.CellsX <= 0 || dto.CellsY <= 0 || attributes.Length != dto.CellsX * dto.CellsY)
        {
            throw new InvalidDataException(
                $"Zone geometry holds {attributes.Length} cell attributes for a {dto.CellsX} x {dto.CellsY} grid.");
        }
        return new ZoneGeometry
        {
            MinX = dto.MinX,
            MinY = dto.MinY,
            MaxX = dto.MaxX,
            MaxY = dto.MaxY,
            CellsX = dto.CellsX,
            CellsY = dto.CellsY,
            CellAttributes = attributes,
            GroundTypes = dto.GroundTypesGzip is { } ground && Decompress(ground) is { } types && types.Length == attributes.Length
                ? types
                : Array.Empty<byte>(),
            Regions = dto.Regions
                .Select(r => new ZoneRegion(r.Id, r.Type, r.Param, r.X1, r.Y1, r.X2, r.Y2))
                .ToArray(),
            Points = (dto.Points ?? new List<ZonePointDto>())
                .Select(p => new ZonePoint(p.Id, p.Type, p.Param, p.X, p.Y))
                .ToArray(),
        };
    }

    public static Dictionary<ushort, ZoneGeometry> ReadAll(WorldSnapshotDto snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var byMap = new Dictionary<ushort, ZoneGeometry>();
        foreach (var zone in snapshot.Zones)
        {
            if (zone.Geometry is null) continue;
            byMap[zone.MapId] = FromDto(zone.Geometry);
        }
        return byMap;
    }

    private static byte[] Compress(byte[] raw)
    {
        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            gzip.Write(raw, 0, raw.Length);
        }
        return buffer.ToArray();
    }

    private static byte[] Decompress(byte[] compressed)
    {
        using var input = new MemoryStream(compressed, writable: false);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }
}
