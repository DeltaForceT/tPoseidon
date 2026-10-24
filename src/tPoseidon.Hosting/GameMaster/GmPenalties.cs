using tPoseidon.Core.Data.Postgres;
using tPoseidon.Hosting.Database;

namespace tPoseidon.Hosting.GameMaster;

public interface IGmPenaltyBook
{
    void SetKind(PenaltyKind kind);

    void SetTime(short code, short count, int hours);

    IReadOnlyList<PenaltyKind> Kinds();

    int Penalize(string accountName, short code, long gameMasterAccountId, string note);

    int Release(string accountName, string gameMasterName);

    IReadOnlyList<RunningPenalty> Running();
}

public sealed class DatabasePenaltyBook : IGmPenaltyBook
{
    private readonly DatabaseSettings _database;

    public DatabasePenaltyBook(DatabaseSettings database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public void SetKind(PenaltyKind kind) => DatabaseAdmin.WithPenalties(_database, store =>
    {
        store.SetKind(kind);
        return 0;
    });

    public void SetTime(short code, short count, int hours) => DatabaseAdmin.WithPenalties(_database, store =>
    {
        store.SetTime(code, count, hours);
        return 0;
    });

    public IReadOnlyList<PenaltyKind> Kinds() => DatabaseAdmin.WithPenalties(_database, store => store.Kinds());

    public int Penalize(string accountName, short code, long gameMasterAccountId, string note) =>
        DatabaseAdmin.WithPenalties(_database, store => store.Penalize(accountName, code, gameMasterAccountId, note));

    public int Release(string accountName, string gameMasterName) =>
        DatabaseAdmin.WithPenalties(_database, store => store.Release(accountName, gameMasterName));

    public IReadOnlyList<RunningPenalty> Running() => DatabaseAdmin.WithPenalties(_database, store => store.Running());
}

public static class GmPenalties
{
    public const string KindName = "penalty-kind";
    public const string TimeName = "penalty-time";
    public const string PenalizeName = "penalty";
    public const string ReleaseName = "penalty-release";
    public const string ListName = "penalty-list";
    private const long OperatorAccountId = 0;
    private const string OperatorName = "operator";

    public static IReadOnlyList<string> Names { get; } = new[] { KindName, TimeName, PenalizeName, ReleaseName, ListName };

    public static IReadOnlyList<string> Help { get; } = new[]
    {
        $"  {KindName,-20} <code> <name> [kind]",
        "                       Add or rename a penalty kind; its name is shown to the player at the lobby.",
        $"                       tposeidon gm {KindName} 1001 \"Abusive chat\"",
        $"  {TimeName,-20} <code> <count> <hours>",
        "                       Length of the count-th penalty of a kind (a missing step blocks for 9999 hours).",
        $"                       tposeidon gm {TimeName} 1001 1 72",
        $"  {PenalizeName,-20} <account> <code> [\"note\"]",
        "                       Block an account from logging in; the lobby shows the kind and the release time.",
        $"                       tposeidon gm {PenalizeName} player1 1001 \"spam\"",
        $"  {ReleaseName,-20} <account>",
        "                       End an account's running penalties now.",
        $"  {ListName,-20}",
        "                       Show the penalty kinds and the running penalties.",
    };

    public static bool Handles(string subcommand) =>
        subcommand is KindName or TimeName or PenalizeName or ReleaseName or ListName;

    public static GmOutcome Run(IGmPenaltyBook book, string subcommand, IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(arguments);
        return subcommand switch
        {
            KindName => SetKind(book, arguments),
            TimeName => SetTime(book, arguments),
            PenalizeName => Penalize(book, arguments),
            ReleaseName => Release(book, arguments),
            _ => List(book),
        };
    }

