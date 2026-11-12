namespace tPoseidon.Core.LibConfig;

public sealed record DropEventItemEntry(
    int ItemId,
    float DrillRatio,
    float MonRatio,
    int InYear, int InMonth, int InDay, int InHour, int InMinute,
    int OutYear, int OutMonth, int OutDay, int OutHour, int OutMinute,
    string WorkingDays)
{
    public const string TableName = "DropItemEventParam";
}
