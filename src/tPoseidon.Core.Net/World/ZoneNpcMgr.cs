using tPoseidon.Core.Data;

namespace tPoseidon.Core.Net.World;

public sealed class ZoneNpcMgr
{
    private readonly Zone _zone;
    private readonly NpcManager _global;

    public ZoneNpcMgr(Zone zone, NpcManager global)
    {
        _zone = zone ?? throw new ArgumentNullException(nameof(zone));
        _global = global ?? throw new ArgumentNullException(nameof(global));
    }

    public Zone Zone => _zone;
    public NpcManager Global => _global;

    public int CreatedCount { get; private set; }

    public bool IsInitialized { get; private set; }

    public void Init()
    {
        IsInitialized = true;
    }

    public IReadOnlyList<NpcEntity> CreateAllNpcs(string libconfigMapName)
    {
        ArgumentException.ThrowIfNullOrEmpty(libconfigMapName);
        var created = _global.PreloadMap(_zone.MapId, libconfigMapName);
        CreatedCount = created.Count;
        _zone.NpcSpawnCount = CreatedCount;
        return created;
    }
}
