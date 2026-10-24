using tPoseidon.Core.Game.Items;

namespace tPoseidon.Core.Net.World;

public sealed class ZoneItemMgr
{
    private readonly Zone _zone;
    private readonly ItemManager _global;

    public ZoneItemMgr(Zone zone, ItemManager global)
    {
        _zone = zone ?? throw new ArgumentNullException(nameof(zone));
        _global = global ?? throw new ArgumentNullException(nameof(global));
    }

    public Zone Zone => _zone;
    public ItemManager Global => _global;

    public bool IsInitialized { get; private set; }

    public void Init()
    {
        IsInitialized = true;
    }
}
