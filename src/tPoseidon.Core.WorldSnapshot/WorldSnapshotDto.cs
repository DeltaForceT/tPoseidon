namespace tPoseidon.Core.WorldSnapshot;

public sealed record WorldSnapshotDto
{
        public const int CurrentFormatVersion = 1;

    public int FormatVersion { get; init; } = CurrentFormatVersion;

        public required string Variant { get; init; }

        public required string BuildTimestampUtc { get; init; }

        public required Dictionary<string, string> SourceHashes { get; init; }

        public required List<string> BuildWarnings { get; init; }

    public required List<ZoneDto> Zones { get; init; }

    public required List<NpcEntityDto> Npcs { get; init; }

    public required List<MonsterEntityDto> Monsters { get; init; }

        public required ushort NextEntityIdAfterBoot { get; init; }
    public List<CharacterCombatInfoDto>? CombatInfos { get; init; }
}

public sealed record CharacterCombatInfoDto
{
    public required int Id { get; init; }
    public required int MinAttackRange { get; init; }
    public required int MaxAttackRange { get; init; }
    public required int ChaseRange { get; init; }
    public required float WalkSpeed { get; init; }
    public required float RunSpeed { get; init; }
    public required int AttackMs { get; init; }
    public required int Attack2Ms { get; init; }
    public required int AttackReadyMs { get; init; }
}

public sealed record ZoneDto
{
    public required ushort MapId { get; init; }
    public required string Name { get; init; }
    public required string Md3FileName { get; init; }
    public required int NpcSpawnCount { get; init; }
    public required int MonsterSpawnCount { get; init; }
    public required List<PortalDto> Portals { get; init; }
    public ZoneGeometryDto? Geometry { get; init; }
}

public sealed record ZoneGeometryDto
{
    public required int MinX { get; init; }
    public required int MinY { get; init; }
    public required int MaxX { get; init; }
    public required int MaxY { get; init; }
    public required int CellsX { get; init; }
    public required int CellsY { get; init; }
    public required byte[] CellAttributesGzip { get; init; }
    public byte[]? GroundTypesGzip { get; init; }
    public required List<ZoneRegionDto> Regions { get; init; }
    public List<ZonePointDto>? Points { get; init; }
}

public sealed record ZonePointDto
{
    public required int Id { get; init; }
    public required int Type { get; init; }
    public required int Param { get; init; }
    public required int X { get; init; }
    public required int Y { get; init; }
}

public sealed record ZoneRegionDto
{
    public required int Id { get; init; }
    public required int Type { get; init; }
    public required int Param { get; init; }
    public required int X1 { get; init; }
    public required int Y1 { get; init; }
    public required int X2 { get; init; }
    public required int Y2 { get; init; }
}

public sealed record PortalDto
{
    public required int Id { get; init; }
    public required int XMin { get; init; }
    public required int YMin { get; init; }
    public required int XMax { get; init; }
    public required int YMax { get; init; }
    public required ushort DestMap { get; init; }
}

public sealed record NpcEntityDto
{
    public required ushort EntityId { get; init; }
    public required ushort MapId { get; init; }
    public required int ChrType { get; init; }
    public required ushort PosId { get; init; }
    public required float SpawnX { get; init; }
    public required float SpawnY { get; init; }
}

public sealed record MonsterEntityDto
{
    public required ushort EntityId { get; init; }
    public required ushort MapId { get; init; }
    public required int MonsterParamId { get; init; }
    public required string Name { get; init; }
    public required ushort SpawnX { get; init; }
    public required ushort SpawnY { get; init; }
    public required int MaxHp { get; init; }
    public required int Level { get; init; }
    public required ushort SpawnPointId { get; init; }
    public required bool IsBoss { get; init; }
    public int RegionId { get; init; }
}
