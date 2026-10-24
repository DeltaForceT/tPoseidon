namespace tPoseidon.Core.LibConfig;

public sealed record NpcMsgEntry
{
        public required string TableName { get; init; }

        public required int No { get; init; }

        public byte NpcSType { get; init; }

        public string Message { get; init; } = string.Empty;
}