    private static GmOutcome SetKind(IGmPenaltyBook book, IReadOnlyList<string> arguments)
    {
        short kind = 0;
        if (arguments.Count is < 2 or > 3 || !short.TryParse(arguments[0], out var code)
            || (arguments.Count == 3 && !short.TryParse(arguments[2], out kind)))
        {
            throw new HostingException($"Usage: tposeidon gm {KindName} <code> <name> [kind]");
        }

        book.SetKind(new PenaltyKind(code, arguments[1], kind));
        return new GmOutcome(GmOutcome.Done, new[] { $"Penalty kind {code} is '{arguments[1]}'." });
    }

    private static GmOutcome SetTime(IGmPenaltyBook book, IReadOnlyList<string> arguments)
    {
        if (arguments.Count != 3 || !short.TryParse(arguments[0], out var code)
            || !short.TryParse(arguments[1], out var count) || !int.TryParse(arguments[2], out var hours) || count < 1 || hours < 1)
        {
            throw new HostingException($"Usage: tposeidon gm {TimeName} <code> <count from 1> <hours>");
        }

        book.SetTime(code, count, hours);
        return new GmOutcome(GmOutcome.Done, new[] { $"Penalty {code}, step {count}: {hours} hour(s)." });
    }

    private static GmOutcome Penalize(IGmPenaltyBook book, IReadOnlyList<string> arguments)
    {
        if (arguments.Count is < 2 or > 3 || !short.TryParse(arguments[1], out var code))
        {
            throw new HostingException($"Usage: tposeidon gm {PenalizeName} <account> <code> [\"note\"]");
        }

        var note = arguments.Count == 3 ? arguments[2] : string.Empty;
        var result = book.Penalize(arguments[0], code, OperatorAccountId, note);
        var line = result switch
        {
            PenaltyResults.Ok => $"Account '{arguments[0]}' is blocked with penalty {code}.",
            PenaltyResults.NoAccount => $"There is no account named '{arguments[0]}'.",
            PenaltyResults.AlreadyPenalized => $"Account '{arguments[0]}' already has a running penalty.",
            PenaltyResults.Withdrawn => $"Account '{arguments[0]}' is withdrawn.",
            _ => $"The penalty was not stored (result {result}).",
        };
        var ok = result == PenaltyResults.Ok;
        return new GmOutcome(ok ? GmOutcome.Done : GmOutcome.NotDone, new[] { line }, Refused: !ok);
    }

    private static GmOutcome Release(IGmPenaltyBook book, IReadOnlyList<string> arguments)
    {
        if (arguments.Count != 1)
        {
            throw new HostingException($"Usage: tposeidon gm {ReleaseName} <account>");
        }

        var released = book.Release(arguments[0], OperatorName);
        return released == 0
            ? new GmOutcome(GmOutcome.NotDone, new[] { $"Account '{arguments[0]}' has no running penalty." }, Refused: true)
            : new GmOutcome(GmOutcome.Done, new[] { $"Released {released} penalty(ies) of '{arguments[0]}'." });
    }

    private static GmOutcome List(IGmPenaltyBook book)
    {
        var kinds = book.Kinds();
        var running = book.Running();
        var lines = new List<string> { kinds.Count == 0 ? "No penalty kinds." : "Penalty kinds:" };
        lines.AddRange(kinds.Select(kind => $"  {kind.Code,6}  {kind.Name} (kind {kind.Kind})"));
        lines.Add(running.Count == 0 ? "No running penalties." : "Running penalties:");
        lines.AddRange(running.Select(penalty =>
            $"  {penalty.AccountName}: {penalty.PenaltyName} #{penalty.Count}, {penalty.Hours} h from {penalty.AppliedAt.ToLocalTime():yyyy-MM-dd HH:mm} to {penalty.ReleaseAt.ToLocalTime():yyyy-MM-dd HH:mm}{(string.IsNullOrEmpty(penalty.Note) ? string.Empty : $" ({penalty.Note})")}"));
        return new GmOutcome(GmOutcome.Done, lines);
    }
}
