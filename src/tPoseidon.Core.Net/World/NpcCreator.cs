namespace tPoseidon.Core.Net.World;

public sealed class NpcCreator : IZoneEntityCreator
{
    private readonly NpcManager _npcManager;
    private readonly Action<string>? _log;

    public NpcCreator(NpcManager npcManager, Action<string>? log = null)
    {
        _npcManager = npcManager ?? throw new ArgumentNullException(nameof(npcManager));
        _log = log;
    }

    public int Spawn(Zone zone)
    {
        var libName = ExtractLibConfigSuffix(zone.MapInfo.NpcTable);
        if (string.IsNullOrEmpty(libName)) return 0;

        try
        {
            var preloaded = _npcManager.PreloadMap(zone.MapId, libName);
            zone.NpcSpawnCount = preloaded.Count;
            return preloaded.Count;
        }
        catch (Exception ex)
        {
            _log?.Invoke($"NpcCreator: preload failed for map {zone.MapId} ({zone.Name}, libName={libName}): {ex.GetType().Name}: {ex.Message}");
            return 0;
        }
    }

    private static string ExtractLibConfigSuffix(string npcTable)
    {
        if (string.IsNullOrEmpty(npcTable)) return string.Empty;
        const string prefix = "NpcTalk_";
        if (npcTable.StartsWith(prefix, StringComparison.Ordinal))
        {
            return npcTable.Substring(prefix.Length);
        }
        return npcTable;
    }
}
