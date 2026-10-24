namespace tPoseidon.Core.Data;

public sealed record QuestHistory
{
        public required ulong CharacterId { get; init; }

        public required byte QuestType { get; init; }

        public required int QuestId { get; init; }

        public required byte CompletionCount { get; init; }

        public int MonsterId { get; init; }

        public short KillCount { get; init; }

        public int RecordTime { get; init; }

        public DateTimeOffset CompletedAt { get; init; } = DateTimeOffset.UtcNow;
}
