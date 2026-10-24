namespace tPoseidon.Core.Data;

public enum GmRateEventKind : short
{
    Exp = 0,
    Ability = 1,
}

public enum GmRateEventList : short
{
    Waiting = 0,
    Running = 1,
}

public sealed record GmRateEventRow(
    GmRateEventKind Kind,
    GmRateEventList List,
    int Position,
    byte Stat,
    ushort FirstPercent,
    ushort SecondPercent,
    long StartTime,
    long EndTime);

public sealed record GmRateEventState(IReadOnlyList<GmRateEventRow> Rows, ushort DirectExpPercent, ushort DirectTmPercent)
{
    public static GmRateEventState Empty { get; } = new(Array.Empty<GmRateEventRow>(), 0, 0);
}

public interface IGmRateEventStore
{
    GmRateEventState Load();

    void Save(GmRateEventState state);
}
