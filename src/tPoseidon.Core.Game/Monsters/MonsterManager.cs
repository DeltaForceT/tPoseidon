using System.Collections.Concurrent;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Monsters;

public sealed class MonsterManager : IMonsterEntityProvider
{
    private readonly IMonsterParamLookup _catalog;
    private readonly IMonItemDropInfoLookup? _dropCatalog;
    private readonly MonsterOverridesProvider? _monsterOverrides;
    private readonly IdAllocator _idAllocator;
    private readonly Func<long>? _damageLimit;
    private readonly ConcurrentDictionary<ushort, MonsterEntity> _byEntityId = new();
    private readonly ConcurrentDictionary<ushort, IMonsterStrategy> _strategyByEntityId = new();
    private readonly ConcurrentDictionary<ushort, GroundItem> _groundItems = new();
    private readonly Random _rng;

    public MonsterManager(
        IMonsterParamLookup catalog,
        IdAllocator idAllocator,
        IMonItemDropInfoLookup? dropCatalog = null,
        MonsterOverridesProvider? monsterOverrides = null,
        int? rngSeed = null,
        Func<long>? damageLimit = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(idAllocator);
        _catalog = catalog;
        _dropCatalog = dropCatalog;
        _monsterOverrides = monsterOverrides;
        _idAllocator = idAllocator;
        _damageLimit = damageLimit;
        _rng = rngSeed is int s ? new Random(s) : new Random();
    }

    public event Action<MonsterEntity>? MonsterSpawned;
    public event Action<MonsterEntity, ushort, long>? MonsterDamaged;
    public event Action<MonsterEntity, ushort>? MonsterKilled;

    public int Count => _byEntityId.Count;

    public IReadOnlyCollection<MonsterEntity> All => _byEntityId.Values.ToArray();

    public MonsterParamEntry? Param(int monsterParamId) => _catalog.GetById(monsterParamId);

    public MonsterEntity? Get(ushort entityId) =>
        _byEntityId.TryGetValue(entityId, out var m) ? m : null;

    public IReadOnlyCollection<MonsterEntity> GetByMap(ushort mapId) =>
        _byEntityId.Values.Where(m => m.MapId == mapId && !m.IsDead).ToArray();

    public MonsterEntity? Spawn(int monsterParamId, ushort mapId, ushort x, ushort y, ushort? spawnPointId = null)
        => Spawn(monsterParamId, mapId, x, y, spawnPointId, explicitEntityId: null);

    public MonsterEntity? Spawn(int monsterParamId, ushort mapId, ushort x, ushort y, ushort? spawnPointId, ushort? explicitEntityId)
        => Spawn(monsterParamId, mapId, x, y, spawnPointId, explicitEntityId, regionId: 0);

    public MonsterEntity? Spawn(int monsterParamId, ushort mapId, ushort x, ushort y, ushort? spawnPointId, ushort? explicitEntityId, int regionId)
    {
        var def = _catalog.GetById(monsterParamId);
        if (def is null) return null;

        var ovr = _monsterOverrides?.Current.Get(monsterParamId);
        var name = ovr?.Name ?? def.Name;
        var level = ovr?.Level ?? def.Level;
        var isBoss = ovr?.IsBoss ?? def.IsBoss;
        var maxHp = ResolveMaxHpWithOverride(def, ovr);

        var entityId = explicitEntityId ?? _idAllocator.NewId();
        var atype = MonsterStrategyFactory.FromByte((byte)def.Type);
        var entity = new MonsterEntity
        {
            EntityId = entityId,
            MapId = mapId,
            MonsterParamId = monsterParamId,
            Name = name,
            SpawnX = x,
            SpawnY = y,
            MaxHp = maxHp,
            Level = level,
            SpawnPointId = spawnPointId ?? 0,
            RegionId = regionId,
            IsBoss = isBoss,
            Atype = atype,
            AiViewRange = def.AiViewRange,
            DamageLimit = _damageLimit,
        };
        entity.InitHp(maxHp);
        entity.InitPosition(x, y);
        entity.InitSpeed(def.WalkSpeed);

        var strategy = MonsterStrategyFactory.Create(atype);
        strategy.OnSpawn(entity);

        _byEntityId[entityId] = entity;
        _strategyByEntityId[entityId] = strategy;
        MonsterSpawned?.Invoke(entity);
        return entity;
    }

