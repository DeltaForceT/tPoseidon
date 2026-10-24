namespace tPoseidon.Core.Data;

public sealed record ReportedCharacter(uint AccountId, ulong CharacterId, string Name);

public sealed record PlayerReport(
    byte Island,
    ulong PlaintiffCharUid,
    ulong DefendantCharUid,
    string Title,
    string Description,
    byte NotifyKind,
    IReadOnlyList<string> Chatting);

public static class ReportRules
{
    public const int Slots = 5;
    public const int TitleLength = 50;
    public const int DescriptionLength = 200;
    public const string StealWord = "steel";
}

public interface IReportStore
{
    ReportedCharacter? FindCharacter(string name);

    int Insert(PlayerReport report);
}
