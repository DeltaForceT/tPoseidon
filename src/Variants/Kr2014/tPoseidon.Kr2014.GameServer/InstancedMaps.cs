internal static class InstancedMaps
{
    private const ushort FirstSoloRoomKey = 0xFFFE;

    private static IReadOnlySet<ushort> _maps = new HashSet<ushort>();
    private static IReadOnlySet<ushort> _partyMaps = new HashSet<ushort>();
    private static int _nextSoloRoomKey = FirstSoloRoomKey;

    public static void Register(IEnumerable<ushort> maps, IEnumerable<ushort>? partyMaps = null)
    {
        ArgumentNullException.ThrowIfNull(maps);
        var party = (partyMaps ?? Array.Empty<ushort>()).ToHashSet();
        Volatile.Write(ref _partyMaps, party);
        Volatile.Write(ref _maps, maps.Concat(party).ToHashSet());
    }

    public static bool Contains(ushort mapId) => Volatile.Read(ref _maps).Contains(mapId);

    public static bool IsPartyRoom(ushort mapId) => Volatile.Read(ref _partyMaps).Contains(mapId);

    public static ushort PartyRoomKey(ushort partyId)
    {
        if (partyId != 0) return partyId;
        var key = Interlocked.Decrement(ref _nextSoloRoomKey) + 1;
        if (key <= ushort.MaxValue / 2) Interlocked.CompareExchange(ref _nextSoloRoomKey, FirstSoloRoomKey, key - 1);
        return (ushort)key;
    }
}
