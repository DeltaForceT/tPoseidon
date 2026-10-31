using System.Globalization;
using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Data.Postgres;
using tPoseidon.Core.Net.Packets;
using tPoseidon.Hosting.Database;

namespace tPoseidon.Hosting.GameMaster;

public interface IPlayerAccountBook
{
    bool Exists(string username);

    void Create(string username, string password);

    bool SetPassword(string username, string password);

    IReadOnlyList<AccountListing> List();
}

public sealed class DatabasePlayerAccountBook : IPlayerAccountBook
{
    private readonly DatabaseSettings _database;

    public DatabasePlayerAccountBook(DatabaseSettings database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public bool Exists(string username) => DatabaseAdmin.FindAccountName(_database, username) is not null;

    public void Create(string username, string password) => DatabaseAdmin.AddAccount(_database, username, password);

    public bool SetPassword(string username, string password) => DatabaseAdmin.SetAccountPassword(_database, username, password);

    public IReadOnlyList<AccountListing> List() => DatabaseAdmin.ListAccounts(_database);
}

public static class GmPlayerAccounts
{
    public const string AddName = "account-add";
    public const string PasswordName = "account-password";
    public const string ListName = "account-list";

    public static IReadOnlyList<string> Names { get; } = [AddName, PasswordName, ListName];

    public static string? NameProblem(string username)
    {
        ArgumentNullException.ThrowIfNull(username);
        if (username.Length == 0)
        {
            return "The account name cannot be empty.";
        }

        return Encoding.UTF8.GetByteCount(username) > LoginRequestPacket.UsernameFieldBytes
            ? string.Create(CultureInfo.InvariantCulture, $"The account name can be at most {LoginRequestPacket.UsernameFieldBytes} bytes: the game client cannot send a longer one at login.")
            : null;
    }

    public static string? PasswordProblem(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        if (password.Length == 0)
        {
            return "The password cannot be empty.";
        }

        return Encoding.UTF8.GetByteCount(password) > LoginRequestPacket.PasswordFieldBytes
            ? string.Create(CultureInfo.InvariantCulture, $"The password can be at most {LoginRequestPacket.PasswordFieldBytes} bytes: the game client cannot send a longer one at login.")
            : null;
    }

    public static GmOutcome Add(IPlayerAccountBook book, string username, string password)
    {
        ArgumentNullException.ThrowIfNull(book);
        var name = username.Trim();
        var problem = NameProblem(name) ?? PasswordProblem(password);
        if (problem is not null)
        {
            return Refuse(problem);
        }

        if (book.Exists(name))
        {
            return Refuse($"There is already an account named '{name}'. Use the password function to change its password.");
        }

        book.Create(name, password);
        return new GmOutcome(GmOutcome.Done, [$"Created account '{name}'. The player logs in to the game with it."]);
    }

    public static GmOutcome SetPassword(IPlayerAccountBook book, string username, string password)
    {
        ArgumentNullException.ThrowIfNull(book);
        var name = username.Trim();
        var problem = NameProblem(name) ?? PasswordProblem(password);
        if (problem is not null)
        {
            return Refuse(problem);
        }

        return book.SetPassword(name, password)
            ? new GmOutcome(GmOutcome.Done, [$"Changed the password of account '{name}'."])
            : Refuse($"There is no account named '{name}'.");
    }

    public static GmOutcome List(IPlayerAccountBook book)
    {
        ArgumentNullException.ThrowIfNull(book);
        var accounts = book.List();
        var lines = new List<string> { accounts.Count == 0 ? "There are no accounts." : string.Create(CultureInfo.InvariantCulture, $"{accounts.Count} accounts:") };
        lines.AddRange(accounts.Select(Describe));
        return new GmOutcome(GmOutcome.Done, lines);
    }

    public static string Describe(AccountListing account)
    {
        ArgumentNullException.ThrowIfNull(account);
        var markers = new List<string>();
        if (account.HasGmCharacter)
        {
            markers.Add("GM");
        }

        if (account.IsMember)
        {
            markers.Add("member");
        }

        var created = account.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return $"  {account.Username} · created {created}{(markers.Count == 0 ? string.Empty : " · " + string.Join(", ", markers))}";
    }

    private static GmOutcome Refuse(string line) => new(GmOutcome.NotDone, [line], Refused: true);
}
