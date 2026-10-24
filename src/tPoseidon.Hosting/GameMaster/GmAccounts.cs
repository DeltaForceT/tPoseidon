using System.Net;
using tPoseidon.Core.Data;
using tPoseidon.Core.Data.Postgres;
using tPoseidon.Hosting.Database;

namespace tPoseidon.Hosting.GameMaster;

public interface IGmAccountBook
{
    GmCharacterRegistration? Register(string characterName, string permittedIp);

    GmCharacterEntry? Unregister(string characterName);

    IReadOnlyList<GmCharacterEntry> List();

    IReadOnlyList<CharacterSummary> FindCharacters(string text, int limit);
}

public sealed class DatabaseGmAccountBook : IGmAccountBook
{
    private readonly DatabaseSettings _database;

    public DatabaseGmAccountBook(DatabaseSettings database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public GmCharacterRegistration? Register(string characterName, string permittedIp) => DatabaseAdmin.RegisterGmCharacter(_database, characterName, permittedIp);

    public GmCharacterEntry? Unregister(string characterName) => DatabaseAdmin.UnregisterGmCharacter(_database, characterName);

    public IReadOnlyList<GmCharacterEntry> List() => DatabaseAdmin.ListGmCharacters(_database);

    public IReadOnlyList<CharacterSummary> FindCharacters(string text, int limit) => DatabaseAdmin.FindCharacters(_database, text, limit);
}

public static class GmAccounts
{
    public const string RegisterName = "register";
    public const string UnregisterName = "unregister";
    public const string ListName = "list";
    public const string LocalPermitIp = "127.0.0.1";

    public static IReadOnlyList<string> Names { get; } = new[] { RegisterName, UnregisterName, ListName };

    public static IReadOnlyList<string> Help { get; } = new[]
    {
        $"  {RegisterName,-20} --character <name> [--permit-ip <address>]",
        "                       Make a character its account's GM character (one per account) and allow GM",
        $"                       login from an address (default {LocalPermitIp}). Servers may keep running.",
        "                       tposeidon gm register --character Boss",
        $"  {UnregisterName,-20} --character <name>",
        "                       Take the GM character away again: its GM login addresses are removed and the",
        "                       account becomes a normal account.",
        $"  {ListName,-20}",
        "                       Show the registered GM characters and the addresses they may log in from.",
    };

    public static bool IsPermitAddress(string text) => IPAddress.TryParse(text, out _);

    public static GmOutcome Register(IGmAccountBook book, string character, string permitIp)
    {
        ArgumentNullException.ThrowIfNull(book);
        var registration = book.Register(character, permitIp)
            ?? throw new HostingException($"There is no character named '{character}'. Create it in game first.");
        return new GmOutcome(GmOutcome.Done, new[]
        {
            $"'{registration.CharacterName}' is now the GM character of account {registration.AccountId}.",
            $"GM login is allowed from {registration.PermittedIp}, with the account's password.",
        });
    }

    public static GmOutcome Unregister(IGmAccountBook book, string character)
    {
        ArgumentNullException.ThrowIfNull(book);
        var removed = book.Unregister(character);
        return removed is null
            ? new GmOutcome(GmOutcome.NotDone, new[] { $"'{character}' is not a registered GM character." }, Refused: true)
            : new GmOutcome(GmOutcome.Done, new[] { $"'{removed.CharacterName}' is no longer a GM character; account '{removed.AccountName}' is a normal account again." });
    }

    public static GmOutcome List(IGmAccountBook book)
    {
        ArgumentNullException.ThrowIfNull(book);
        var entries = book.List();
        var lines = new List<string> { entries.Count == 0 ? "No GM characters are registered." : "GM characters:" };
        lines.AddRange(entries.Select(Describe));
        return new GmOutcome(GmOutcome.Done, lines);
    }

    public static string Describe(GmCharacterEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var addresses = entry.PermittedIps.Count == 0 ? "no address" : string.Join(", ", entry.PermittedIps);
        return $"  {entry.CharacterName} (account '{entry.AccountName}', {entry.AccountId}): GM login from {addresses}, since {entry.CreatedAt.ToLocalTime():yyyy-MM-dd}";
    }
}