    public IMonsterStrategy? GetStrategy(ushort entityId) =>
        _strategyByEntityId.TryGetValue(entityId, out var s) ? s : null;

    private static long ResolveMaxHpWithOverride(MonsterParamEntry def, MonsterOverrideEntry? ovr)
    {
        if (ovr?.MaxHpLv is null) return ResolveMaxHp(def);

        return Math.Max(1L, ovr?.MaxHpLv ?? def.MaxHpLv);
    }

    public DamageResult ApplyDamage(ushort entityId, ushort attackerEntityId, long damage)
    {
        if (!_byEntityId.TryGetValue(entityId, out var mob))
            return DamageResult.NotFound;
        if (mob.IsDead)
            return DamageResult.AlreadyDead;

        var actual = mob.ApplyDamage(damage);
        if (actual <= 0)
            return DamageResult.AlreadyDead;

        if (_strategyByEntityId.TryGetValue(entityId, out var strategy))
        {
            strategy.OnDamaged(mob, attackerEntityId, actual);
        }

        MonsterDamaged?.Invoke(mob, attackerEntityId, actual);

        if (mob.IsDead)
        {
            mob.ClearAttackers();
            if (strategy is not null) strategy.OnDeath(mob, attackerEntityId);
            MonsterKilled?.Invoke(mob, attackerEntityId);
            return DamageResult.Killed;
        }
        return DamageResult.Damaged;
    }

    public void ReportKilled(MonsterEntity mob, ushort killerEntityId)
    {
        ArgumentNullException.ThrowIfNull(mob);
        if (_strategyByEntityId.TryGetValue(mob.EntityId, out var strategy)) strategy.OnDeath(mob, killerEntityId);
        MonsterKilled?.Invoke(mob, killerEntityId);
    }

    public bool Remove(ushort entityId)
    {
        if (!_byEntityId.TryRemove(entityId, out _)) return false;
        _strategyByEntityId.TryRemove(entityId, out _);
        _idAllocator.Release(entityId);
        return true;
    }

    private static long ResolveMaxHp(MonsterParamEntry def)
    {
        return Math.Max(1L, def.MaxHpLv);
    }

    public static int ResolveMaxMp(MonsterParamEntry def)
    {
        return Math.Max(0, def.MaxMpLv);
    }

    private const int MaxDropCascadeDepth = 4;

    private void RollDropTableRecursive(MonsterEntity mob, int dropTableId, List<GroundItem> dropped, HashSet<int> visited, int depth)
    {
        if (depth >= MaxDropCascadeDepth) return;
        if (!visited.Add(dropTableId)) return;
        if (_dropCatalog is null) return;

        var dropTable = _dropCatalog.GetById(dropTableId);
        if (dropTable is null) return;

        foreach (var item in dropTable.Items)
        {
            if (_rng.NextDouble() > item.Rate) continue;
            var entityId = _idAllocator.NewId();
            var ground = new GroundItem
            {
                EntityId = entityId,
                MapId = mob.MapId,
                X = mob.DestinationX,
                Y = mob.DestinationY,
                ItemIndex = item.ItemId,
                Count = item.Count,
                DroppedByMobEntityId = mob.EntityId,
            };
            _groundItems[entityId] = ground;
            dropped.Add(ground);
        }

        foreach (var sub in dropTable.SubTables)
        {
            if (_rng.NextDouble() > sub.Rate) continue;
            RollDropTableRecursive(mob, sub.TableId, dropped, visited, depth + 1);
        }
    }
}

public enum DamageResult
{
    Damaged,
    Killed,
    NotFound,
    AlreadyDead,
}
