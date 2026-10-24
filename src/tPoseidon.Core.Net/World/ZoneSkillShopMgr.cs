using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Shop;

namespace tPoseidon.Core.Net.World;

public sealed class ZoneSkillShopMgr
{
    private readonly Zone _zone;
    private readonly SkillShopManager? _skillShopManager;
    private readonly List<SkillShopEntity> _shops = new();

    public ZoneSkillShopMgr(Zone zone, SkillShopManager? skillShopManager = null)
    {
        _zone = zone ?? throw new ArgumentNullException(nameof(zone));
        _skillShopManager = skillShopManager;
    }

    public bool IsInitialized { get; private set; }
    public bool IsCreated { get; private set; }

    public void Init()
    {
        IsInitialized = true;
    }

    public void CreateAll()
    {
        if (_skillShopManager is not null && !string.IsNullOrEmpty(_zone.Name))
        {
            foreach (var shop in _skillShopManager.PreloadMap(_zone.MapId, _zone.Name))
            {
                _shops.Add(shop);
            }
        }
        IsCreated = true;
    }
}
