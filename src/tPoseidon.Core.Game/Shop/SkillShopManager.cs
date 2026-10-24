using System.Collections.Concurrent;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Shop;

public sealed class SkillShopManager
{
    public const string SkillShopTablePrefix = "SkillShopTalk_";

    private readonly ISkillShopZoneLookup _catalog;
    private readonly INpcPointProvider _points;
    private readonly IdAllocator _ids;
    private readonly Action<string>? _log;

    private readonly ConcurrentDictionary<ushort, SkillShopEntity> _byEntityId = new();
    private readonly ConcurrentDictionary<(ushort MapId, int NpcType, int PosId), ushort> _entityIdByKey = new();

    public SkillShopManager(
        ISkillShopZoneLookup catalog,
        INpcPointProvider points,
        IdAllocator ids,
        Action<string>? log = null)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _points = points ?? throw new ArgumentNullException(nameof(points));
        _ids = ids ?? throw new ArgumentNullException(nameof(ids));
        _log = log;
    }

    public int Count => _byEntityId.Count;

    public SkillShopEntity? Get(ushort entityId) =>
        _byEntityId.TryGetValue(entityId, out var s) ? s : null;

    public IReadOnlyList<SkillShopEntity> GetByMap(ushort mapId) =>
        _byEntityId.Values.Where(s => s.MapId == mapId).ToArray();

    public SkillShopEntity? GetByKey(ushort mapId, int npcType, int posId)
    {
        if (_entityIdByKey.TryGetValue((mapId, npcType, posId), out var entityId)
            && _byEntityId.TryGetValue(entityId, out var entity))
        {
            return entity;
        }
        return null;
    }

    public IReadOnlyList<SkillShopEntity> PreloadSkillShopTable(ushort mapId, string skillShopTable)
    {
        ArgumentNullException.ThrowIfNull(skillShopTable);
        if (!skillShopTable.StartsWith(SkillShopTablePrefix, StringComparison.Ordinal) || skillShopTable.Length == SkillShopTablePrefix.Length)
        {
            return Array.Empty<SkillShopEntity>();
        }
        return PreloadMap(mapId, skillShopTable[SkillShopTablePrefix.Length..]);
    }

    public SkillShopEntity? CreateSkillShop(ushort mapId, string libconfigMapName, SkillShopZoneEntry row)
    {
        ArgumentException.ThrowIfNullOrEmpty(libconfigMapName);
        ArgumentNullException.ThrowIfNull(row);

        if (!_points.TryGet(mapId, (ushort)row.PosId, out var point))
        {
            _log?.Invoke(
                $"SkillShopManager: not found SkillShop Point, MapId={mapId}, MapName={libconfigMapName}, NpcType={row.NpcType}, PosId={row.PosId} — fallback to Origin");
            point = NpcPoint.Origin();
        }

        var entityId = _ids.NewId();
        var entity = new SkillShopEntity
        {
            EntityId = entityId,
            MapId = mapId,
            NpcType = row.NpcType,
            PosId = row.PosId,
            JobQuestId = row.JobQuestId,
            LimitJobType = row.LimitJobType,
            ChangeType = row.ChangeType,
            ChangeJob = row.ChangeJob,
            PurchaseRate = row.PurchaseRate,
            SellingRate = row.SellingRate,
            ItemTable = row.ItemTable,
            LimitSkillMaster = row.LimitSkillMaster,
            Pages = row.Pages,
            SpawnX = (ushort)point.X,
            SpawnY = (ushort)point.Y,
        };

        _byEntityId[entityId] = entity;
        _entityIdByKey[(mapId, row.NpcType, row.PosId)] = entityId;
        return entity;
    }

    public IReadOnlyList<SkillShopEntity> PreloadMap(ushort mapId, string libconfigMapName)
    {
        ArgumentException.ThrowIfNullOrEmpty(libconfigMapName);

        var rows = _catalog.GetByMap(libconfigMapName);
        var created = new List<SkillShopEntity>(rows.Count);
        foreach (var row in rows)
        {
            var shop = CreateSkillShop(mapId, libconfigMapName, row);
            if (shop is not null) created.Add(shop);
        }
        return created;
    }

    public bool Remove(ushort entityId)
    {
        if (!_byEntityId.TryRemove(entityId, out var entity)) return false;
        _entityIdByKey.TryRemove((entity.MapId, entity.NpcType, entity.PosId), out _);
        _ids.Release(entityId);
        return true;
    }
}
