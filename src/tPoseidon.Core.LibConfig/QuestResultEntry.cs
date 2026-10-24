namespace tPoseidon.Core.LibConfig;

public sealed record QuestResultEntry
{
    public required int QuestId { get; init; }

        public required int Count { get; init; }

    public int Type { get; init; }

    public int Item0 { get; init; }
    public int Cnt0 { get; init; }
    public int Item1 { get; init; }
    public int Cnt1 { get; init; }
    public int Item2 { get; init; }
    public int Cnt2 { get; init; }
    public int Item3 { get; init; }
    public int Cnt3 { get; init; }

    public int Gelder { get; init; }
    public int Skill { get; init; }
    public int SkillLv { get; init; }

    public int Exp { get; init; }
    public int Tmxp { get; init; }

        public string Msg0 { get; init; } = string.Empty;
    public string Msg1 { get; init; } = string.Empty;
    public string Msg2 { get; init; } = string.Empty;
    public string Msg3 { get; init; } = string.Empty;
    public string Msg4 { get; init; } = string.Empty;
    public string Msg5 { get; init; } = string.Empty;
    public string Msg6 { get; init; } = string.Empty;
    public string Msg7 { get; init; } = string.Empty;
    public string Msg8 { get; init; } = string.Empty;
    public string Msg9 { get; init; } = string.Empty;

    public string TableName { get; init; } = string.Empty;

    public int RandomTableId0 { get; init; }
    public int RandomTableId1 { get; init; }
    public int RandomTableId2 { get; init; }

    public IEnumerable<(int ItemIndex, int Count)> RowItems()
    {
        if (Item0 != 0) yield return (Item0, Cnt0);
        if (Item1 != 0) yield return (Item1, Cnt1);
        if (Item2 != 0) yield return (Item2, Cnt2);
        if (Item3 != 0) yield return (Item3, Cnt3);
    }

    public IEnumerable<int> RandomTables()
    {
        if (RandomTableId0 != 0) yield return RandomTableId0;
        if (RandomTableId1 != 0) yield return RandomTableId1;
        if (RandomTableId2 != 0) yield return RandomTableId2;
    }

    public string? Message(int index) => index switch
    {
        0 => Msg0,
        1 => Msg1,
        2 => Msg2,
        3 => Msg3,
        4 => Msg4,
        5 => Msg5,
        6 => Msg6,
        7 => Msg7,
        8 => Msg8,
        9 => Msg9,
        _ => null,
    };

    public IEnumerable<(int ItemIndex, int Count)> NonEmptyItems()
    {
        if (Item0 > 0 && Cnt0 > 0) yield return (Item0, Cnt0);
        if (Item1 > 0 && Cnt1 > 0) yield return (Item1, Cnt1);
        if (Item2 > 0 && Cnt2 > 0) yield return (Item2, Cnt2);
        if (Item3 > 0 && Cnt3 > 0) yield return (Item3, Cnt3);
    }

        public string? MessageForGrade(int grade)
    {
        var raw = grade switch
        {
            0 => Msg0,
            1 => Msg1,
            2 => Msg2,
            3 => Msg3,
            4 => Msg4,
            5 => Msg5,
            6 => Msg6,
            7 => Msg7,
            8 => Msg8,
            9 => Msg9,
            _ => null,
        };
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return raw;
    }
}
