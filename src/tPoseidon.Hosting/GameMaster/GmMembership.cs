using tPoseidon.Core.Data;
using tPoseidon.Core.Data.Postgres;
using tPoseidon.Hosting.Database;

namespace tPoseidon.Hosting.GameMaster;

public sealed class DatabaseMembershipBook : IMembershipStore
{
    private readonly DatabaseSettings _database;

    public DatabaseMembershipBook(DatabaseSettings database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public bool SetMember(string accountName, bool member) =>
        DatabaseAdmin.WithMembership(_database, store => store.SetMember(accountName, member));

    public bool SetMemberOfCharacter(string characterName, bool member) =>
        DatabaseAdmin.WithMembership(_database, store => store.SetMemberOfCharacter(characterName, member));

    public IReadOnlyList<string> Members() =>
        DatabaseAdmin.WithMembership(_database, store => store.Members());
}

public static class GmMembership
{
    public const string Name = "member";
    public const string OnWord = "on";
    public const string OffWord = "off";
    public const string ListWord = "list";

    public static IReadOnlyList<string> Help { get; } = new[]
    {
        $"  {Name,-20} <account> {OnWord}|{OffWord}",
        "                       Give an account the original's membership bonus (+20 percent walk, run and chase",
        "                       speed, and the PC-room EXP and TM rates) or take it away. It starts at the next",
        "                       login or zone change of the account's characters.",
        $"                       tposeidon gm {Name} player1 {OnWord}",
        $"  {Name,-20} {ListWord}",
        "                       Show the member accounts.",
    };

    public static GmOutcome Run(IMembershipStore book, IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Count == 1 && arguments[0].Equals(ListWord, StringComparison.OrdinalIgnoreCase))
        {
            return List(book);
        }

        if (arguments.Count != 2 || !TryParseSwitch(arguments[1], out var member))
        {
            throw new HostingException($"Usage: tposeidon gm {Name} <account> {OnWord}|{OffWord}  or  tposeidon gm {Name} {ListWord}");
        }

        return book.SetMember(arguments[0], member)
            ? new GmOutcome(GmOutcome.Done, new[] { $"Account '{arguments[0]}' is {(member ? "now" : "no longer")} a member; it applies at the next login or zone change." })
            : new GmOutcome(GmOutcome.NotDone, new[] { $"There is no account named '{arguments[0]}'." }, Refused: true);
    }

    public static bool TryParseSwitch(string word, out bool member)
    {
        ArgumentNullException.ThrowIfNull(word);
        member = word.Equals(OnWord, StringComparison.OrdinalIgnoreCase);
        return member || word.Equals(OffWord, StringComparison.OrdinalIgnoreCase);
    }

    private static GmOutcome List(IMembershipStore book)
    {
        var members = book.Members();
        var lines = new List<string> { members.Count == 0 ? "No account is a member." : "Member accounts:" };
        lines.AddRange(members.Select(name => $"  {name}"));
        return new GmOutcome(GmOutcome.Done, lines);
    }
}
