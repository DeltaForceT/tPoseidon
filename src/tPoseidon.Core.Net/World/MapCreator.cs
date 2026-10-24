using System.IO;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Net.World;

public sealed class MapCreator : IZoneCreator
{
    private readonly IPortalLookup? _portals;
    private readonly Func<string>? _md3DirectoryProvider;

    public MapCreator(IPortalLookup? portals, Func<string>? md3DirectoryProvider = null)
    {
        _portals = portals;
        _md3DirectoryProvider = md3DirectoryProvider;
    }

    public void Build(Zone zone)
    {
        var fromMd3 = LoadPortalsFromMd3(zone);
        var fromOverride = _portals?.GetEntrances(zone.MapId) ?? Array.Empty<PortalEntrance>();

        if (fromMd3.Count == 0 && fromOverride.Count == 0)
        {
            zone.Portals = Array.Empty<PortalEntrance>();
            return;
        }
        if (fromMd3.Count == 0)
        {
            zone.Portals = fromOverride;
            return;
        }
        if (fromOverride.Count == 0)
        {
            zone.Portals = fromMd3;
            return;
        }

        var byKey = new Dictionary<(ushort DestMap, int XMin, int YMin), PortalEntrance>(fromMd3.Count + fromOverride.Count);
        foreach (var p in fromMd3)
        {
            byKey[(p.DestMap, p.XMin, p.YMin)] = p;
        }
        foreach (var p in fromOverride)
        {
            byKey[(p.DestMap, p.XMin, p.YMin)] = p;
        }
        zone.Portals = byKey.Values.ToArray();
    }

    private IReadOnlyList<PortalEntrance> LoadPortalsFromMd3(Zone zone)
    {
        if (_md3DirectoryProvider is null) return Array.Empty<PortalEntrance>();
        if (string.IsNullOrEmpty(zone.Md3FileName)) return Array.Empty<PortalEntrance>();

        var dir = _md3DirectoryProvider();
        if (string.IsNullOrEmpty(dir)) return Array.Empty<PortalEntrance>();

        var fname = zone.Md3FileName;
        if (!fname.EndsWith(".md3", StringComparison.OrdinalIgnoreCase))
        {
            fname += ".md3";
        }
        var map = Md3MapReader.ReadMap(tPoseidon.Core.Common.ClientFiles.Combine(dir, fname));
        if (map is null) return Array.Empty<PortalEntrance>();

        var entrances = new List<PortalEntrance>();
        foreach (var range in map.Ranges)
        {
            if (range.Type != 0) continue;
            if (range.Destination <= 0 || range.Destination > ushort.MaxValue) continue;
            entrances.Add(new PortalEntrance
            {
                Id = range.Id,
                XMin = range.X1,
                YMin = range.Y1,
                XMax = range.X2,
                YMax = range.Y2,
                DestMap = (ushort)range.Destination,
            });
        }
        return entrances;
    }
}
