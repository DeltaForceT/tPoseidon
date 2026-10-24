namespace tPoseidon.Core.LibConfig;

public sealed record MonsterQuestResultEntry
{
    public static readonly IReadOnlyList<string> TablePrefixes = new[] { "MonsterQuestResult_", "MonsterQuestResuit_" };

    public required string TableName { get; init; }
    public int Grade { get; init; }
    public int Type { get; init; }

    public int Item0 { get; init; }
    public int Cnt0 { get; init; }
    public int Item1 { get; init; }
    public int Cnt1 { get; init; }
    public int Item2 { get; init; }
    public int Cnt2 { get; init; }
    public int Item3 { get; init; }
    public int Cnt3 { get; init; }
    public int Item4 { get; init; }
    public int Cnt4 { get; init; }

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

    public int Gelder { get; init; }

    public int RandomTableId0 { get; init; }
    public int RandomTableId1 { get; init; }
    public int RandomTableId2 { get; init; }

    public IEnumerable<(int ItemIndex, int Count)> RowItems()
    {
        if (Item0 != 0) yield return (Item0, Cnt0);
        if (Item1 != 0) yield return (Item1, Cnt1);
        if (Item2 != 0) yield return (Item2, Cnt2);
        if (Item3 != 0) yield return (Item3, Cnt3);
        if (Item4 != 0) yield return (Item4, Cnt4);
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
}
