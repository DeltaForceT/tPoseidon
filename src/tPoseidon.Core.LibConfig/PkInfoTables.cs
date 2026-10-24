namespace tPoseidon.Core.LibConfig;

public sealed record PkInfoTables(int ButtonCoolTime, int SuperTime, int TodayPkMax, int TotalPkMax, int TotalDeathMax)
{
    public const string Table = "PKInfo";

    public static PkInfoTables Empty { get; } = new(0, 0, 0, 0, 0);
}
