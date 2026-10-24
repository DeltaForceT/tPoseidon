using tPoseidon.Core.Common;

namespace tPoseidon.Core.LibConfig;

public interface INpcPointProvider
{
        bool TryGet(ushort mapId, ushort posId, out NpcPoint point);
}
