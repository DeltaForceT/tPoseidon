using tPoseidon.Core.Common;

namespace tPoseidon.Core.LibConfig;

public static class MapPointTypes
{
    public const int Npc = 4;
    public const int Shop = 5;
    public const int SkillShop = 10;
}

public static class Md3PointCatalog
{
    public static InMemoryNpcPointProvider Load(IEnumerable<MapInfoEntry> maps, string mapDirectory, int pointType)
    {
        ArgumentNullException.ThrowIfNull(maps);
        ArgumentException.ThrowIfNullOrEmpty(mapDirectory);

        var points = new Dictionary<(ushort MapId, ushort PosId), NpcPoint>();
        foreach (var map in maps)
        {
            var fileName = map.Md3FileName.Trim();
            if (fileName.Length == 0) continue;
            if (Md3MapReader.ReadMap(tPoseidon.Core.Common.ClientFiles.Combine(mapDirectory, fileName)) is not { } md3) continue;
            foreach (var point in md3.Points)
            {
                if (point.Type != pointType || point.Id is < 0 or > ushort.MaxValue) continue;
                points.TryAdd((map.Id, (ushort)point.Id), NpcPoint.Xy(point.X, point.Y));
            }
        }
        return new InMemoryNpcPointProvider(points.Select(entry => (entry.Key, entry.Value)));
    }
}
