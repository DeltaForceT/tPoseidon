using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.WorldSnapshot;

public static class WorldSnapshotBuilder
{
        public static WorldSnapshotDto Build(
        string variant,
        IReadOnlyList<ZoneSummary> zones,
        IReadOnlyList<NpcEntity> npcs,
        IReadOnlyList<MonsterEntity> monsters,
        ushort nextEntityIdAfterBoot,
        IReadOnlyDictionary<string, string> sourceHashes,
        IReadOnlyList<string> buildWarnings,
        IReadOnlyDictionary<ushort, ZoneGeometry>? zoneGeometry = null,
        IReadOnlyList<CharacterCombatInfo>? combatInfos = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(variant);
        ArgumentNullException.ThrowIfNull(zones);
        ArgumentNullException.ThrowIfNull(npcs);
        ArgumentNullException.ThrowIfNull(monsters);
        ArgumentNullException.ThrowIfNull(sourceHashes);
        ArgumentNullException.ThrowIfNull(buildWarnings);

        return new WorldSnapshotDto
        {
            Variant = variant,
            BuildTimestampUtc = DateTime.UtcNow.ToString("O"),
            SourceHashes = new Dictionary<string, string>(sourceHashes),
            BuildWarnings = new List<string>(buildWarnings),
            Zones = zones.Select(z => new ZoneDto
            {
                MapId = z.MapId,
                Name = z.Name,
                Md3FileName = z.Md3FileName,
                NpcSpawnCount = z.NpcSpawnCount,
                MonsterSpawnCount = z.MonsterSpawnCount,
                Portals = z.Portals.Select(p => new PortalDto
                {
                    Id = p.Id,
                    XMin = p.XMin,
                    YMin = p.YMin,
                    XMax = p.XMax,
                    YMax = p.YMax,
                    DestMap = p.DestMap,
                }).ToList(),
                Geometry = zoneGeometry is not null && zoneGeometry.TryGetValue(z.MapId, out var geometry)
                    ? ZoneGeometrySnapshot.ToDto(geometry)
                    : null,
            }).ToList(),
            Npcs = npcs.Select(n => new NpcEntityDto
            {
                EntityId = n.EntityId,
                MapId = n.MapId,
                ChrType = n.ChrType,
                PosId = n.PosId,
                SpawnX = n.Spawn.X,
                SpawnY = n.Spawn.Y,
            }).ToList(),
            Monsters = monsters.Select(m => new MonsterEntityDto
            {
                EntityId = m.EntityId,
                MapId = m.MapId,
                MonsterParamId = m.MonsterParamId,
                Name = m.Name,
                SpawnX = m.SpawnX,
                SpawnY = m.SpawnY,
                MaxHp = m.MaxHp,
                Level = m.Level,
                SpawnPointId = m.SpawnPointId,
                IsBoss = m.IsBoss,
                RegionId = m.RegionId,
            }).ToList(),
            NextEntityIdAfterBoot = nextEntityIdAfterBoot,
            CombatInfos = combatInfos?.Select(CharacterCombatSnapshot.ToDto).ToList(),
        };
    }
}

public sealed record ZoneSummary
{
    public required ushort MapId { get; init; }
    public required string Name { get; init; }
    public required string Md3FileName { get; init; }
    public required int NpcSpawnCount { get; init; }
    public required int MonsterSpawnCount { get; init; }
    public required IReadOnlyList<PortalEntrance> Portals { get; init; }
}
