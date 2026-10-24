using tPoseidon.Core.Common;

namespace tPoseidon.Core.Data;

public sealed record NpcEntity
{
        public required ushort EntityId { get; init; }

        public required ushort MapId { get; init; }

        public required int ChrType { get; init; }

        public required ushort PosId { get; init; }

        public required NpcPoint Spawn { get; init; }

        public int HeadMarkType { get; init; }

        public string? DisplayName { get; init; }

        public object? Source { get; init; }
}
