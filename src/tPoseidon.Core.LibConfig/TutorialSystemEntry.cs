namespace tPoseidon.Core.LibConfig;

public sealed record TutorialSystemEntry
{
        public required int Id { get; init; }

        public int OpenType { get; init; }

        public int QuestId { get; init; }

        public int StartNpcID { get; init; }

        public int EndNpcID { get; init; }

        public int StartMapID { get; init; }

        public int MonsterMapID { get; init; }

        public int EndMapID { get; init; }

        public string DlgTable { get; init; } = string.Empty;

        public int DlgIndex { get; init; }

        public int UIOpen { get; init; }

        public int UIOpenType { get; init; }
}
