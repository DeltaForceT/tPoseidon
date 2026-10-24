namespace tPoseidon.Core.LibConfig;

public sealed record MonsterQuestInfoEntry
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public string Client { get; init; } = string.Empty;
    public string Pos { get; init; } = string.Empty;
    public string Comment { get; init; } = string.Empty;

    public int MinLevel { get; init; }
    public int MaxLevel { get; init; }

    public int SrcMonster0 { get; init; }
    public int Cnt0 { get; init; }
    public int Time0 { get; init; }
    public int MinLv0 { get; init; }
    public int MaxLv0 { get; init; }
    public int Exp0 { get; init; }
    public int Tmxp0 { get; init; }
    public int MapId0 { get; init; }

    public int SrcMonster1 { get; init; }
    public int Cnt1 { get; init; }
    public int Time1 { get; init; }
    public int MinLv1 { get; init; }
    public int MaxLv1 { get; init; }
    public int Exp1 { get; init; }
    public int Tmxp1 { get; init; }
    public int MapId1 { get; init; }

    public int SrcMonster2 { get; init; }
    public int Cnt2 { get; init; }
    public int Time2 { get; init; }
    public int MinLv2 { get; init; }
    public int MaxLv2 { get; init; }
    public int Exp2 { get; init; }
    public int Tmxp2 { get; init; }
    public int MapId2 { get; init; }

    public int SrcMonster3 { get; init; }
    public int Cnt3 { get; init; }
    public int Time3 { get; init; }
    public int MinLv3 { get; init; }
    public int MaxLv3 { get; init; }
    public int Exp3 { get; init; }
    public int Tmxp3 { get; init; }
    public int MapId3 { get; init; }

    public int SrcMonster4 { get; init; }
    public int Cnt4 { get; init; }
    public int Time4 { get; init; }
    public int MinLv4 { get; init; }
    public int MaxLv4 { get; init; }
    public int Exp4 { get; init; }
    public int Tmxp4 { get; init; }
    public int MapId4 { get; init; }

        public string ResultTable { get; init; } = string.Empty;

        public int MaxCount { get; init; }

        public int ReturnMapID { get; init; }
}
