using tPoseidon.Core.Common.Indexing;

namespace tPoseidon.Core.Data;

public sealed class WorldEntityStore
{
    private readonly MapBucketIndex<NpcEntity> _entities = new(npc => npc.EntityId, npc => npc.MapId, npc => npc.ChrType);

    public void Install(NpcEntity npc)
    {
        ArgumentNullException.ThrowIfNull(npc);
        _entities.Install(npc);
    }

    public NpcEntity? Get(ushort entityId) => _entities.Get(entityId);

    public IReadOnlyList<NpcEntity> InMap(ushort mapId) => _entities.InMap(mapId);

    public NpcEntity? FindByTypeOnMap(int chrType, ushort mapId) => _entities.FindByTypeOnMap(chrType, mapId);

    public bool Remove(ushort entityId) => _entities.Remove(entityId);

    public int Count => _entities.Count;

    public IReadOnlyList<NpcEntity> All() => _entities.All();
}
