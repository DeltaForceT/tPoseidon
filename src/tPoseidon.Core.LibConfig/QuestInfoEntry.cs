namespace tPoseidon.Core.LibConfig;

public sealed record QuestInfoEntry
{
        public required int Id { get; init; }

        public required string Name { get; init; }

        public string Client { get; init; } = string.Empty;

        public string Pos { get; init; } = string.Empty;

        public string Comment { get; init; } = string.Empty;

        public int MinLevel { get; init; }

        public int MaxLevel { get; init; }

        public int Exp { get; init; }

        public int Tmxp { get; init; }

        public int SrcItem0 { get; init; }
    public int SrcItem1 { get; init; }
    public int SrcItem2 { get; init; }
    public int SrcItem3 { get; init; }
    public int SrcItem4 { get; init; }
    public int SrcItem5 { get; init; }

        public int Cnt0 { get; init; }
    public int Cnt1 { get; init; }
    public int Cnt2 { get; init; }
    public int Cnt3 { get; init; }
    public int Cnt4 { get; init; }
    public int Cnt5 { get; init; }

        public int SrcSkill0 { get; init; }
    public int SrcSkillLv0 { get; init; }

        public int SrcGelder { get; init; }

        public string ResultTable { get; init; } = string.Empty;

        public int MaxCount { get; init; }

        public int RefId { get; init; }

        public int Options { get; init; }

    public string SourceTable { get; init; } = string.Empty;
}
