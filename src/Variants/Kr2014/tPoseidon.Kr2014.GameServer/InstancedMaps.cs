internal static class InstancedMaps
{
    private static IReadOnlySet<ushort> _maps = new HashSet<ushort>();

    public static void Register(IEnumerable<ushort> maps)
    {
        ArgumentNullException.ThrowIfNull(maps);
        Volatile.Write(ref _maps, maps.ToHashSet());
    }

    public static bool Contains(ushort mapId) => Volatile.Read(ref _maps).Contains(mapId);
}
