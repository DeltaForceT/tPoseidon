using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Shop;

namespace tPoseidon.Core.Net.World;

public sealed class ZoneShopMgr
{
    private readonly Zone _zone;
    private readonly ShopManager? _shopManager;
    private readonly List<ShopEntity> _shops = new();

    public ZoneShopMgr(Zone zone, ShopManager? shopManager = null)
    {
        _zone = zone ?? throw new ArgumentNullException(nameof(zone));
        _shopManager = shopManager;
    }

    public Zone Zone => _zone;

    public IReadOnlyList<ShopEntity> Shops => _shops;
    public int ShopCount => _shops.Count;

    public bool IsInitialized { get; private set; }
    public bool IsCreated { get; private set; }

    public void Init()
    {
        IsInitialized = true;
    }

    public void CreateAll()
    {
        if (_shopManager is not null && !string.IsNullOrEmpty(_zone.Name))
        {
            foreach (var shop in _shopManager.PreloadMap(_zone.MapId, _zone.Name))
            {
                _shops.Add(shop);
            }
        }
        IsCreated = true;
    }

    public void AddShop(ShopEntity shop)
    {
        ArgumentNullException.ThrowIfNull(shop);
        _shops.Add(shop);
    }

    public ShopEntity? GetByEntityId(ushort entityId)
        => _shops.FirstOrDefault(s => s.EntityId == entityId);
}
