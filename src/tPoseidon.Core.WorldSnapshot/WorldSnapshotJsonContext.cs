using System.Text.Json.Serialization;

namespace tPoseidon.Core.WorldSnapshot;

[JsonSerializable(typeof(WorldSnapshotDto))]
[JsonSerializable(typeof(ZoneDto))]
[JsonSerializable(typeof(PortalDto))]
[JsonSerializable(typeof(ZoneGeometryDto))]
[JsonSerializable(typeof(ZoneRegionDto))]
[JsonSerializable(typeof(CharacterCombatInfoDto))]
[JsonSerializable(typeof(NpcEntityDto))]
[JsonSerializable(typeof(MonsterEntityDto))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance", "CA1852",
    Justification = "Source-generated JsonSerializerContext partials must be unsealed.")]
internal partial class WorldSnapshotJsonContext : JsonSerializerContext;
