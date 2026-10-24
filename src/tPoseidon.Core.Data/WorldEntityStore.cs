namespace tPoseidon.Core.Data;

public sealed class WorldEntityStore
{
    private readonly object _gate = new();
    private readonly Dictionary<ushort, NpcEntity> _byId = new();

        public void Install(NpcEntity npc)
    {
        ArgumentNullException.ThrowIfNull(npc);
        lock (_gate)
        {
            if (_byId.ContainsKey(npc.EntityId))
            {
                throw new InvalidOperationException(
                    $"Entity 0x{npc.EntityId:X4} already installed.");
            }
            _byId[npc.EntityId] = npc;
        }
    }

        public NpcEntity? Get(ushort entityId)
    {
        lock (_gate)
        {
            return _byId.TryGetValue(entityId, out var npc) ? npc : null;
        }
    }

        public IReadOnlyList<NpcEntity> InMap(ushort mapId)
    {
        lock (_gate)
        {
            return _byId.Values
                .Where(e => e.MapId == mapId)
                .OrderBy(e => e.EntityId)
                .ToArray();
        }
    }

        public NpcEntity? FindByTypeOnMap(int chrType, ushort mapId)
    {
        lock (_gate)
        {
            return _byId.Values
                .Where(e => e.MapId == mapId && e.ChrType == chrType)
                .OrderBy(e => e.EntityId)
                .FirstOrDefault();
        }
    }

        public bool Remove(ushort entityId)
    {
        lock (_gate)
        {
            return _byId.Remove(entityId);
        }
    }

        public int Count
    {
        get
        {
            lock (_gate) { return _byId.Count; }
        }
    }

        public IReadOnlyList<NpcEntity> All()
    {
        lock (_gate)
        {
            return _byId.Values.OrderBy(e => e.EntityId).ToArray();
        }
    }
}
