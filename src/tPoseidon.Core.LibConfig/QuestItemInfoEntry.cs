namespace tPoseidon.Core.LibConfig;

public sealed record QuestItemInfoEntry
{
        public required int Id { get; init; }

        public required int QuestId { get; init; }

        public required int ItemId { get; init; }

        public int RefineLevel { get; init; }
}
