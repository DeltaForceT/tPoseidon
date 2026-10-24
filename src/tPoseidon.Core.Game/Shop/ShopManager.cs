using System.Collections.Concurrent;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Shop;

public sealed class ShopManager
{
    public const string ShopTablePrefix = "Shop_";

    private readonly IShopZoneLookup _shopCatalog;
    private readonly INpcPointProvider _points;
    private readonly IdAllocator _ids;
    private readonly Action<string>? _log;

    private readonly ConcurrentDictionary<ushort, ShopEntity> _byEntityId = new();
    private readonly ConcurrentDictionary<(ushort MapId, int NpcType, int PosId), ushort> _entityIdByKey = new();

    public ShopManager(
        IShopZoneLookup shopCatalog,
        INpcPointProvider points,
        IdAllocator ids,
        Action<string>? log = null)
    {
        _shopCatalog = shopCatalog ?? throw new ArgumentNullException(nameof(shopCatalog));
        _points = points ?? throw new ArgumentNullException(nameof(points));
        _ids = ids ?? throw new ArgumentNullException(nameof(ids));
        _log = log;
    }

    public int Count => _byEntityId.Count;

    public ShopEntity? Get(ushort entityId) =>
        _byEntityId.TryGetValue(entityId, out var s) ? s : null;

    public IReadOnlyList<ShopEntity> GetByMap(ushort mapId) =>
        _byEntityId.Values.Where(s => s.MapId == mapId).ToArray();

    public ShopEntity? GetByKey(ushort mapId, int npcType, int posId)
    {
        if (_entityIdByKey.TryGetValue((mapId, npcType, posId), out var entityId)
            && _byEntityId.TryGetValue(entityId, out var entity))
        {
            return entity;
        }
        return null;
    }

    public ShopEntity? CreateShop(ushort mapId, string libconfigMapName, ShopZoneEntry row)
    {
        ArgumentException.ThrowIfNullOrEmpty(libconfigMapName);
        ArgumentNullException.ThrowIfNull(row);

        if (!_points.TryGet(mapId, (ushort)row.PosId, out var point))
        {
            _log?.Invoke(
                $"ShopManager: not found Shop Point, MapId={mapId}, MapName={libconfigMapName}, NpcType={row.NpcType}, PosId={row.PosId} — fallback to Origin");
            point = NpcPoint.Origin();
        }

        var entityId = _ids.NewId();
        var entity = new ShopEntity
        {
            EntityId = entityId,
            MapId = mapId,
            NpcType = row.NpcType,
            PosId = row.PosId,
            PurchaseRate = row.PurchaseRate,
            SellingRate = row.SellingRate,
            ItemTable = row.ItemTable,
            Script = row.Script,
            SpawnX = (ushort)point.X,
            SpawnY = (ushort)point.Y,
        };

        _byEntityId[entityId] = entity;
        _entityIdByKey[(mapId, row.NpcType, row.PosId)] = entityId;
        return entity;
    }

    public IReadOnlyList<ShopEntity> PreloadMap(ushort mapId, string libconfigMapName)
    {
        ArgumentException.ThrowIfNullOrEmpty(libconfigMapName);

        var rows = _shopCatalog.GetByMap(libconfigMapName);
        var created = new List<ShopEntity>(rows.Count);
        foreach (var row in rows)
        {
            var shop = CreateShop(mapId, libconfigMapName, row);
            if (shop is not null) created.Add(shop);
        }
        return created;
    }

    public IReadOnlyList<ShopEntity> PreloadShopTable(ushort mapId, string shopTable)
    {
        ArgumentNullException.ThrowIfNull(shopTable);
        if (!shopTable.StartsWith(ShopTablePrefix, StringComparison.Ordinal) || shopTable.Length == ShopTablePrefix.Length)
        {
            return Array.Empty<ShopEntity>();
        }
        return PreloadMap(mapId, shopTable[ShopTablePrefix.Length..]);
    }

    public bool Remove(ushort entityId)
    {
        if (!_byEntityId.TryRemove(entityId, out var entity)) return false;
        _entityIdByKey.TryRemove((entity.MapId, entity.NpcType, entity.PosId), out _);
        _ids.Release(entityId);
        return true;
    }
}
